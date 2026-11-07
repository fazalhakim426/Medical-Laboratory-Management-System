using System;
using LabReportApp.Models;

namespace LabReportApp.Services
{
    public static class AgeCalculator
    {
        public const string UnitDays = AgeUnits.Days;
        public const string UnitMonths = AgeUnits.Months;
        public const string UnitYears = AgeUnits.Years;

        public static int ToDays(double ageValue, string ageUnit)
        {
            if (ageValue < 0) ageValue = 0;

            return ageUnit switch
            {
                AgeUnits.Days => (int)Math.Round(ageValue),
                AgeUnits.Months => (int)Math.Round(ageValue * 30.0),
                _ => (int)Math.Round(ageValue * 365.0)
            };
        }

        public static int GetAgeInDays(Patient patient, DateTime asOf)
        {
            if (patient.DateOfBirth.HasValue)
            {
                var dob = patient.DateOfBirth.Value.Date;
                if (dob > asOf.Date) return 0;
                return (int)(asOf.Date - dob).TotalDays;
            }

            return ToDays(patient.AgeValue, string.IsNullOrWhiteSpace(patient.AgeUnit) ? AgeUnits.Years : patient.AgeUnit);
        }

        public static (double value, string unit) FromDateOfBirth(DateTime dateOfBirth, DateTime asOf)
        {
            if (dateOfBirth.Date > asOf.Date)
                return (0, AgeUnits.Days);

            int totalDays = (int)(asOf.Date - dateOfBirth.Date).TotalDays;
            if (totalDays < 60)
                return (totalDays, AgeUnits.Days);

            int totalMonths = ((asOf.Year - dateOfBirth.Year) * 12) + asOf.Month - dateOfBirth.Month;
            if (asOf.Day < dateOfBirth.Day) totalMonths--;
            if (totalMonths < 0) totalMonths = 0;

            if (totalMonths < 24)
                return (totalMonths, AgeUnits.Months);

            double years = totalDays / 365.0;
            return (Math.Floor(years), AgeUnits.Years);
        }

        public static string FormatAgeDisplay(Patient patient)
        {
            string unit = string.IsNullOrWhiteSpace(patient.AgeUnit) ? AgeUnits.Years : patient.AgeUnit;
            string valueText = Math.Abs(patient.AgeValue % 1) < 0.0000001
                ? ((int)patient.AgeValue).ToString()
                : patient.AgeValue.ToString("0.##");

            string shortUnit = unit switch
            {
                AgeUnits.Days => "d",
                AgeUnits.Months => "mo",
                _ => "y"
            };

            return $"{valueText} {shortUnit}";
        }
    }
}
