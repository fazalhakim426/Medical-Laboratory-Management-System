using System;
using System.Collections.Generic;
using LabReportApp.Config;
using LabReportApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace LabReportApp.Services
{
    public static class PdfPreviewService
    {
        private static byte[]? _qr;

        public static byte[]? Render(PdfDocumentKind kind, PdfTemplateSettings template, CustomerBranding branding)
        {
            PdfFontRegistry.Ensure();
            var patient = SamplePatient();
            var tests = SampleTests();
            byte[] qr = SampleQr();
            IDocument document = kind == PdfDocumentKind.Bill
                ? BillService.BuildDocument(patient, tests, qr, template, branding)
                : PdfReportService.BuildDocument(patient, tests, qr, template, branding);

            foreach (byte[] image in document.GenerateImages(new ImageGenerationSettings { RasterDpi = 110 }))
                return image;
            return null;
        }

        private static Patient SamplePatient() => new()
        {
            SerialNumber = "LAB-000123",
            Name = "Ayesha Khan",
            Age = 29,
            AgeValue = 29,
            AgeUnit = AgeUnits.Years,
            Gender = "Female",
            PhoneNumber = "03001234567",
            Cnic = "35202-1234567-8",
            ReferredBy = "Dr. Ahmed",
            PregnancyStatus = PregnancyStatuses.NonPregnant,
            CreatedAt = DateTime.Now,
            DiscountPercentage = 10,
            ReportHeading = "Laboratory Report"
        };

        private static List<TestResultItem> SampleTests()
        {
            return new List<TestResultItem>
            {
                Row("Complete Blood Count", "Hemoglobin", "13.4", "g/dL", "12.0 - 15.5", 12, 15.5, 800, 0),
                Row("Complete Blood Count", "WBC", "14.2", "10^9/L", "4.0 - 11.0", 4, 11, 600, 0),
                Row("Liver Function Tests", "ALT", "32", "U/L", "0 - 45", 0, 45, 900, 5),
                Row("Liver Function Tests", "AST", "28", "U/L", "0 - 40", 0, 40, 900, 0)
            };
        }

        private static TestResultItem Row(
            string category, string name, string result, string unit, string range,
            double min, double max, decimal price, decimal discount)
        {
            var item = new TestResultItem
            {
                CategoryHeading = category,
                TestName = name,
                Unit = unit,
                NormalRange = range,
                RangeMinValue = min,
                RangeMaxValue = max,
                Price = price,
                CatalogPrice = price,
                Discount = discount,
                DiscountMode = "Percent"
            };
            item.Result = result;
            return item;
        }

        private static byte[] SampleQr()
        {
            if (_qr != null)
                return _qr;
            try
            {
                string url = AppSettings.ReportsBaseUrl.TrimEnd('/') + "/LAB-PREVIEW.pdf";
                _qr = QrCodeService.GenerateQrPng(url);
            }
            catch
            {
                _qr = Array.Empty<byte>();
            }
            return _qr;
        }
    }
}
