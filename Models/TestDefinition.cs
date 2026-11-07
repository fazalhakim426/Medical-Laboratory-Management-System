namespace LabReportApp.Models
{
    public class TestDefinition
    {
        public int Id { get; set; }
        public string CategoryHeading { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// Legacy single free-text range used as fallback when no TestReferenceRanges exist.
        /// </summary>
        public string NormalRange { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsPanel { get; set; }

        /// <summary>Shown in autocomplete dropdowns.</summary>
        public string SuggestionLabel =>
            IsPanel
                ? $"{TestName}  ·  Panel  ·  Rs. {Price:N0}"
                : $"{TestName}  ·  {CategoryHeading}  ·  Rs. {Price:N0}";

        // Editable ComboBoxes use ToString() for the closed/selected text.
        public override string ToString() => TestName ?? string.Empty;
    }
}
