using System;
using System.Text.Json;

namespace LabReportApp.Models
{
    public enum PdfDocumentKind
    {
        Bill,
        Report
    }

    public enum PaperSizeKind
    {
        A4,
        A5,
        Thermal58,
        Thermal80,
        Custom
    }

    public enum PdfTextAlign
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Layout for one PDF type. Bill and report each keep their own copy.
    /// Nothing in here can hide the Lab ProX footer.
    /// </summary>
    public class PdfTemplateSettings
    {
        public PdfDocumentKind Kind { get; set; }

        public PaperSizeKind PaperSize { get; set; } = PaperSizeKind.A4;
        public double CustomWidthMm { get; set; } = 210;
        public double CustomHeightMm { get; set; } = 297;
        public bool CustomContinuous { get; set; }
        public string PreferredPrinter { get; set; } = "";

        public double MarginTopMm { get; set; } = 10;
        public double MarginRightMm { get; set; } = 10;
        public double MarginBottomMm { get; set; } = 10;
        public double MarginLeftMm { get; set; } = 10;

        public double SectionSpacingMm { get; set; } = 3;
        public double CellPaddingMm { get; set; } = 1.4;

        public bool ShowPageBorder { get; set; }
        public double BorderThickness { get; set; } = 0.8;
        public string BorderColor { get; set; } = "#94A3B8";

