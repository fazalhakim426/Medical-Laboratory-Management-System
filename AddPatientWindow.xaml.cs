using System;
using System.Windows;
using System.Windows.Controls;
using LabReportApp.Config;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class AddPatientWindow : Window
    {
        public PatientProfile? SavedProfile { get; private set; }

        public AddPatientWindow(PatientProfile? existing = null)
        {
            InitializeComponent();
            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            LoadDoctors();

            if (existing != null)
            {
                txtTitle.Text = "Edit Patient";
                Title = "Edit Patient";
                btnSave.Content = "Update Patient";
                LoadProfile(existing);
            }
        }

        private void LoadDoctors(string? preferName = null)
        {
            var doctors = DatabaseHelper.GetDoctors();
            cmbReferredBy.ItemsSource = doctors;

            string? target = preferName;
            if (string.IsNullOrWhiteSpace(target) && cmbReferredBy.SelectedItem is Doctor current)
                target = current.Name;

            if (!string.IsNullOrWhiteSpace(target))
            {
                foreach (var d in doctors)
                {
                    if (string.Equals(d.Name, target, StringComparison.OrdinalIgnoreCase))
                    {
                        cmbReferredBy.SelectedItem = d;
                        return;
                    }
                }
                cmbReferredBy.Text = target;
            }
        }

        private void LoadProfile(PatientProfile p)
        {
            SavedProfile = p;
            txtName.Text = p.Name;
            txtPhone.Text = p.PhoneNumber;
            txtCnic.Text = p.Cnic;
            txtAge.Text = p.AgeValue % 1 == 0 ? ((int)p.AgeValue).ToString() : p.AgeValue.ToString("0.##");
            SelectCombo(cmbAgeUnit, p.AgeUnit);
            SelectCombo(cmbGender, p.Gender);
            dpDob.SelectedDate = p.DateOfBirth;
            SelectCombo(cmbPregnancy, p.PregnancyStatus);
            LoadDoctors(p.ReferredBy);
            txtLabName.Text = p.LabName;
            bool female = string.Equals(p.Gender, "Female", StringComparison.OrdinalIgnoreCase);
            cmbPregnancy.IsEnabled = female;
        }

        private void btnManageDoctors_Click(object sender, RoutedEventArgs e)
        {
            string? keep = GetSelectedDoctorName();
            new ManageDoctorsWindow { Owner = this }.ShowDialog();
            LoadDoctors(keep);
        }

        private string GetSelectedDoctorName()
        {
            if (cmbReferredBy.SelectedItem is Doctor doc)
                return doc.Name?.Trim() ?? "";
            return cmbReferredBy.Text?.Trim() ?? "";
        }

        private void dpDob_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (dpDob.SelectedDate is not DateTime dob) return;
            var (value, unit) = AgeCalculator.FromDateOfBirth(dob, DateTime.Now);
            txtAge.Text = value % 1 == 0 ? ((int)value).ToString() : value.ToString("0.##");
            SelectCombo(cmbAgeUnit, unit);
        }

        private void cmbGender_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool female = string.Equals(
                (cmbGender.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                "Female", StringComparison.OrdinalIgnoreCase);
            cmbPregnancy.IsEnabled = female;
            if (!female) cmbPregnancy.SelectedIndex = 0;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            txtError.Text = "";

            if (string.IsNullOrWhiteSpace(txtName.Text) ||
                string.IsNullOrWhiteSpace(txtPhone.Text) ||
                string.IsNullOrWhiteSpace(txtCnic.Text) ||
                string.IsNullOrWhiteSpace(txtAge.Text) ||
                cmbGender.SelectedItem == null ||
                cmbAgeUnit.SelectedItem == null)
            {
                txtError.Text = "Please fill Name, Phone, CNIC, Age, Age Unit and Gender.";
                return;
            }

            if (!Cnic.TryNormalize(txtCnic.Text, out _, out string cnic, out string cnicError))
            {
                txtError.Text = cnicError;
                return;
            }

            if (!double.TryParse(txtAge.Text.Trim(), out double ageValue) || ageValue < 0)
            {
                txtError.Text = "Age must be a valid number.";
                return;
            }

            var profile = SavedProfile ?? new PatientProfile();
            profile.Name = txtName.Text.Trim();
            profile.PhoneNumber = txtPhone.Text.Trim();
            profile.Cnic = cnic;
            profile.AgeValue = ageValue;
            profile.AgeUnit = (cmbAgeUnit.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? AgeUnits.Years;
            profile.DateOfBirth = dpDob.SelectedDate;
            profile.Gender = (cmbGender.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            profile.PregnancyStatus = (cmbPregnancy.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? PregnancyStatuses.NotApplicable;
            profile.ReferredBy = GetSelectedDoctorName();
            profile.LabName = string.IsNullOrWhiteSpace(txtLabName.Text)
                ? AppSettings.LabName
                : txtLabName.Text.Trim();

            try
            {
                SavedProfile = DatabaseHelper.SavePatientProfile(profile);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                txtError.Text = "Could not save patient: " + ex.Message;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static void SelectCombo(ComboBox combo, string? value)
        {
            if (combo == null) return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }
    }
}
