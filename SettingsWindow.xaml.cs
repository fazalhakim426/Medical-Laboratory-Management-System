using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using LabReportApp.Config;
using LabReportApp.Models;
using LabReportApp.Services;
using Microsoft.Win32;

namespace LabReportApp
{
    public partial class SettingsWindow : Window
    {
        private string _logoFileName = "";

        public SettingsWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            txtBackupFolder.Text = AppSettings.BackupFolder;
            txtBrandNotice.Text =
                LabProXBrand.ProductName + " is the software publisher. The name, logo and WhatsApp " +
                LabProXBrand.WhatsAppDisplay + " stay on the application and on every PDF. The fields below change your hospital identity only.";
            txtLockedFooter.Text = LabProXBrand.FooterText;
            LoadBranding();
        }

        private void LoadBranding()
        {
            var branding = LabProXSettingsStore.Current.Customer;
            txtHospitalName.Text = branding.HospitalName;
            txtTagline.Text = branding.Tagline;
            txtAddress.Text = branding.Address;
            txtCity.Text = branding.City;
            txtPhone.Text = branding.Phone;
            txtEmail.Text = branding.Email;
            txtWebsite.Text = branding.Website;
            txtRegistration.Text = branding.RegistrationNumber;
            txtHours.Text = branding.WorkingHours;
            txtExtra.Text = branding.AdditionalInfo;
            _logoFileName = branding.LogoFileName ?? "";
            ShowLogo();
        }

        private void btnSaveBranding_Click(object sender, RoutedEventArgs e)
        {
            var branding = new CustomerBranding
            {
                HospitalName = txtHospitalName.Text,
                Tagline = txtTagline.Text,
                LogoFileName = _logoFileName,
                Address = txtAddress.Text,
                City = txtCity.Text,
                Phone = txtPhone.Text,
                Email = txtEmail.Text,
                Website = txtWebsite.Text,
                RegistrationNumber = txtRegistration.Text,
                WorkingHours = txtHours.Text,
                AdditionalInfo = txtExtra.Text
            };
            branding.Normalize();
            LabProXSettingsStore.SaveCustomer(branding);
            _logoFileName = branding.LogoFileName;
            ShowLogo();
            if (Owner is MainWindow main)
                main.RefreshHeaderBranding();
            MessageBox.Show(
                "Laboratory identity saved. Bills, receipts and reports will use it. Lab ProX branding is unchanged.",
                LabProXBrand.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnBrowseLogo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select hospital logo",
                Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Branding");
                Directory.CreateDirectory(dir);
                string ext = Path.GetExtension(dialog.FileName);
                if (string.IsNullOrWhiteSpace(ext))
                    ext = ".png";
                string fileName = "hospital-logo" + ext.ToLowerInvariant();
                string dest = Path.Combine(dir, fileName);
                File.Copy(dialog.FileName, dest, true);
                foreach (string old in Directory.GetFiles(dir, "hospital-logo.*"))
                {
                    if (!string.Equals(old, dest, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(old); } catch { /* keep the new file even if an older copy is locked */ }
                    }
                }

                _logoFileName = fileName;
                ShowLogo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not copy the logo.\n" + ex.Message, LabProXBrand.ProductName,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnClearLogo_Click(object sender, RoutedEventArgs e)
        {
            _logoFileName = "";
            ShowLogo();
        }

        private void ShowLogo()
        {
            string? path = LabProXSettingsStore.ResolveLogoPath(new CustomerBranding { LogoFileName = _logoFileName });
            if (path == null)
            {
                imgLogo.Source = null;
                txtNoLogo.Visibility = Visibility.Visible;
                txtLogoFile.Text = "";
                return;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            imgLogo.Source = image;
            txtNoLogo.Visibility = Visibility.Collapsed;
            txtLogoFile.Text = path;
        }

        private void btnBackupNow_Click(object sender, RoutedEventArgs e)
        {
            bool success = BackupService.BackupDatabase(out string message);
            MessageBox.Show(
                success ? message : $"Backup failed.\n{message}",
                success ? "Backup Complete" : "Backup Failed",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void btnRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "This will replace your CURRENT database with the backup file you choose.\n" +
                "Make sure no other computer/attendant is using this app right now.\n\nContinue?",
                "Restore Database", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            var dialog = new OpenFileDialog
            {
                Title = "Select a database backup file (.db)",
                Filter = "Database Backup (*.db)|*.db|All Files (*.*)|*.*",
                InitialDirectory = Directory.Exists(AppSettings.BackupFolder) ? AppSettings.BackupFolder : ""
            };

            if (dialog.ShowDialog() != true) return;

            bool success = BackupService.RestoreDatabase(dialog.FileName, out string message);
            MessageBox.Show(message,
                success ? "Restore Successful" : "Restore Failed",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Error);

            if (!success) return;

            MessageBox.Show(
                "The application will now close. Please reopen it to use the restored data.",
                "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown();
        }
    }
}
