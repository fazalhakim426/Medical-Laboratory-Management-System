using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class ManagePanelsWindow : Window
    {
        private readonly ObservableCollection<TestDefinition> _members = new();
        private List<TestDefinition> _individuals = new();
        private int _editingId;
        private bool _loadingSelection;

        public ManagePanelsWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            lstMembers.ItemsSource = _members;
            txtSearch.Text = "";
            LoadIndividuals();
            LoadGrid();
            RefreshAvailable();
        }

        private void LoadIndividuals()
        {
            _individuals = DatabaseHelper.GetAllTestDefinitions()
                .Where(t => t.IsActive && !t.IsPanel)
                .OrderBy(t => t.TestName)
                .ToList();
        }

        private void LoadGrid()
        {
            var rows = new List<PanelRow>();
            foreach (var panel in DatabaseHelper.GetAllTestDefinitions().Where(t => t.IsPanel).OrderBy(t => t.TestName))
            {
                int count = DatabaseHelper.GetPanelMembers(panel.Id).Count;
                rows.Add(new PanelRow
                {
                    Id = panel.Id,
                    Name = panel.TestName,
                    Category = panel.CategoryHeading,
                    Price = panel.Price,
                    TestCount = count
                });
            }

            _loadingSelection = true;
            dgPanels.ItemsSource = rows;
            dgPanels.SelectedItem = rows.FirstOrDefault(r => r.Id == _editingId);
            _loadingSelection = false;
        }

        private void dgPanels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingSelection || dgPanels.SelectedItem is not PanelRow row)
                return;
            LoadPanel(row.Id);
        }

        private void LoadPanel(int id)
        {
            var panel = DatabaseHelper.GetTestDefinitionById(id);
            if (panel == null || !panel.IsPanel)
                return;

            _editingId = panel.Id;
            txtName.Text = panel.TestName;
            txtCategory.Text = panel.CategoryHeading;
            txtPrice.Text = panel.Price.ToString("0.##", CultureInfo.CurrentCulture);
            txtMemberHeading.Text = $"Tests in {panel.TestName}";

            _members.Clear();
            foreach (var member in DatabaseHelper.GetPanelMembers(panel.Id).Where(m => !m.IsPanel))
                _members.Add(member);
            RefreshAvailable();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshAvailable();

        private void RefreshAvailable()
        {
            var taken = _members.Select(m => m.Id).ToHashSet();
            string term = txtSearch?.Text?.Trim() ?? "";
            var matches = _individuals.Where(t => !taken.Contains(t.Id));
            if (!string.IsNullOrWhiteSpace(term))
            {
                matches = matches.Where(t =>
                    t.TestName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || t.CategoryHeading.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (lstAvailable != null)
                lstAvailable.ItemsSource = matches.ToList();
        }

        private void btnAddTests_Click(object sender, RoutedEventArgs e) => AddSelectedTests();

        private void btnRemoveTests_Click(object sender, RoutedEventArgs e) => RemoveSelectedTests();

        private void lstAvailable_MouseDoubleClick(object sender, MouseButtonEventArgs e) => AddSelectedTests();

        private void lstMembers_MouseDoubleClick(object sender, MouseButtonEventArgs e) => RemoveSelectedTests();

        private void AddSelectedTests()
        {
            foreach (TestDefinition test in lstAvailable.SelectedItems.Cast<TestDefinition>().ToList())
            {
                if (_members.All(m => m.Id != test.Id))
                    _members.Add(test);
            }
            RefreshAvailable();
        }

        private void RemoveSelectedTests()
        {
            foreach (TestDefinition test in lstMembers.SelectedItems.Cast<TestDefinition>().ToList())
                _members.Remove(test);
            RefreshAvailable();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Enter a panel name, for example CBC or LFT.", "Missing Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtPrice.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal price) || price < 0)
            {
                MessageBox.Show("Package price must be a number, zero or greater.", "Invalid Price",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = DatabaseHelper.GetTestDefinitionByName(name);
            if (existing != null && existing.Id != _editingId)
            {
                string what = existing.IsPanel ? "another panel" : "an individual test";
                MessageBox.Show($"That name is already used by {what}. Choose a different panel name.",
                    "Name In Use", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string category = string.IsNullOrWhiteSpace(txtCategory.Text) ? "General" : txtCategory.Text.Trim();
            try
            {
                DatabaseHelper.InsertOrUpdateTestDefinition(new TestDefinition
                {
                    Id = _editingId,
                    TestName = name,
                    CategoryHeading = category,
                    Unit = "",
                    NormalRange = "",
                    Price = price,
                    IsPanel = true,
                    IsActive = true
                });

                if (_editingId == 0)
                {
                    var saved = DatabaseHelper.GetTestDefinitionByName(name);
                    if (saved == null)
                        throw new InvalidOperationException("The panel was not saved.");
                    _editingId = saved.Id;
                }

                DatabaseHelper.SetPanelMembers(_editingId, _members.Select(m => m.Id).ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show("The panel could not be saved.\n" + ex.Message, "Save Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            txtMemberHeading.Text = $"Tests in {name}";
            LoadIndividuals();
            LoadGrid();
            RefreshAvailable();
            MessageBox.Show($"{name} saved at Rs. {price:N0} with {_members.Count} test(s).",
                "Panel Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            _editingId = 0;
            txtName.Text = "";
            txtCategory.Text = "";
            txtPrice.Text = "";
            txtMemberHeading.Text = "Tests in this panel";
            _members.Clear();
            _loadingSelection = true;
            dgPanels.SelectedItem = null;
            _loadingSelection = false;
            RefreshAvailable();
            txtName.Focus();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            int id = _editingId;
            string name = txtName.Text.Trim();
            if (id == 0 && dgPanels.SelectedItem is PanelRow selected)
            {
                id = selected.Id;
                name = selected.Name;
            }

            if (id == 0)
            {
                MessageBox.Show("Select a panel to delete.", "Delete Panel",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete {name}?\n\nBills that already used this panel keep their original package price and tests.",
                "Delete Panel", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                DatabaseHelper.DeleteTestDefinition(id);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The panel could not be deleted.\n" + ex.Message, "Delete Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            btnClear_Click(sender, e);
            LoadGrid();
        }

        private sealed class PanelRow
        {
            public int Id { get; init; }
            public string Name { get; init; } = "";
            public string Category { get; init; } = "";
            public decimal Price { get; init; }
            public int TestCount { get; init; }
            public string PriceText => Price.ToString("N0");
        }
    }
}
