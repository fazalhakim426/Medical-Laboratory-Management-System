using System.Windows;
using System.Windows.Controls;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class ManageDoctorsWindow : Window
    {
        private int _editingId;

        public ManageDoctorsWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            LoadGrid();
        }

        private void LoadGrid()
        {
            dgDoctors.ItemsSource = DatabaseHelper.GetDoctors(activeOnly: false);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Doctor name is required.", "Missing Information",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                DatabaseHelper.SaveDoctor(new Doctor
                {
                    Id = _editingId,
                    Name = txtName.Text.Trim(),
                    Specialty = txtSpecialty.Text.Trim(),
                    Phone = txtPhone.Text.Trim(),
                    Clinic = txtClinic.Text.Trim(),
                    IsActive = true
                });
                LoadGrid();
                ClearForm();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Could not save doctor.\n" + ex.Message, "Save Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void ClearForm()
        {
            _editingId = 0;
            txtName.Text = "";
            txtSpecialty.Text = "";
            txtPhone.Text = "";
            txtClinic.Text = "";
            dgDoctors.SelectedItem = null;
        }

        private void dgDoctors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgDoctors.SelectedItem is not Doctor selected) return;
            _editingId = selected.Id;
            txtName.Text = selected.Name;
            txtSpecialty.Text = selected.Specialty;
            txtPhone.Text = selected.Phone;
            txtClinic.Text = selected.Clinic;
        }

        private void btnDeactivate_Click(object sender, RoutedEventArgs e)
        {
            if (dgDoctors.SelectedItem is not Doctor selected)
            {
                MessageBox.Show("Select a doctor first.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            selected.IsActive = false;
            DatabaseHelper.SaveDoctor(selected);
            LoadGrid();
            ClearForm();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
