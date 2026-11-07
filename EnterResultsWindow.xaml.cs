using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LabReportApp.Config;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class EnterResultsWindow : Window
    {
        private readonly Patient _patient;
        private readonly ObservableCollection<TestResultItem> _results = new();

        public EnterResultsWindow(int patientId)
        {
            InitializeComponent();
            dgResults.AddHandler(PreviewKeyDownEvent, new KeyEventHandler(dgResults_PreviewKeyDown), handledEventsToo: true);
            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");

            var loaded = DatabaseHelper.GetPatientById(patientId);
            if (loaded == null)
            {
                MessageBox.Show("This patient could not be found (it may have been removed).",
                    "Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                _patient = new Patient();
                Loaded += (_, _) => Close();
                return;
            }

            _patient = loaded;

            txtPatientSummary.Text =
                $"{_patient.SerialNumber}  |  {_patient.Name}  |  {_patient.PhoneNumber}" +
                (string.IsNullOrWhiteSpace(_patient.Cnic) ? "" : $"  |  CNIC {_patient.Cnic}");

            var sampleAt = _patient.SampleCollectedAt == default ? _patient.CreatedAt : _patient.SampleCollectedAt;
            txtDemographics.Text =
                $"Age: {_patient.AgeDisplay}   ·   Gender: {_patient.Gender}   ·   Pregnancy: {_patient.PregnancyStatus}   ·   Sample: {sampleAt:dd-MMM-yyyy hh:mm tt}";

            foreach (var row in DatabaseHelper.GetResultsForPatient(_patient.Id).Where(r => r.IsResultRow))
            {
                if (string.IsNullOrWhiteSpace(row.AbnormalFlag))
                    row.RecalculateStatus();
                _results.Add(row);
            }

            dgResults.ItemsSource = _results;
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => FocusResult(0), DispatcherPriority.ContextIdle);
        }

        private void ResultInput_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox box || box.IsKeyboardFocusWithin)
                return;

            box.Focus();
            e.Handled = true;
        }

        private void dgResults_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Tab)
                return;

            var box = FocusedResultBox();
            if (box?.DataContext is not TestResultItem row)
                return;

            int index = _results.IndexOf(row);
            if (index < 0)
                return;

            bool backward = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            int next = backward ? index - 1 : index + 1;
            if (next < 0)
                return;

            e.Handled = true;
            if (next >= _results.Count)
            {
                btnSaveResults.Focus();
                return;
            }

            FocusResult(next);
        }

        private TextBox? FocusedResultBox()
        {
            if (Keyboard.FocusedElement is TextBox direct && Equals(direct.Tag, "ResultInput"))
                return direct;

            if (Keyboard.FocusedElement is not DependencyObject focused)
                return null;

            var cell = FindAncestor<DataGridCell>(focused);
            if (cell == null)
                return null;

            return FindVisualChildren<TextBox>(cell).FirstOrDefault(b => Equals(b.Tag, "ResultInput"));
        }

        private void FocusResult(int index, bool allowRetry = true)
        {
            if (index < 0 || index >= _results.Count || dgResults == null)
                return;

            var item = _results[index];
            dgResults.SelectedItem = item;
            dgResults.ScrollIntoView(item);
            dgResults.UpdateLayout();

            Dispatcher.BeginInvoke(() =>
            {
                var input = FindVisualChildren<TextBox>(dgResults)
                    .FirstOrDefault(b => Equals(b.Tag, "ResultInput") && ReferenceEquals(b.DataContext, item));
                if (input == null)
                {
                    if (allowRetry)
                        Dispatcher.BeginInvoke(() => FocusResult(index, false), DispatcherPriority.ApplicationIdle);
                    return;
                }

                input.Focus();
                Keyboard.Focus(input);
                input.SelectAll();
            }, DispatcherPriority.Loaded);
        }

        private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            var current = child;
            while (current != null)
            {
                if (current is T match)
                    return match;
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    yield return match;
                foreach (var nested in FindVisualChildren<T>(child))
                    yield return nested;
            }
        }

        private void btnSaveResults_Click(object sender, RoutedEventArgs e)
        {
            var missing = _results.Where(r => string.IsNullOrWhiteSpace(r.Result)).ToList();
            if (missing.Count > 0)
            {
                var confirm = MessageBox.Show(
                    $"{missing.Count} test(s) still have no result entered. Save and send anyway?",
                    "Missing Results", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            btnSaveResults.IsEnabled = false;
            txtStatus.Text = "Saving results...";

            try
            {
                foreach (var row in _results)
                    row.RecalculateStatus();

                DatabaseHelper.SaveResultsAndComplete(_patient.Id, _results.ToList());

                string reportUrl = AppSettings.ReportsBaseUrl.TrimEnd('/') + "/" + _patient.SerialNumber + ".pdf";
                byte[] qrBytes = QrCodeService.GenerateQrPng(reportUrl);

                txtStatus.Text = "Generating PDF report...";
                string localPdfPath = PdfReportService.GenerateReport(_patient, _results.ToList(), qrBytes);

                try
                {
                    BackupService.BackupDatabase(out _);
                    BackupService.BackupPdfReport(localPdfPath);
                }
                catch
                {
                    // Backups must never block the completion flow.
                }

                txtStatus.Text = "Uploading report online...";
                bool uploaded = FtpUploadService.UploadReport(localPdfPath, _patient.SerialNumber + ".pdf", out string ftpError);

                string statusMsg = $"Report for {_patient.SerialNumber} completed and saved at:\n{localPdfPath}\n";
                statusMsg += uploaded
                    ? "Uploaded online successfully. The QR code on the bill/report will now work."
                    : $"Upload FAILED ({ftpError}). Report is only saved locally.";
                txtStatus.Text = statusMsg;

                var result = MessageBox.Show(
                    $"Report for {_patient.SerialNumber} has been completed.\n\nOpen the PDF now?",
                    "Success", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(localPdfPath) { UseShellExecute = true });
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Failed: " + ex.Message;
            }
            finally
            {
                btnSaveResults.IsEnabled = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
