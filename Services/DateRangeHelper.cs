using System;

namespace LabReportApp.Services
{
    public enum DatePreset
    {
        Yesterday,
        Today,
        ThisWeek,
        ThisMonth,
        Custom
    }

    public static class DateRangeHelper
    {
        public static (DateTime From, DateTime To) Resolve(DatePreset preset, DateTime? customFrom = null, DateTime? customTo = null)
        {
            var today = DateTime.Today;
            return preset switch
            {
                DatePreset.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
                DatePreset.Today => (today, today),
                DatePreset.ThisWeek => (StartOfWeek(today), today),
                DatePreset.ThisMonth => (new DateTime(today.Year, today.Month, 1), today),
                DatePreset.Custom => (
                    (customFrom ?? today).Date,
                    (customTo ?? customFrom ?? today).Date),
                _ => (today, today)
            };
        }

        /// <summary>Week starts Monday (lab week convention).</summary>
        public static DateTime StartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }
    }
}
