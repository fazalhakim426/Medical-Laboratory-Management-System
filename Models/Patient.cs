using System;
using LabReportApp.Services;

namespace LabReportApp.Models
{
    public class Patient
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>Legacy whole-years field for older rows / displays.</summary>
        public int Age { get; set; }

        public double AgeValue { get; set; }
        public string AgeUnit { get; set; } = AgeUnits.Years;
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string PregnancyStatus { get; set; } = PregnancyStatuses.NotApplicable;

        public string ReferredBy { get; set; } = string.Empty;
        public string LabName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>National identity number snapshotted on the visit. Unique on the patient profile.</summary>
        public string Cnic { get; set; } = string.Empty;

        public string ReportHeading { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Sample / registration timestamp used for age-at-test calculations.
        /// Frozen at billing so reopening old reports does not recompute age.
        /// </summary>
        public DateTime SampleCollectedAt { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Pending";
        public decimal TotalAmount { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal NetAmount { get; set; }

        /// <summary>Optional link to PatientProfiles registry.</summary>
        public int? ProfileId { get; set; }

        /// <summary>NotPrinted | Printed</summary>
        public string PrintStatus { get; set; } = "NotPrinted";

        public string AgeDisplay => AgeCalculator.FormatAgeDisplay(this);
    }
}
