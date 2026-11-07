using System;
using System.IO;
using System.Text.Json;
using LabReportApp.Models;

namespace LabReportApp.Services
{
    public static class LabProXSettingsStore
    {
        private static readonly object Gate = new();
        private static LabProXSettings? _cache;

        public static string SettingsPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LabProXSettings.json");

        public static LabProXSettings Current
        {
            get
            {
                lock (Gate)
                    return _cache ??= Load();
            }
        }

        public static void Save(LabProXSettings settings)
        {
            settings.Customer ??= new CustomerBranding();
            settings.Bill ??= PdfTemplateSettings.CreateBillDefaults();
            settings.Report ??= PdfTemplateSettings.CreateReportDefaults();
            settings.Customer.Normalize();
            settings.Bill.Kind = PdfDocumentKind.Bill;
            settings.Report.Kind = PdfDocumentKind.Report;
            settings.Bill.Normalize();
            settings.Report.Normalize();

            string json = JsonSerializer.Serialize(settings, LabProXJson.Options);
            string path = SettingsPath;
            string temp = path + ".tmp";
            lock (Gate)
            {
                File.WriteAllText(temp, json);
                File.Move(temp, path, true);
                _cache = settings;
            }
        }

        public static void SaveCustomer(CustomerBranding branding)
        {
            var all = Current.Clone();
            all.Customer = branding;
            Save(all);
        }

        public static void SaveDocument(PdfTemplateSettings template)
        {
            var all = Current.Clone();
            if (template.Kind == PdfDocumentKind.Report)
                all.Report = template;
            else
                all.Bill = template;
            Save(all);
        }

        public static string? ResolveLogoPath(CustomerBranding branding)
        {
            if (string.IsNullOrWhiteSpace(branding.LogoFileName))
                return null;
            string name = Path.GetFileName(branding.LogoFileName);
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Branding", name);
            return File.Exists(path) ? path : null;
        }

        private static LabProXSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new LabProXSettings();

                string json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<LabProXSettings>(json, LabProXJson.Options) ?? new LabProXSettings();
                settings.Customer ??= new CustomerBranding();
                settings.Bill ??= PdfTemplateSettings.CreateBillDefaults();
                settings.Report ??= PdfTemplateSettings.CreateReportDefaults();
                settings.Bill.Kind = PdfDocumentKind.Bill;
                settings.Report.Kind = PdfDocumentKind.Report;
                settings.Customer.Normalize();
                settings.Bill.Normalize();
                settings.Report.Normalize();
                return settings;
            }
            catch
            {
                return new LabProXSettings();
            }
        }
    }
}
