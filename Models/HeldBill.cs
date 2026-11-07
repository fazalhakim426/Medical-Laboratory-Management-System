using System;
using System.Collections.Generic;

namespace LabReportApp.Models
{
    /// <summary>
    /// In-progress bill parked so the attendant can handle an emergency bill first.
    /// Not a real visit yet — only becomes a Patients/TestResults visit on Generate Bill.
    /// </summary>
    public class HeldBill
    {
        public int Id { get; set; }
        public int? ProfileId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string MrNumber { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Cnic { get; set; } = string.Empty;
        public double AgeValue { get; set; }
        public string AgeUnit { get; set; } = AgeUnits.Years;
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string PregnancyStatus { get; set; } = PregnancyStatuses.NotApplicable;
        public string ReferredBy { get; set; } = string.Empty;
        public string LabName { get; set; } = string.Empty;
        public decimal OverallDiscount { get; set; }
        public string Note { get; set; } = string.Empty;
        public string TestSummary { get; set; } = string.Empty;
        public DateTime HeldAt { get; set; } = DateTime.Now;
        public List<TestResultItem> Items { get; set; } = new();

        public string HeldAtDisplay => HeldAt.ToString("dd-MMM-yyyy HH:mm");
        public string PatientDisplay =>
            string.IsNullOrWhiteSpace(MrNumber) ? PatientName : $"{PatientName} ({MrNumber})";
    }
}
