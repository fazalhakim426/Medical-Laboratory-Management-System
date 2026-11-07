using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LabReportApp.Database;
using LabReportApp.Services;
using QuestPDF.Infrastructure;

namespace LabReportApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // A click anywhere on a dropdown opens its list, including editable boxes
            // and dropdowns inside a table (the first click would otherwise only select the row).
            EventManager.RegisterClassHandler(
                typeof(ComboBox),
                UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(ComboBox_OpenOnClick),
                handledEventsToo: true);

            // Catch ANY unhandled exception anywhere in the app and show it in a message box,
            // instead of the app silently crashing with no visible error (which is what a plain
            // "Exit Code 1" with no console output usually means for a WPF app).
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                // Apply the last-used theme (Light/Dark) before anything else is shown.
                ThemeManager.LoadSavedTheme();

                // QuestPDF is free for small companies/individuals under the Community license.
                // Read the license terms here: https://www.questpdf.com/license/
                QuestPDF.Settings.License = LicenseType.Community;
                PdfFontRegistry.Ensure();

                // Make sure the local SQLite database and tables exist before the app runs.
                DatabaseHelper.InitializeDatabase();

                // Take a backup snapshot every time the app starts, as an extra safety net.
                // Wrapped in try/catch so a missing/unplugged backup drive never stops the app from opening.
                try
                {
                    BackupService.BackupDatabase(out _);
                }
                catch
                {
                    // Ignore - the manual "Backup Now" button and the automatic per-report backup
                    // will still work once the backup destination is available.
                }
            }
            catch (Exception ex)
            {
                ShowFatalError("Startup", ex);
                Shutdown();
            }
        }

        private static void ComboBox_OpenOnClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ComboBox combo || !combo.IsEnabled || combo.IsDropDownOpen)
                return;

            // Open after this click finishes, so the list does not close again immediately.
            combo.Dispatcher.BeginInvoke(() =>
            {
                if (!combo.IsEnabled || combo.IsDropDownOpen)
                    return;
                combo.Focus();
                combo.IsDropDownOpen = true;
            }, DispatcherPriority.Input);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowFatalError("UI Thread / Window Startup", e.Exception);
            e.Handled = true;
            Shutdown(); // exit cleanly instead of hanging with no window open
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                ShowFatalError("Background Thread", ex);
        }

        private void ShowFatalError(string source, Exception ex)
        {
            var messageBuilder = new System.Text.StringBuilder();
            messageBuilder.AppendLine($"An unexpected error occurred ({source}):");
            messageBuilder.AppendLine();

            // Unwrap the full chain of inner exceptions - the outer one (e.g. "Exception has
            // been thrown by the target of an invocation") is usually just a generic wrapper;
            // the REAL cause is in the innermost exception.
            Exception? current = ex;
            int level = 0;
            while (current != null)
            {
                messageBuilder.AppendLine($"[{level}] {current.GetType().Name}: {current.Message}");
                current = current.InnerException;
                level++;
            }

            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Full stack trace:");
            messageBuilder.AppendLine(ex.ToString());

            string message = messageBuilder.ToString();

            // NOTE: This app's OutputType is WinExe (a Windows GUI app), so Console.WriteLine
            // does NOT show up in the terminal even when launched via "dotnet run" - Windows
            // does not attach a console to GUI-subsystem processes. Writing to a log file next
            // to the .exe is the only reliable way to see this message.
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                File.WriteAllText(logPath, $"[{DateTime.Now}] {source}:\n{message}\n");

                // Try to open the log file automatically in Notepad so you see it right away.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(logPath) { UseShellExecute = true });
            }
            catch
            {
                // If we can't even write/open the log, there's nothing more we can do here.
            }
        }
    }
}