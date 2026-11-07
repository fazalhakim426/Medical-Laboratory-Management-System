using System;
using LabReportApp.Services;

namespace LabReportApp.Models
{
    /// <summary>
    /// Reusable registered patient (demographics). Visits/bills remain in the Patients table.
    /// </summary>
    public class PatientProfile
    {
        public int Id { get; set; }
        public string MrNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>Formatted CNIC, for example 12345-1234567-1. Unique per patient.</summary>
        public string Cnic { get; set; } = string.Empty;

        public double AgeValue { get; set; }
        public string AgeUnit { get; set; } = AgeUnits.Years;
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string PregnancyStatus { get; set; } = PregnancyStatuses.NotApplicable;
        public string ReferredBy { get; set; } = string.Empty;
        public string LabName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public string AgeDisplay => AgeCalculator.FormatAgeDisplay(new Patient
        {
            AgeValue = AgeValue,
            AgeUnit = AgeUnit
        });

        public string DropdownLabel =>
            $"{Name}  ·  {AgeDisplay} / {Gender}  ·  {PhoneNumber}" +
            (string.IsNullOrWhiteSpace(Cnic) ? "" : $"  ·  {Cnic}") +
            $"  ·  {MrNumber}";

        public string SearchBlob =>
            $"{Name} {PhoneNumber} {Cnic} {MrNumber} {Gender} {ReferredBy}".ToLowerInvariant();
    }

    /// <summary>Row model for dashboard pending / print cards.</summary>
    public class VisitDashboardItem
    {
        public int VisitId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string MrNumber { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ReferredBy { get; set; } = string.Empty;
        public string TestSummary { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PrintStatus { get; set; } = "NotPrinted";
        public decimal TotalAmount { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal NetAmount { get; set; }
        public DateTime? RefundedAt { get; set; }

        public string CreatedAtDisplay => CreatedAt.ToString("dd-MMM-yyyy HH:mm");
        public string StatusDisplay => Status switch
        {
            "Pending" => "Pending Results",
            "Completed" => "Completed",
            "Refunded" => "Refunded",
            _ => Status
        };
    }
}
