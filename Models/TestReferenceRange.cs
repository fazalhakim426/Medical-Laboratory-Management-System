using System;

namespace LabReportApp.Models
{
    /// <summary>
    /// One demographic-specific reference interval for a predefined test.
    /// Age bounds are stored in days so Days/Months/Years patients can be matched uniformly.
    /// </summary>
    public class TestReferenceRange
    {
        public int Id { get; set; }
        public int TestDefinitionId { get; set; }

        /// <summary>Inclusive lower age bound in days. Null = no lower bound.</summary>
        public int? AgeMinDays { get; set; }

        /// <summary>Inclusive upper age bound in days. Null = no upper bound.</summary>
        public int? AgeMaxDays { get; set; }

        /// <summary>Male | Female | Both</summary>
        public string Gender { get; set; } = "Both";

        /// <summary>Not Applicable | Pregnant | Non-Pregnant</summary>
        public string PregnancyStatus { get; set; } = "Not Applicable";

        /// <summary>Numeric lower bound when the range is quantitative. Null for qualitative-only ranges.</summary>
        public double? MinValue { get; set; }

        /// <summary>Numeric upper bound when the range is quantitative. Null for qualitative-only ranges.</summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Display text for the report (e.g. "13.5-17.5" or "Negative" or "Non-reactive").
        /// Always preferred for UI/PDF display.
        /// </summary>
        public string ReferenceText { get; set; } = string.Empty;

        public string Unit { get; set; } = string.Empty;
        public string Methodology { get; set; } = string.Empty;

        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>True for sample/placeholder ranges that have not been clinically approved by the lab.</summary>
        public bool IsIllustrative { get; set; } = true;

        /// <summary>True when this range is qualitative (no numeric min/max comparison).</summary>
        public bool IsQualitative => !MinValue.HasValue && !MaxValue.HasValue;

        /// <summary>Friendly age-band label for management grids (derived, not stored).</summary>
        public string AgeGroupLabel
        {
            get
            {
                if (!AgeMinDays.HasValue && !AgeMaxDays.HasValue)
                    return "All ages";

                string Format(int days)
                {
                    if (days < 30) return $"{days}d";
                    if (days < 365) return $"{days / 30}mo";
                    return $"{days / 365}y";
                }

                if (AgeMinDays.HasValue && AgeMaxDays.HasValue)
                    return $"{Format(AgeMinDays.Value)} – {Format(AgeMaxDays.Value)}";
                if (AgeMinDays.HasValue)
                    return $"≥ {Format(AgeMinDays.Value)}";
                return $"≤ {Format(AgeMaxDays!.Value)}";
            }
        }
    }
}
