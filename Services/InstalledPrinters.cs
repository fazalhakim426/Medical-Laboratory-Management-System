using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;

namespace LabReportApp.Services
{
    public static class InstalledPrinters
    {
        public static IReadOnlyList<string> GetNames()
        {
            var names = new List<string>();
            try
            {
                using var server = new LocalPrintServer();
                foreach (PrintQueue queue in server.GetPrintQueues())
                {
                    if (!string.IsNullOrWhiteSpace(queue.Name))
                        names.Add(queue.Name);
                    queue.Dispose();
                }
            }
            catch
            {
                // Printer enumeration is optional. Layout still follows the paper size.
            }

            return names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
