using System;
using System.Collections.Generic;
using System.IO;
using QuestPDF.Drawing;

namespace LabReportApp.Services
{
    public static class PdfFontRegistry
    {
        private static readonly object Gate = new();
        private static bool _ready;
        private static readonly HashSet<string> Families = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Styles = new(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<string> AvailableFamilies
        {
            get
            {
                Ensure();
                var list = new List<string>();
                foreach (var family in Families)
                {
                    if (!family.Equals("Lato", StringComparison.OrdinalIgnoreCase))
                        list.Add(family);
                }
                list.Sort(StringComparer.OrdinalIgnoreCase);
                list.Insert(0, "Lato");
                return list;
            }
        }

        public static void Ensure()
        {
            lock (Gate)
            {
                if (_ready)
                    return;
                _ready = true;
                QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

                string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                Register(fonts, "Arial", "arial.ttf", bold: false, italic: false);
                Register(fonts, "Arial", "arialbd.ttf", bold: true, italic: false);
                Register(fonts, "Arial", "ariali.ttf", bold: false, italic: true);
                Register(fonts, "Arial", "arialbi.ttf", bold: true, italic: true);
                Register(fonts, "Calibri", "calibri.ttf", bold: false, italic: false);
                Register(fonts, "Calibri", "calibrib.ttf", bold: true, italic: false);
                Register(fonts, "Calibri", "calibrii.ttf", bold: false, italic: true);
                Register(fonts, "Calibri", "calibriz.ttf", bold: true, italic: true);
                Register(fonts, "Segoe UI", "segoeui.ttf", bold: false, italic: false);
                Register(fonts, "Segoe UI", "segoeuib.ttf", bold: true, italic: false);
                Register(fonts, "Segoe UI", "segoeuii.ttf", bold: false, italic: true);
                Register(fonts, "Segoe UI", "segoeuiz.ttf", bold: true, italic: true);
                Register(fonts, "Times New Roman", "times.ttf", bold: false, italic: false);
                Register(fonts, "Times New Roman", "timesbd.ttf", bold: true, italic: false);
                Register(fonts, "Times New Roman", "timesi.ttf", bold: false, italic: true);
                Register(fonts, "Times New Roman", "timesbi.ttf", bold: true, italic: true);
                Register(fonts, "Georgia", "georgia.ttf", bold: false, italic: false);
                Register(fonts, "Georgia", "georgiab.ttf", bold: true, italic: false);
                Register(fonts, "Georgia", "georgiai.ttf", bold: false, italic: true);
                Register(fonts, "Georgia", "georgiaz.ttf", bold: true, italic: true);
                Register(fonts, "Verdana", "verdana.ttf", bold: false, italic: false);
                Register(fonts, "Verdana", "verdanab.ttf", bold: true, italic: false);
                Register(fonts, "Verdana", "verdanai.ttf", bold: false, italic: true);
                Register(fonts, "Verdana", "verdanaz.ttf", bold: true, italic: true);
                Register(fonts, "Tahoma", "tahoma.ttf", bold: false, italic: false);
                Register(fonts, "Tahoma", "tahomabd.ttf", bold: true, italic: false);
                Register(fonts, "Consolas", "consola.ttf", bold: false, italic: false);
                Register(fonts, "Consolas", "consolab.ttf", bold: true, italic: false);
                Register(fonts, "Consolas", "consolai.ttf", bold: false, italic: true);
                Register(fonts, "Consolas", "consolaz.ttf", bold: true, italic: true);
            }
        }

        public static string Resolve(string? requested)
        {
            Ensure();
            if (string.IsNullOrWhiteSpace(requested) || requested.Equals("Lato", StringComparison.OrdinalIgnoreCase))
                return "Lato";
            return Families.Contains(requested) ? requested : "Lato";
        }

        public static bool Supports(string? family, bool bold, bool italic)
        {
            Ensure();
            if (string.IsNullOrWhiteSpace(family) || family.Equals("Lato", StringComparison.OrdinalIgnoreCase))
                return true;
            return Styles.Contains(Key(family, bold, italic));
        }

        private static void Register(string folder, string family, string fileName, bool bold, bool italic)
        {
            string path = Path.Combine(folder, fileName);
            if (!File.Exists(path))
                return;

            try
            {
                FontManager.RegisterFont(File.OpenRead(path));
            }
            catch
            {
                // The font can already be visible to QuestPDF through Windows.
            }

            Families.Add(family);
            Styles.Add(Key(family, bold, italic));
        }

        private static string Key(string family, bool bold, bool italic) =>
            family.Trim() + "|" + (bold ? "b" : "") + (italic ? "i" : "");
    }
}
