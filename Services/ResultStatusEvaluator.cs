using System;
using System.Globalization;
using System.Text.RegularExpressions;
using LabReportApp.Models;

namespace LabReportApp.Services
{
    public static class ResultStatusEvaluator
    {
        public const string Low = "LOW";
        public const string High = "HIGH";
        public const string Normal = "NORMAL";
        public const string NotConfigured = "NOT CONFIGURED";
        public const string Qualitative = "QUALITATIVE";

        /// <summary>
        /// Evaluates a technician-entered result against a snapshotted/resolved reference range.
        /// Never invents NORMAL when the range is missing. Never alters the result value.
        /// </summary>
        public static string Evaluate(TestResultItem row)
        {
            if (string.Equals(row.NormalRange, "Reference Range Not Configured", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(row.NormalRange))
            {
                return NotConfigured;
            }

            // Qualitative: no numeric bounds stored on the snapshot.
            if (row.IsQualitative || (!row.RangeMinValue.HasValue && !row.RangeMaxValue.HasValue))
            {
                // If the display text looks purely qualitative, don't force LOW/HIGH.
                if (row.IsQualitative || LooksQualitative(row.NormalRange))
                    return Qualitative;
            }

            if (!TryParseResultNumber(row.Result, out double value))
                return string.IsNullOrWhiteSpace(row.Result) ? "" : Qualitative;

            double? min = row.RangeMinValue;
            double? max = row.RangeMaxValue;

            // Fallback: try to parse "a-b" from the snapshotted display text when numeric bounds
            // were not stored (legacy rows).
            if (!min.HasValue && !max.HasValue)
                TryParseRangeText(row.NormalRange, out min, out max);

            if (!min.HasValue && !max.HasValue)
                return Qualitative;

            if (min.HasValue && value < min.Value) return Low;
            if (max.HasValue && value > max.Value) return High;
            return Normal;
        }

        public static bool LooksQualitative(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            string[] tokens =
            {
                "Negative", "Positive", "Non-reactive", "Non-Reactive", "Reactive",
                "Not seen", "Not Seen", "Absent", "Clear", "Brown", "Formed", "Pale yellow"
            };
            foreach (var token in tokens)
            {
                if (t.Equals(token, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            // No digit → treat as qualitative display text.
            return !Regex.IsMatch(t, @"\d");
        }

        public static bool TryParseResultNumber(string? result, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(result)) return false;

            // Strip thousand separators and common suffixes technicians type.
            string cleaned = result.Trim().Replace(",", "");
            cleaned = Regex.Replace(cleaned, @"[^\d\.\-]", "");
            return double.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseRangeText(string? text, out double? min, out double? max)
        {
            min = null;
            max = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Patterns: "13.5-17.5", "70-99", "<140", ">40"
            var between = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*[-–—]\s*(\d+(?:\.\d+)?)");
            if (between.Success)
            {
                min = double.Parse(between.Groups[1].Value, CultureInfo.InvariantCulture);
                max = double.Parse(between.Groups[2].Value, CultureInfo.InvariantCulture);
                return true;
            }

            var lt = Regex.Match(text, @"[<>≤≥]\s*(\d+(?:\.\d+)?)");
            if (lt.Success)
            {
                double v = double.Parse(lt.Groups[1].Value, CultureInfo.InvariantCulture);
                if (text.Contains('>') || text.Contains('≥')) min = v;
                else max = v;
                return true;
            }

            return false;
        }
    }
}
