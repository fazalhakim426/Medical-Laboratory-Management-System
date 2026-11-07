using System;
using System.Diagnostics;
using System.IO;
using LabReportApp.Config;
using LabReportApp.Database;

namespace LabReportApp.Services
{
    public static class ReportPrintService
    {
        public static string GetLocalPdfPath(string serialNumber) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppSettings.LocalReportsFolder, $"{serialNumber}.pdf");

        public static bool TryOpen(string serialNumber, out string error)
        {
            error = "";
            string path = GetLocalPdfPath(serialNumber);
            if (!File.Exists(path))
            {
                error = "The PDF file was not found locally. It may have been moved or deleted.";
                return false;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }

        /// <summary>
        /// Sends the PDF to the system print handler (separate process per file so each
        /// patient's report prints on its own job/page set). Marks the visit as Printed
        /// after the print verb is successfully started.
        /// </summary>
        public static bool TryPrint(string serialNumber, int visitId, out string error)
        {
            error = "";
            string path = GetLocalPdfPath(serialNumber);
            if (!File.Exists(path))
            {
                error = "The PDF file was not found locally.";
                return false;
            }

            try
            {
                string printer = LabProXSettingsStore.Current.Report.PreferredPrinter ?? "";
                var psi = new ProcessStartInfo(path)
                {
                    UseShellExecute = true,
                    Verb = string.IsNullOrWhiteSpace(printer) ? "print" : "printto",
                    Arguments = string.IsNullOrWhiteSpace(printer) ? "" : "\"" + printer.Replace("\"", "") + "\""
                };
                Process.Start(psi);
                DatabaseHelper.MarkVisitPrinted(visitId);
                return true;
            }
            catch (Exception ex)
            {
                // Fallback: open the PDF so the user can print manually.
                try
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    error = $"Could not send directly to printer ({ex.Message}). The PDF was opened for manual printing.";
                }
                catch (Exception openEx)
                {
                    error = openEx.Message;
                    return false;
                }

                return false;
            }
        }
    }
}
