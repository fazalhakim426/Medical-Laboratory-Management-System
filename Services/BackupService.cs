using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using LabReportApp.Config;

namespace LabReportApp.Services
{
    public static class BackupService
    {
        private static string DbFile => AppPaths.DatabaseFile;
        private static string ConnectionString => $"Data Source={DbFile}";

        /// <summary>
        /// Creates a fresh, consistent snapshot of the live database using SQLite's own
        /// "VACUUM INTO" command (safe to run even while the database is in use - it never
        /// copies a half-written file) and saves it into the configured backup folder(s),
        /// with a timestamp in the filename. Old timestamped backups beyond the configured
        /// retention count are automatically deleted.
        /// </summary>
        public static bool BackupDatabase(out string message)
        {
            message = string.Empty;

            if (!AppSettings.EnableAutoBackup)
                return false;

            bool anySuccess = false;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"LabProX_backup_{timestamp}.db";

            foreach (var folder in GetBackupFolders())
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    string backupPath = Path.Combine(folder, backupFileName);
                    string escapedPath = backupPath.Replace("'", "''"); // escape for the SQL string literal

                    using (var connection = new SqliteConnection(ConnectionString))
                    {
                        connection.Open();
                        using var cmd = new SqliteCommand($"VACUUM INTO '{escapedPath}';", connection);
                        cmd.ExecuteNonQuery();
                    }

                    CleanupOldBackups(folder);
                    anySuccess = true;
                }
                catch (Exception ex)
                {
                    message += $"Backup to '{folder}' failed: {ex.Message}\n";
                    LogBackupIssue(ex.Message, folder);
                }
            }

            if (anySuccess)
                message = $"Database backed up successfully at {DateTime.Now:dd-MMM-yyyy HH:mm:ss}.\n" + message;

            return anySuccess;
        }

        /// <summary>
        /// Copies a generated report PDF into the backup folder(s) too, so every report is
        /// safe even if the local Reports folder or the database is ever lost or corrupted.
        /// PDF backups are never auto-deleted (medical records should be kept indefinitely).
        /// </summary>
        public static void BackupPdfReport(string localPdfPath)
        {
            if (!AppSettings.EnableAutoBackup || !File.Exists(localPdfPath))
                return;

            foreach (var folder in GetBackupFolders())
            {
                try
                {
                    string reportsBackupFolder = Path.Combine(folder, "Reports");
                    Directory.CreateDirectory(reportsBackupFolder);
                    string destination = Path.Combine(reportsBackupFolder, Path.GetFileName(localPdfPath));
                    File.Copy(localPdfPath, destination, overwrite: true);
                }
                catch (Exception ex)
                {
                    LogBackupIssue(ex.Message, folder);
                }
            }
        }

        /// <summary>
        /// Restores the live database from a chosen backup file. A safety copy of the current
        /// (possibly corrupted) database is kept first, just in case. The application should
        /// be restarted after a successful restore.
        /// </summary>
        public static bool RestoreDatabase(string backupFilePath, out string message)
        {
            message = string.Empty;

            if (!File.Exists(backupFilePath))
            {
                message = "Selected backup file was not found.";
                return false;
            }

            try
            {
                if (File.Exists(DbFile))
                {
                    string safetyCopy = Path.Combine(
                        Path.GetDirectoryName(DbFile) ?? ".",
                        $"LabProX_beforeRestore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                    File.Copy(DbFile, safetyCopy, overwrite: true);
                }

                File.Copy(backupFilePath, DbFile, overwrite: true);
                message = "Database restored successfully. Please restart the application.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Restore failed: {ex.Message}";
                return false;
            }
        }

        private static string[] GetBackupFolders()
        {
            var folders = new List<string>();

            if (!string.IsNullOrWhiteSpace(AppSettings.BackupFolder))
                folders.Add(AppSettings.BackupFolder);

            if (!string.IsNullOrWhiteSpace(AppSettings.SecondaryBackupFolder))
                folders.Add(AppSettings.SecondaryBackupFolder);

            return folders.ToArray();
        }

        private static void CleanupOldBackups(string folder)
        {
            try
            {
                var backups = new DirectoryInfo(folder)
                    .GetFiles("LabProX_backup_*.db")
                    .Concat(new DirectoryInfo(folder).GetFiles("LabReports_backup_*.db"))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                foreach (var old in backups.Skip(AppSettings.MaxDatabaseBackupsToKeep))
                    old.Delete();
            }
            catch
            {
                // Non-critical - if cleanup fails we simply keep a few extra backups than intended.
            }
        }

        private static void LogBackupIssue(string errorText, string folder)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Backup to '{folder}' failed: {errorText}\n");
            }
            catch
            {
                // If we can't even write the log, there's nothing more we can do here.
            }
        }
    }
}
