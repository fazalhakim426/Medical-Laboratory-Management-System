using System;
using System.Globalization;
using LabReportApp.Models;

namespace LabReportApp.Services
{
    public static class AbnormalFlagCalculator
    {
        /// <summary>
        /// Computes LOW / HIGH / NORMAL for numeric results. Qualitative or unconfigured
        /// ranges never invent a NORMAL flag.
        /// </summary>
        public static string Calculate(string resultText, TestReferenceRange? range, bool rangeConfigured)
        {
            if (!rangeConfigured || range == null)
                return AbnormalFlags.NotConfigured;

            if (range.IsQualitative)
                return AbnormalFlags.Qualitative;

            if (!TryParseResult(resultText, out double value))
                return string.Empty;

            if (range.MinValue.HasValue && value < range.MinValue.Value)
                return AbnormalFlags.Low;

            if (range.MaxValue.HasValue && value > range.MaxValue.Value)
                return AbnormalFlags.High;

            if (range.MinValue.HasValue || range.MaxValue.HasValue)
                return AbnormalFlags.Normal;

            return AbnormalFlags.NotConfigured;
        }

        public static bool TryParseResult(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string cleaned = text.Trim()
                .Replace(",", "", StringComparison.Ordinal)
                .Replace(" ", "", StringComparison.Ordinal);

            // Strip common qualitative prefixes technicians might type with numbers.
            foreach (var prefix in new[] { "<", ">", "<=", ">=", "=" })
            {
                if (cleaned.StartsWith(prefix, StringComparison.Ordinal))
                    cleaned = cleaned[prefix.Length..];
            }

            return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(cleaned, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }
    }
}