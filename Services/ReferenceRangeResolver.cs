using System;
using System.Collections.Generic;
using System.Linq;
using LabReportApp.Database;
using LabReportApp.Models;

namespace LabReportApp.Services
{
    public class ReferenceRangeResolution
    {
        public TestReferenceRange? Range { get; init; }
        public string DisplayText { get; init; } = "Reference Range Not Configured";
        public string Unit { get; init; } = string.Empty;
        public bool IsConfigured => Range != null;
        public bool RequiresPregnancyWarning { get; init; }
        public string WarningMessage { get; init; } = string.Empty;
    }

    /// <summary>
    /// Picks the best matching TestReferenceRange for a patient demographic + test.
    /// Never invents a range — returns "Reference Range Not Configured" when none match.
    /// </summary>
    public static class ReferenceRangeResolver
    {
        public static ReferenceRangeResolution Resolve(int testDefinitionId, Patient patient, DateTime asOf)
        {
            var ranges = DatabaseHelper.GetReferenceRangesForTest(testDefinitionId);
            int ageDays = AgeCalculator.GetAgeInDays(patient, asOf);
            string pregnancy = string.IsNullOrWhiteSpace(patient.PregnancyStatus)
                ? PregnancyStatuses.NotApplicable
                : patient.PregnancyStatus;
            return Resolve(ranges, ageDays, patient.Gender ?? "", pregnancy, asOf);
        }

        /// <summary>Overload used by DatabaseHelper when ranges are already loaded in a transaction.</summary>
        public static ReferenceRangeResolution Resolve(
            IEnumerable<TestReferenceRange> ranges,
            int ageDays,
            string gender,
            string pregnancy,
            DateTime asOf)
        {
            var list = ranges
                .Where(r => r.IsActive)
                .Where(r => IsEffective(r, asOf))
                .ToList();

            if (list.Count == 0)
            {
                return new ReferenceRangeResolution
                {
                    DisplayText = "Reference Range Not Configured"
                };
            }

            gender = NormalizeGender(gender);
            if (string.IsNullOrWhiteSpace(pregnancy))
                pregnancy = PregnancyStatuses.NotApplicable;

            bool testHasPregnancySpecific = list.Any(r =>
                r.PregnancyStatus is PregnancyStatuses.Pregnant or PregnancyStatuses.NonPregnant);

            if (testHasPregnancySpecific &&
                string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase) &&
                pregnancy is not (PregnancyStatuses.Pregnant or PregnancyStatuses.NonPregnant))
            {
                return new ReferenceRangeResolution
                {
                    DisplayText = "Reference Range Not Configured",
                    RequiresPregnancyWarning = true,
                    WarningMessage = "This test has pregnancy-specific reference ranges. Please set Pregnancy Status for this patient."
                };
            }

            var candidates = list
                .Where(r => AgeMatches(r, ageDays))
                .Where(r => GenderMatches(r, gender))
                .Where(r => PregnancyMatches(r, pregnancy, gender))
                .ToList();

            if (candidates.Count == 0)
            {
                return new ReferenceRangeResolution
                {
                    DisplayText = "Reference Range Not Configured"
                };
            }

            var best = candidates
                .OrderByDescending(SpecificityScore)
                .ThenByDescending(r => r.AgeMinDays ?? int.MinValue)
                .ThenBy(r => r.AgeMaxDays ?? int.MaxValue)
                .First();

            string display = !string.IsNullOrWhiteSpace(best.ReferenceText)
                ? best.ReferenceText
                : FormatNumeric(best);

            return new ReferenceRangeResolution
            {
                Range = best,
                DisplayText = string.IsNullOrWhiteSpace(display) ? "Reference Range Not Configured" : display,
                Unit = best.Unit ?? ""
            };
        }

        public static ReferenceRangeResolution ResolveByTestName(string testName, Patient patient, DateTime asOf)
        {
            var def = DatabaseHelper.GetTestDefinitionByName(testName);
            if (def == null)
            {
                return new ReferenceRangeResolution
                {
                    DisplayText = "Reference Range Not Configured"
                };
            }

            var resolution = Resolve(def.Id, patient, asOf);
            if (!string.IsNullOrWhiteSpace(resolution.Unit) || string.IsNullOrWhiteSpace(def.Unit))
                return resolution;

            return new ReferenceRangeResolution
            {
                Range = resolution.Range,
                DisplayText = resolution.DisplayText,
                Unit = def.Unit,
                RequiresPregnancyWarning = resolution.RequiresPregnancyWarning,
                WarningMessage = resolution.WarningMessage
            };
        }

        private static bool IsEffective(TestReferenceRange r, DateTime asOf)
        {
            if (r.EffectiveFrom.HasValue && asOf.Date < r.EffectiveFrom.Value.Date) return false;
            if (r.EffectiveTo.HasValue && asOf.Date > r.EffectiveTo.Value.Date) return false;
            return true;
        }

        private static bool AgeMatches(TestReferenceRange r, int ageDays)
        {
            int min = r.AgeMinDays ?? 0;
            if (ageDays < min) return false;
            if (r.AgeMaxDays.HasValue && ageDays > r.AgeMaxDays.Value) return false;
            return true;
        }

        private static bool GenderMatches(TestReferenceRange r, string gender)
        {
            if (string.Equals(r.Gender, RangeGenders.Both, StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(r.Gender, gender, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PregnancyMatches(TestReferenceRange r, string pregnancy, string gender)
        {
            if (string.Equals(r.PregnancyStatus, PregnancyStatuses.NotApplicable, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase))
                return false;

            return string.Equals(r.PregnancyStatus, pregnancy, StringComparison.OrdinalIgnoreCase);
        }

        private static int SpecificityScore(TestReferenceRange r)
        {
            int score = 0;
            if (!string.Equals(r.PregnancyStatus, PregnancyStatuses.NotApplicable, StringComparison.OrdinalIgnoreCase))
                score += 4;
            if (!string.Equals(r.Gender, RangeGenders.Both, StringComparison.OrdinalIgnoreCase))
                score += 2;
            if ((r.AgeMinDays ?? 0) > 0 || r.AgeMaxDays.HasValue)
                score += 1;
            return score;
        }

        private static string NormalizeGender(string gender)
        {
            if (string.Equals(gender, "Male", StringComparison.OrdinalIgnoreCase)) return "Male";
            if (string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase)) return "Female";
            return gender ?? "";
        }

        private static string FormatNumeric(TestReferenceRange r)
        {
            if (r.MinValue.HasValue && r.MaxValue.HasValue)
                return $"{Trim(r.MinValue.Value)}-{Trim(r.MaxValue.Value)}";
            if (r.MinValue.HasValue)
                return $"≥ {Trim(r.MinValue.Value)}";
            if (r.MaxValue.HasValue)
                return $"≤ {Trim(r.MaxValue.Value)}";
            return "";
        }

        private static string Trim(double v) =>
            Math.Abs(v % 1) < 0.0000001 ? ((int)v).ToString() : v.ToString("0.##");
    }
}
