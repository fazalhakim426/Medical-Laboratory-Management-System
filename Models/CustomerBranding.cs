using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LabReportApp.Config;

namespace LabReportApp.Models
{
    /// <summary>
    /// Hospital or laboratory identity printed on that customer's documents.
    /// This never replaces the Lab ProX product name or WhatsApp number.
    /// </summary>
    public class CustomerBranding
    {
        public string HospitalName { get; set; } = "";
        public string LogoFileName { get; set; } = "";
        public string Tagline { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Website { get; set; } = "";
        public string RegistrationNumber { get; set; } = "";
        public string WorkingHours { get; set; } = "";
        public string AdditionalInfo { get; set; } = "";

        public string ResolvedHospitalName(Patient? patient)
        {
            if (!string.IsNullOrWhiteSpace(HospitalName))
                return HospitalName.Trim();
            if (patient != null && !string.IsNullOrWhiteSpace(patient.LabName))
                return patient.LabName.Trim();
            if (!IsPlaceholder(AppSettings.LabName))
                return AppSettings.LabName.Trim();
            return "";
        }

        public string ResolvedAddress()
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(Address))
                lines.Add(Address.Trim());
            if (!string.IsNullOrWhiteSpace(City))
            {
                string city = City.Trim();
                bool already = lines.Exists(line => line.Contains(city, StringComparison.OrdinalIgnoreCase));
                if (!already)
                    lines.Add(city);
            }

            if (lines.Count > 0)
                return string.Join("\n", lines);
            if (!IsPlaceholder(AppSettings.LabAddress))
                return AppSettings.LabAddress.Trim();
            return "";
        }

        public string ResolvedContact()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Phone)) parts.Add(Phone.Trim());
            if (!string.IsNullOrWhiteSpace(Email)) parts.Add(Email.Trim());
            if (!string.IsNullOrWhiteSpace(Website)) parts.Add(Website.Trim());
            if (parts.Count > 0)
                return string.Join("   |   ", parts);
            if (!IsPlaceholder(AppSettings.LabContact))
                return AppSettings.LabContact.Trim();
            return "";
        }

        public void Normalize()
        {
            HospitalName = Clip(HospitalName, 160);
            LogoFileName = string.IsNullOrWhiteSpace(LogoFileName) ? "" : Path.GetFileName(LogoFileName.Trim());
            Tagline = Clip(Tagline, 200);
            Address = Clip(Address, 500);
            City = Clip(City, 80);
            Phone = Clip(Phone, 80);
            Email = Clip(Email, 120);
            Website = Clip(Website, 160);
            RegistrationNumber = Clip(RegistrationNumber, 80);
            WorkingHours = Clip(WorkingHours, 160);
            AdditionalInfo = Clip(AdditionalInfo, 500);
        }

        public CustomerBranding Clone()
        {
            string json = JsonSerializer.Serialize(this, LabProXJson.Options);
            return JsonSerializer.Deserialize<CustomerBranding>(json, LabProXJson.Options) ?? new CustomerBranding();
        }

        private static string Clip(string? value, int max)
        {
            value = value?.Trim() ?? "";
            return value.Length <= max ? value : value[..max];
        }

        private static bool IsPlaceholder(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;
            return value.Contains("Your LAB", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("Your Lab", StringComparison.OrdinalIgnoreCase);
        }
    }
}
