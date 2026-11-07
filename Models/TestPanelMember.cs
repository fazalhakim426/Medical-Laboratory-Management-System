namespace LabReportApp.Models
{
    /// <summary>
    /// Links a panel test (e.g. CBC) to one of its result parameters (e.g. Hemoglobin).
    /// The panel itself is billed; members appear on the result-entry / report screens.
    /// </summary>
    public class TestPanelMember
    {
        public int Id { get; set; }
        public int PanelTestId { get; set; }
        public int MemberTestId { get; set; }
        public int SortOrder { get; set; }

        // Convenience fields populated by joins (not always persisted).
        public string MemberTestName { get; set; } = string.Empty;
        public string MemberUnit { get; set; } = string.Empty;
        public string MemberCategory { get; set; } = string.Empty;
    }
}