        public string DocumentTitle { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string HeaderNote { get; set; } = "";
        public PdfTextAlign HeaderAlignment { get; set; } = PdfTextAlign.Left;
        public bool ShowLogo { get; set; } = true;
        public double LogoHeightMm { get; set; } = 16;
        public bool ShowHeaderLine { get; set; } = true;
        public string HeaderBackground { get; set; } = "#FFFFFF";
        public string HeaderTextColor { get; set; } = "#0F172A";
        public string TitleColor { get; set; } = "#1457B8";

        public string HeadingFontFamily { get; set; } = "Lato";
        public double HeadingFontSize { get; set; } = 16;
        public bool HeadingBold { get; set; } = true;
        public string BodyFontFamily { get; set; } = "Lato";
        public double BodyFontSize { get; set; } = 10;
        public string BodyColor { get; set; } = "#1E293B";
        public string MutedColor { get; set; } = "#64748B";
        public string AccentColor { get; set; } = "#1457B8";

        public string TableHeaderBackground { get; set; } = "#E8EEF5";
        public string TableHeaderColor { get; set; } = "#0F172A";
        public string TableLineColor { get; set; } = "#CBD5E1";
        public bool ShowTableBorders { get; set; } = true;

        public bool ShowSerial { get; set; } = true;
        public bool ShowDate { get; set; } = true;
        public bool ShowPatientName { get; set; } = true;
        public bool ShowAgeGender { get; set; } = true;
        public bool ShowPhone { get; set; } = true;
        public bool ShowCnic { get; set; } = true;
        public bool ShowReferredBy { get; set; } = true;
        public bool ShowQrCode { get; set; } = true;

        public bool ShowPregnancy { get; set; } = true;
        public bool ShowUnit { get; set; } = true;
        public bool ShowReferenceRange { get; set; } = true;
        public bool ShowStatus { get; set; } = true;
        public bool ShowCategoryHeadings { get; set; } = true;
        public bool ShowEndOfReport { get; set; } = true;

        public bool ShowPrice { get; set; } = true;
        public bool ShowDiscount { get; set; } = true;
        public bool ShowNet { get; set; } = true;
        public bool ShowTotals { get; set; } = true;

        public string CustomNote { get; set; } = "";
        public bool ShowCustomNote { get; set; } = true;
        public bool ShowSignature { get; set; }
        public string SignatureName { get; set; } = "";
        public string SignatureTitle { get; set; } = "";
        public bool ShowStamp { get; set; }
        public string StampText { get; set; } = "";

        public bool ShowAddressInFooter { get; set; }
        public bool ShowContactInFooter { get; set; }
        public bool ShowFooterLine { get; set; } = true;
        public string FooterBackground { get; set; } = "#FFFFFF";
        public string FooterNote { get; set; } = "";
        public PdfTextAlign FooterAlignment { get; set; } = PdfTextAlign.Center;

        public double PaperWidthMm => PaperSize switch
        {
            PaperSizeKind.A4 => 210,
            PaperSizeKind.A5 => 148,
            PaperSizeKind.Thermal58 => 58,
            PaperSizeKind.Thermal80 => 80,
            _ => CustomWidthMm <= 0 ? 210 : CustomWidthMm
        };

        public bool IsNarrow => PaperWidthMm < 100;
        public bool IsVeryNarrow => PaperWidthMm < 72;

        public static PdfTemplateSettings CreateBillDefaults() => new()
        {
            Kind = PdfDocumentKind.Bill,
            PaperSize = PaperSizeKind.A5,
            DocumentTitle = "Patient Bill / Receipt",
            HeadingFontSize = 15,
            BodyFontSize = 9,
            MarginTopMm = 8,
            MarginRightMm = 8,
            MarginBottomMm = 8,
            MarginLeftMm = 8,
            ShowCustomNote = true,
            CustomNote = "This is a bill for the requested tests. Scan the QR code to download the report when it is ready.",
            ShowAddressInFooter = true,
            ShowContactInFooter = true,
            SignatureTitle = "Cashier"
        };

        public static PdfTemplateSettings CreateReportDefaults() => new()
        {
            Kind = PdfDocumentKind.Report,
            PaperSize = PaperSizeKind.A4,
            DocumentTitle = "Laboratory Report",
            HeadingFontSize = 18,
            BodyFontSize = 10,
            MarginTopMm = 10,
            MarginRightMm = 10,
            MarginBottomMm = 10,
            MarginLeftMm = 10,
            ShowCustomNote = false,
            ShowSignature = true,
            ShowAddressInFooter = true,
            ShowContactInFooter = true,
            SignatureTitle = "Lab In-Charge"
        };

        public void Normalize()
        {
            CustomWidthMm = Clamp(CustomWidthMm <= 0 ? 210 : CustomWidthMm, 40, 500);
            CustomHeightMm = Clamp(CustomHeightMm <= 0 ? 297 : CustomHeightMm, 40, 1200);

            double maxMargin = IsNarrow ? 5 : 25;
            MarginTopMm = Clamp(MarginTopMm, 0, maxMargin);
            MarginRightMm = Clamp(MarginRightMm, 0, maxMargin);
            MarginBottomMm = Clamp(MarginBottomMm, 0, maxMargin);
            MarginLeftMm = Clamp(MarginLeftMm, 0, maxMargin);

            double horizontal = MarginLeftMm + MarginRightMm;
            double limit = PaperWidthMm * 0.4;
            if (horizontal > limit && horizontal > 0)
            {
                double scale = limit / horizontal;
                MarginLeftMm = Math.Round(MarginLeftMm * scale, 2);
                MarginRightMm = Math.Round(MarginRightMm * scale, 2);
            }

            SectionSpacingMm = Clamp(SectionSpacingMm, 0, 15);
            CellPaddingMm = Clamp(CellPaddingMm, 0.3, 5);
            BorderThickness = Clamp(BorderThickness, 0.3, 4);
            LogoHeightMm = Clamp(LogoHeightMm <= 0 ? 16 : LogoHeightMm, 8, 40);
            HeadingFontSize = Clamp(HeadingFontSize, 8, 32);
            BodyFontSize = Clamp(BodyFontSize, 6, 18);

            DocumentTitle = Clip(DocumentTitle, 120);
            Subtitle = Clip(Subtitle, 160);
            HeaderNote = Clip(HeaderNote, 500);
            CustomNote = Clip(CustomNote, 800);
            FooterNote = Clip(FooterNote, 400);
            SignatureName = Clip(SignatureName, 80);
            SignatureTitle = Clip(SignatureTitle, 80);
            StampText = Clip(StampText, 80);
            PreferredPrinter = Clip(PreferredPrinter, 200);

            if (string.IsNullOrWhiteSpace(HeadingFontFamily)) HeadingFontFamily = "Lato";
            if (string.IsNullOrWhiteSpace(BodyFontFamily)) BodyFontFamily = "Lato";

            HeaderBackground = PdfColors.Normalize(HeaderBackground, "#FFFFFF");
            HeaderTextColor = PdfColors.Normalize(HeaderTextColor, "#0F172A");
            TitleColor = PdfColors.Normalize(TitleColor, "#1457B8");
            BodyColor = PdfColors.Normalize(BodyColor, "#1E293B");
            MutedColor = PdfColors.Normalize(MutedColor, "#64748B");
            AccentColor = PdfColors.Normalize(AccentColor, "#1457B8");
            TableHeaderBackground = PdfColors.Normalize(TableHeaderBackground, "#E8EEF5");
            TableHeaderColor = PdfColors.Normalize(TableHeaderColor, "#0F172A");
            TableLineColor = PdfColors.Normalize(TableLineColor, "#CBD5E1");
            BorderColor = PdfColors.Normalize(BorderColor, "#94A3B8");
            FooterBackground = PdfColors.Normalize(FooterBackground, "#FFFFFF");
        }

        public PdfTemplateSettings Clone()
        {
            string json = JsonSerializer.Serialize(this, LabProXJson.Options);
            return JsonSerializer.Deserialize<PdfTemplateSettings>(json, LabProXJson.Options) ?? new PdfTemplateSettings();
        }

        public string ResolvedTitle()
        {
            if (!string.IsNullOrWhiteSpace(DocumentTitle))
                return DocumentTitle.Trim();
            return Kind == PdfDocumentKind.Bill ? "Patient Bill / Receipt" : "Laboratory Report";
        }

        private static double Clamp(double value, double min, double max) => Math.Min(max, Math.Max(min, value));

        private static string Clip(string? value, int max)
        {
            value = value?.Trim() ?? "";
            return value.Length <= max ? value : value[..max];
        }
    }

    public static class PdfColors
    {
        public static bool TryHex(string? text, out string hex)
        {
            hex = "";
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string s = text.Trim();
            if (s.StartsWith("#"))
                s = s[1..];
            if (s.Length == 3)
                s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
            if (s.Length != 6)
                return false;

            foreach (char c in s)
            {
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok)
                    return false;
            }

            hex = "#" + s.ToUpperInvariant();
            return true;
        }

        public static string Normalize(string? text, string fallback) =>
            TryHex(text, out var hex) ? hex : fallback;
    }
}
