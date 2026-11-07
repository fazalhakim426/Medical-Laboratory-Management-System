using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class TestDefinitionsWindow : Window
    {
        private readonly List<TestDefinition> _all = new();
        private int _editingId;
        private bool _loadingFilters = true;

        public TestDefinitionsWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            LoadGrid();
        }

        private void LoadGrid()
        {
            _all.Clear();
            _all.AddRange(DatabaseHelper.GetAllTestDefinitions());

            string? keepCategory = cmbCategory.SelectedItem as string;
            _loadingFilters = true;
            cmbCategory.Items.Clear();
            cmbCategory.Items.Add("All categories");
            foreach (var category in _all
                         .Select(t => t.CategoryHeading)
                         .Where(c => !string.IsNullOrWhiteSpace(c))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(c => c))
            {
                cmbCategory.Items.Add(category);
            }

            cmbCategory.SelectedItem = keepCategory != null && cmbCategory.Items.Contains(keepCategory)
                ? keepCategory
                : "All categories";
            if (cmbKind.SelectedIndex < 0) cmbKind.SelectedIndex = 0;
            if (cmbStatus.SelectedIndex < 0) cmbStatus.SelectedIndex = 0;
            _loadingFilters = false;
            ApplyFilter();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            if (_loadingFilters || txtSearch == null || dgDefinitions == null)
                return;

            string term = txtSearch.Text.Trim();
            string category = cmbCategory.SelectedItem as string ?? "All categories";
            string kind = (cmbKind.SelectedItem as ComboBoxItem)?.Content as string ?? "All types";
            string status = (cmbStatus.SelectedItem as ComboBoxItem)?.Content as string ?? "All statuses";

            IEnumerable<TestDefinition> query = _all;
            if (term.Length > 0)
            {
                query = query.Where(t =>
                    t.TestName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || t.CategoryHeading.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (t.Unit ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (t.NormalRange ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(category, "All categories", StringComparison.Ordinal))
                query = query.Where(t => string.Equals(t.CategoryHeading, category, StringComparison.OrdinalIgnoreCase));

            if (kind == "Individual tests")
                query = query.Where(t => !t.IsPanel);
            else if (kind == "Panels")
                query = query.Where(t => t.IsPanel);

            if (status == "Active")
                query = query.Where(t => t.IsActive);
            else if (status == "Inactive")
                query = query.Where(t => !t.IsActive);

            var list = query.ToList();
            dgDefinitions.ItemsSource = list;
            txtCount.Text = list.Count == _all.Count
                ? $"{list.Count} tests"
                : $"{list.Count} of {_all.Count}";
            txtEmpty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e) => OpenEditor(null);

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgDefinitions.SelectedItem is not TestDefinition selected)
            {
                MessageBox.Show("Select a test to edit.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenEditor(selected);
        }

        private void dgDefinitions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FindParent<DataGridRow>(e.OriginalSource as DependencyObject) == null)
                return;
            if (dgDefinitions.SelectedItem is TestDefinition selected)
                OpenEditor(selected);
        }

        private void OpenEditor(TestDefinition? test)
        {
            if (test == null)
            {
                _editingId = 0;
                txtEditorTitle.Text = "Add Test";
                txtCategory.Text = "";
                txtTestName.Text = "";
                txtUnit.Text = "";
                txtNormalRange.Text = "";
                txtPrice.Text = "";
                chkIsPanel.IsChecked = false;
            }
            else
            {
                _editingId = test.Id;
                txtEditorTitle.Text = "Edit Test";
                txtCategory.Text = test.CategoryHeading;
                txtTestName.Text = test.TestName;
                txtUnit.Text = test.Unit;
                txtNormalRange.Text = test.NormalRange;
                txtPrice.Text = test.Price.ToString("0.##");
                chkIsPanel.IsChecked = test.IsPanel;
            }

            pnlEditor.Visibility = Visibility.Visible;
            txtCategory.Focus();
        }

        private void CloseEditor() => pnlEditor.Visibility = Visibility.Collapsed;

        private void btnCancelEditor_Click(object sender, RoutedEventArgs e) => CloseEditor();

        private void Backdrop_MouseDown(object sender, MouseButtonEventArgs e) => CloseEditor();

        private void EditorCard_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && pnlEditor.Visibility == Visibility.Visible)
            {
                CloseEditor();
                e.Handled = true;
            }
        }

        private void btnSaveDefinition_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCategory.Text) || string.IsNullOrWhiteSpace(txtTestName.Text))
            {
                MessageBox.Show("Category and Test Name are required.", "Missing Information",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtPrice.Text.Trim(), out decimal price) || price < 0)
            {
                MessageBox.Show("Please enter a valid Price in PKR (0 or more).", "Invalid Price",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string savedName = txtTestName.Text.Trim();
            var definition = new TestDefinition
            {
                Id = _editingId,
                CategoryHeading = txtCategory.Text.Trim(),
                TestName = savedName,
                Unit = txtUnit.Text.Trim(),
                NormalRange = txtNormalRange.Text.Trim(),
                Price = price,
                IsPanel = chkIsPanel.IsChecked == true,
                IsActive = true
            };

            DatabaseHelper.InsertOrUpdateTestDefinition(definition);
            CloseEditor();
            LoadGrid();

            var match = dgDefinitions.Items.OfType<TestDefinition>()
                .FirstOrDefault(t => t.TestName.Equals(savedName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                dgDefinitions.SelectedItem = match;
                dgDefinitions.ScrollIntoView(match);
            }
        }

        private void btnManageRanges_Click(object sender, RoutedEventArgs e)
        {
            if (dgDefinitions.SelectedItem is not TestDefinition selected || selected.Id == 0)
            {
                MessageBox.Show("Select a saved test first, then open Reference Ranges.",
                    "No Test Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selected.IsPanel)
            {
                MessageBox.Show("Panels don't have their own ranges — configure ranges on each member parameter instead.",
                    "Panel Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new ReferenceRangesWindow(selected) { Owner = this };
            win.ShowDialog();
        }

        private void btnDeleteDefinition_Click(object sender, RoutedEventArgs e)
        {
            if (dgDefinitions.SelectedItem is not TestDefinition selected)
            {
                MessageBox.Show("Please select a test to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"Delete test '{selected.TestName}' and its reference ranges?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            DatabaseHelper.DeleteTestDefinition(selected.Id);
            LoadGrid();
        }

        private void btnCloseWindow_Click(object sender, RoutedEventArgs e) => Close();

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T match)
                    return match;
                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }
    }
}
