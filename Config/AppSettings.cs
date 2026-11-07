namespace LabReportApp.Config
{
    /// <summary>
    /// EDIT THIS FILE with your own lab's details before running the app.
    /// Everything the app needs to know about your lab, your website (for hosting reports),
    /// your SMS provider, and your backup locations lives here in one place.
    /// </summary>
    public static class AppSettings
    {
        // Fallback hospital details used only when Settings has no hospital identity yet.
        // They never replace the Lab ProX name, logo, or WhatsApp number.
        public const string LabName = "Your LAB Name Here";
        public const string LabAddress = "Your Lab Address Here";
        public const string LabContact = "Your Lab Contact Info Here (phone/email/website)";

        // ---------- LOCAL STORAGE ----------
        // Folder where generated PDF reports are stored on this PC before upload.
        public const string LocalReportsFolder = "Reports";

        // Folder where generated Bill PDFs (Stage 1 - before results are entered) are stored.
        public const string LocalBillsFolder = "Bills";

        // ---------- ONLINE REPORT LINK ----------
        // The public base URL where your reports will be reachable.
        // Example: if your website's report folder is https://abclab.com/reports/
        // then a report with serial "LAB-000001" will be available at:
        // https://abclab.com/reports/LAB-000001.pdf
        public const string ReportsBaseUrl = "https://prestige-pk.com/reports/";

        // ---------- FTP UPLOAD (uploads the PDF to the above web folder) ----------
        // Ask your web hosting provider for these details (cPanel -> FTP Accounts).
        public const string FtpHost = "ftp.prestige-pk.com";
        public const string FtpUsername = "u217690852.prestigepk";
        public const string FtpPassword = "Tayyab@3344";
        public const string FtpRemoteDirectory = "/"; // remote folder matching ReportsBaseUrl

        // ---------- SMS GATEWAY ----------
        // Most local SMS providers (Telenor, Jazz, Whitesms.pk, SMSAlert.pk, Twilio, etc.)
        // give you a simple URL you call with the phone number and message.
        // Put that template here using {phone} and {message} as placeholders.
        // public const string SmsApiUrlTemplate = "https://smsapi.example.pk/send?apikey=YOUR_API_KEY&to={phone}&message={message}";

        // Set to false if you don't want the app to attempt SMS sending (e.g. testing offline).
        // public const bool EnableSmsSending = true;

        // Set to false if you don't want the app to attempt FTP upload (e.g. testing offline).
        // static readonly (not const) so the compiler still type-checks the upload path when this is true.
        public static readonly bool EnableCloudUpload = true;

        // ---------- BACKUP SETTINGS ----------
        // IMPORTANT: point BackupFolder at a location that is PHYSICALLY SEPARATE from this PC's
        // main drive - e.g. a USB flash drive (like "E:\LabBackups"), an external hard drive,
        // or a second internal drive. If this PC's main drive fails, a backup on the SAME drive
        // won't help you. A USB drive that's always plugged in is the simplest reliable option.
        // G: was unavailable on this PC — using a local folder. Point this back to a USB/external
        // drive (e.g. @"G:\LabReportBackups") when one is available for safer disaster recovery.
        public const string BackupFolder = @"F:\www\Medical-Laboratory-Management-System\Backups";

        // OPTIONAL second backup location for extra safety - e.g. a OneDrive or Google Drive
        // desktop-sync folder path (so backups automatically also go to the cloud whenever
        // there is internet). Leave this as an empty string "" to disable the second backup.
        public const string SecondaryBackupFolder = "";

        // How many timestamped database snapshots to keep before deleting the oldest ones.
        // PDF report backups are NEVER auto-deleted - only these database snapshots are limited.
        public const int MaxDatabaseBackupsToKeep = 30;

        // Master switch for the whole backup system.
        // static readonly (not const) so the compiler still type-checks the backup path when this is true.
        public static readonly bool EnableAutoBackup = true;
    }
}