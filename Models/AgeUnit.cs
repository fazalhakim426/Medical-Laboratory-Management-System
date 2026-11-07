namespace LabReportApp.Models
{
    public static class AgeUnits
    {
        public const string Days = "Days";
        public const string Months = "Months";
        public const string Years = "Years";

        public static readonly string[] All = { Days, Months, Years };
    }

    public static class PregnancyStatuses
    {
        public const string NotApplicable = "Not Applicable";
        public const string Pregnant = "Pregnant";
        public const string NonPregnant = "Non-Pregnant";

        public static readonly string[] All = { NotApplicable, Pregnant, NonPregnant };
    }

    public static class RangeGenders
    {
        public const string Male = "Male";
        public const string Female = "Female";
        public const string Both = "Both";

        public static readonly string[] All = { Male, Female, Both };
    }

    public static class AbnormalFlags
    {
        public const string Low = "LOW";
        public const string High = "HIGH";
        public const string Normal = "NORMAL";
        public const string NotConfigured = "NOT CONFIGURED";
        public const string Qualitative = "";
    }
}
