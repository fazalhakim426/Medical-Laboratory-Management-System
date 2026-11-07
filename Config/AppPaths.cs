using System;
using System.IO;

namespace LabReportApp.Config
{
    /// <summary>
    /// File names written next to the program. An older LabReports.db is renamed once
    /// so existing patient data opens under the Lab ProX name.
    /// </summary>
    public static class AppPaths
    {
        public const string DatabaseFileName = "LabProX.db";
        private const string LegacyDatabaseFileName = "LabReports.db";

        public static string DatabaseFile => ResolveDatabaseFile();

        private static string ResolveDatabaseFile()
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            string current = Path.Combine(folder, DatabaseFileName);
            string legacy = Path.Combine(folder, LegacyDatabaseFileName);
            if (!File.Exists(current) && File.Exists(legacy))
            {
                try
                {
                    File.Move(legacy, current);
                }
                catch (IOException)
                {
                    return legacy;
                }
                catch (UnauthorizedAccessException)
                {
                    return legacy;
                }
            }

            return current;
        }
    }
}
