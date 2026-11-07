using System;

namespace LabReportApp.Models
{
    public class Doctor
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Clinic { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string DisplayLabel =>
            string.IsNullOrWhiteSpace(Specialty) ? Name : $"{Name}  ·  {Specialty}";

        public override string ToString() => Name ?? string.Empty;
    }
}
