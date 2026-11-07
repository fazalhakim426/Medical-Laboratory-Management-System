using System.Linq;

namespace LabReportApp.Services
{
    /// <summary>Pakistan CNIC: 13 digits, stored as 12345-1234567-1.</summary>
    public static class Cnic
    {
        public static bool TryNormalize(string? raw, out string digits, out string formatted, out string error)
        {
            digits = "";
            formatted = "";
            error = "";

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "CNIC is required.";
                return false;
            }

            digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length != 13)
            {
                error = "CNIC must be 13 digits, for example 12345-1234567-1.";
                return false;
            }

            formatted = $"{digits[..5]}-{digits.Substring(5, 7)}-{digits[12]}";
            return true;
        }
    }
}
