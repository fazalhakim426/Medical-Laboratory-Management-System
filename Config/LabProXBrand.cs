namespace LabReportApp.Config
{
    /// <summary>
    /// Permanent Lab ProX identity. Customer settings must never replace these values.
    /// </summary>
    public static class LabProXBrand
    {
        public const string ProductName = "Lab ProX";
        public const string WhatsAppNumber = "+923425534260";
        public const string WhatsAppDisplay = "+92 342 5534 260";
        public const string WhatsAppUrl = "https://wa.me/923425534260";
        public const string FooterText = "Powered by Lab ProX | WhatsApp: +923425534260";
        public const string SalesLabel = "Sales inquiries";

        /// <summary>Official mark used on every PDF footer. Not a customer logo.</summary>
        public const string LogoSvg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\">" +
            "<rect width=\"64\" height=\"64\" rx=\"14\" fill=\"#1457B8\"/>" +
            "<path fill=\"#FFFFFF\" d=\"M26 10 H38 V22 L46.4 38.4 A9.2 9.2 0 0 1 38 52 H26 A9.2 9.2 0 0 1 17.6 38.4 L26 22 Z\"/>" +
            "</svg>";
    }
}
