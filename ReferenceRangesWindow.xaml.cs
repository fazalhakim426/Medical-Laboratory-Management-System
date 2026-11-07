using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class ReferenceRangesWindow : Window
    {
        private readonly int _testDefinitionId;
        private int _editingId;

        public ReferenceRangesWindow(TestDefinition test)
        {
            InitializeComponent();
            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");

            _testDefinitionId = test.Id;
            txtHeader.Text = $"Reference Ranges — {test.TestName}";
            txtUnit.Text = test.Unit;
            txtMethodology.Text = DatabaseHelper.IllustrativeMethodology;
            LoadGrid();
        }

        private void LoadGrid()
        {
            dgRanges.ItemsSource = DatabaseHelper.GetReferenceRangesForTest(_testDefinitionId);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            var range = new TestReferenceRange
            {
                Id = _editingId,
                TestDefinitionId = _testDefinitionId,
                AgeMinDays = ParseNullableInt(txtAgeMin.Text) ?? 0,
                AgeMaxDays = ParseNullableInt(txtAgeMax.Text),
                Gender = (cmbGender.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? RangeGenders.Both,
                PregnancyStatus = (cmbPregnancy.SelectedItem as ComboBoxItem)?.Content?.ToString()
                    ?? PregnancyStatuses.NotApplicable,
                MinValue = ParseNullableDouble(txtMinValue.Text),
                MaxValue = ParseNullableDouble(txtMaxValue.Text),
                ReferenceText = txtReferenceText.Text.Trim(),
                Unit = txtUnit.Text.Trim(),
                Methodology = txtMethodology.Text.Trim(),
                IsActive = chkActive.IsChecked == true,
                IsIllustrative = true
            };

            if (string.IsNullOrWhiteSpace(range.ReferenceText) && !range.MinValue.HasValue && !range.MaxValue.HasValue)
            {
                MessageBox.Show("Enter reference text and/or numeric min/max values.", "Missing Range",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DatabaseHelper.SaveReferenceRange(range);
            LoadGrid();
            ClearForm();
        }

        private void btnClear_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void ClearForm()
        {
            _editingId = 0;
            txtAgeMin.Text = "";
            txtAgeMax.Text = "";
            cmbGender.SelectedIndex = 0;
            cmbPregnancy.SelectedIndex = 0;
            txtMinValue.Text = "";
            txtMaxValue.Text = "";
            txtReferenceText.Text = "";
            chkActive.IsChecked = true;
            dgRanges.SelectedItem = null;
        }

        private void dgRanges_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgRanges.SelectedItem is not TestReferenceRange selected) return;

            _editingId = selected.Id;
            txtAgeMin.Text = selected.AgeMinDays?.ToString() ?? "0";
            txtAgeMax.Text = selected.AgeMaxDays?.ToString() ?? "";
            SelectCombo(cmbGender, selected.Gender);
            SelectCombo(cmbPregnancy, selected.PregnancyStatus);
            txtMinValue.Text = selected.MinValue?.ToString(CultureInfo.InvariantCulture) ?? "";
            txtMaxValue.Text = selected.MaxValue?.ToString(CultureInfo.InvariantCulture) ?? "";
            txtReferenceText.Text = selected.ReferenceText;
            txtUnit.Text = selected.Unit;
            txtMethodology.Text = selected.Methodology;
            chkActive.IsChecked = selected.IsActive;
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgRanges.SelectedItem is not TestReferenceRange selected)
            {
                MessageBox.Show("Select a range to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Delete range '{selected.ReferenceText}' ({selected.AgeGroupLabel})?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            DatabaseHelper.DeleteReferenceRange(selected.Id);
            LoadGrid();
            ClearForm();
        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import reference ranges CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                DatabaseHelper.ImportReferenceRangesFromCsv(dialog.FileName);
                MessageBox.Show("Import complete.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed:\n{ex.Message}", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export reference ranges",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "reference_ranges_export.csv"
            };
            if (dialog.ShowDialog() != true) return;

            DatabaseHelper.ExportReferenceRangesToCsv(dialog.FileName);
            MessageBox.Show($"Exported to:\n{dialog.FileName}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();

        private static void SelectCombo(ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            combo.SelectedIndex = 0;
        }

        private static int? ParseNullableInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            return int.TryParse(text.Trim(), out int v) ? v : null;
        }

        private static double? ParseNullableDouble(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            return double.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out double v) ? v : null;
        }
    }
}
