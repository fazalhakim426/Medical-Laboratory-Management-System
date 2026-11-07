using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LabReportApp.Config;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<TestResultItem> _results = new();
        private readonly ObservableCollection<VisitDashboardItem> _pendingItems = new();
        private readonly ObservableCollection<VisitDashboardItem> _printItems = new();
        private readonly ObservableCollection<HeldBill> _heldItems = new();

        public ObservableCollection<string> AllCategories { get; set; } = new();
        private System.Collections.Generic.List<TestDefinition> _testDefinitions = new();
        private PatientProfile? _selectedProfile;
        private bool _suppressPatientSelection;
        private bool _dashboardsReady;
        private bool _recalculating;

        public MainWindow()
        {
            InitializeComponent();
            dgResults.ItemsSource = _results;
            dgPending.ItemsSource = _pendingItems;
            dgPrintQueue.ItemsSource = _printItems;
            dgHeldBills.ItemsSource = _heldItems;
            DataContext = this;

            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            _results.CollectionChanged += Results_CollectionChanged;

            RefreshHeaderBranding();

            dpPendingFrom.SelectedDate = DateTime.Today;
            dpPendingTo.SelectedDate = DateTime.Today;
            dpPrintFrom.SelectedDate = DateTime.Today;
            dpPrintTo.SelectedDate = DateTime.Today;

            LoadMasterData();
            LoadDoctorFilterCombos();
            UpdateThemeButtonLabel();
            _results.Add(new TestResultItem());
            RefreshPatientDropdown("");
            _dashboardsReady = true;
            RefreshDashboards();

            // One wheel notch must not jump the page. Handle it here for every control,
            // including the tables, so the default "one screen" wheel setting cannot take over.
            mainScrollViewer.PreviewMouseWheel += SmoothPageScroll;

            // Date filters inside later tabs are checked by default. Keep the bill tab in front.
            tabBill.IsSelected = true;
        }

        private void SmoothPageScroll(object sender, MouseWheelEventArgs e)
        {
            if (mainScrollViewer == null || e.Delta == 0) return;
            e.Handled = true;

            // A standard notch reports 120. Move about 36 pixels per notch, and less for a small flick.
            double notches = e.Delta / 120.0;
            mainScrollViewer.ScrollToVerticalOffset(mainScrollViewer.VerticalOffset - notches * 36);
        }

        // ---------------- THEME ----------------

        private void btnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
            TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            UpdateThemeButtonLabel();
        }

        private void UpdateThemeButtonLabel()
        {
            if (btnActions.ContextMenu == null)
                return;
            foreach (var item in btnActions.ContextMenu.Items)
            {
                if (item is MenuItem menu && Equals(menu.Tag, "theme"))
                    menu.Header = ThemeManager.CurrentTheme == "Dark" ? "Light Mode" : "Dark Mode";
            }
        }

        private void LoadMasterData()
        {
            _testDefinitions = DatabaseHelper.GetAllTestDefinitions();
            AllCategories.Clear();
            foreach (var category in _testDefinitions.Select(t => t.CategoryHeading).Distinct().OrderBy(c => c))
                AllCategories.Add(category);

            if (cmbPanels != null)
            {
                cmbPanels.ItemsSource = _testDefinitions
                    .Where(t => t.IsPanel && t.IsActive)
                    .OrderBy(t => t.TestName)
                    .ToList();
            }
        }

        private void btnManageTests_Click(object sender, RoutedEventArgs e)
        {
            new TestDefinitionsWindow { Owner = this }.ShowDialog();
            LoadMasterData();
        }

        private void btnManagePanels_Click(object sender, RoutedEventArgs e)
        {
            new ManagePanelsWindow { Owner = this }.ShowDialog();
            LoadMasterData();
        }

        private void btnManageDoctors_Click(object sender, RoutedEventArgs e)
        {
            new ManageDoctorsWindow { Owner = this }.ShowDialog();
            LoadDoctorFilterCombos();
        }

        private void btnBillHistory_Click(object sender, RoutedEventArgs e) =>
            new BillHistoryWindow { Owner = this }.ShowDialog();

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            new SettingsWindow { Owner = this }.ShowDialog();
            RefreshHeaderBranding();
        }

        private void btnCustomizeBill_Click(object sender, RoutedEventArgs e) =>
            new PdfCustomizeWindow(PdfDocumentKind.Bill) { Owner = this }.ShowDialog();

        private void btnCustomizeReport_Click(object sender, RoutedEventArgs e) =>
            new PdfCustomizeWindow(PdfDocumentKind.Report) { Owner = this }.ShowDialog();

        private void btnActions_Click(object sender, RoutedEventArgs e)
        {
            if (btnActions.ContextMenu == null)
                return;

            // Open after this click finishes, otherwise the same click closes the menu immediately.
            btnActions.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (btnActions.ContextMenu == null)
                    return;
                btnActions.ContextMenu.PlacementTarget = btnActions;
                btnActions.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btnActions.ContextMenu.IsOpen = true;
            }));
        }

        private void btnWhatsAppSales_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(LabProXBrand.WhatsAppUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open WhatsApp.\n" + ex.Message, LabProXBrand.ProductName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void RefreshHeaderBranding()
        {
            txtProductName.Text = LabProXBrand.ProductName;
            txtWhatsAppNumber.Text = LabProXBrand.WhatsAppDisplay;
            Title = LabProXBrand.ProductName;
            string hospital = LabProXSettingsStore.Current.Customer.HospitalName?.Trim() ?? "";
            string date = DateTime.Now.ToString("dddd, dd MMMM yyyy");
            txtHeaderSubtitle.Text = string.IsNullOrWhiteSpace(hospital)
                ? date + "   ·   Patient – Bill – Results – Print"
                : hospital + "   ·   " + date;
        }

        private void LoadDoctorFilterCombos()
        {
            var doctors = new System.Collections.Generic.List<Doctor>
            {
                new Doctor { Id = 0, Name = "All Doctors" }
            };
            doctors.AddRange(DatabaseHelper.GetDoctors());

            string? keepPending = (cmbPendingDoctor?.SelectedItem as Doctor)?.Name;
            string? keepPrint = (cmbPrintDoctor?.SelectedItem as Doctor)?.Name;

            if (cmbPendingDoctor != null)
            {
                cmbPendingDoctor.ItemsSource = doctors;
                cmbPendingDoctor.SelectedIndex = IndexOfDoctor(doctors, keepPending);
            }
            if (cmbPrintDoctor != null)
            {
                cmbPrintDoctor.ItemsSource = doctors.ToList();
                cmbPrintDoctor.SelectedIndex = IndexOfDoctor(doctors, keepPrint);
            }
        }

        private static int IndexOfDoctor(System.Collections.Generic.List<Doctor> doctors, string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            for (int i = 0; i < doctors.Count; i++)
            {
                if (string.Equals(doctors[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }

        // ---------------- PATIENT SELECTION ----------------

        private void RefreshPatientDropdown(string? query)
        {
            var matches = DatabaseHelper.SearchPatientProfiles(query, 80);
            _suppressPatientSelection = true;
            cmbPatientSearch.ItemsSource = matches;
            _suppressPatientSelection = false;
            txtPatientEmpty.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void cmbPatientSearch_DropDownOpened(object sender, EventArgs e) =>
            RefreshPatientDropdown(cmbPatientSearch.Text);

        private void cmbPatientSearch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter or Key.Tab or Key.Escape or Key.Up or Key.Down or Key.Left or Key.Right)
                return;

            RefreshPatientDropdown(cmbPatientSearch.Text);
            cmbPatientSearch.IsDropDownOpen = true;
        }

        private void cmbPatientSearch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPatientSelection) return;
            if (cmbPatientSearch.SelectedItem is PatientProfile profile)
                SelectProfile(profile);
        }

        private void SelectProfile(PatientProfile profile)
        {
            _selectedProfile = profile;
            pnlSelectedPatient.Visibility = Visibility.Visible;
            txtSelectedName.Text = profile.Name;
            txtSelectedMeta.Text =
                $"{profile.MrNumber}  ·  {profile.AgeDisplay} / {profile.Gender}  ·  {profile.PhoneNumber}" +
                (string.IsNullOrWhiteSpace(profile.Cnic) ? "" : $"  ·  CNIC {profile.Cnic}") +
                (string.IsNullOrWhiteSpace(profile.ReferredBy) ? "" : $"  ·  Ref: {profile.ReferredBy}");

            _suppressPatientSelection = true;
            cmbPatientSearch.Text = profile.DropdownLabel;
            _suppressPatientSelection = false;

            RefreshRangesOnGrid();
            txtStatus.Text = $"Selected {profile.Name}. Add tests and generate the bill.";
        }

        private void btnAddPatient_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddPatientWindow { Owner = this };
            if (win.ShowDialog() == true && win.SavedProfile != null)
            {
                RefreshPatientDropdown("");
                SelectProfile(win.SavedProfile);
            }
        }

        private void btnEditPatient_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null)
            {
                MessageBox.Show("Select a patient first.", "No Patient", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new AddPatientWindow(_selectedProfile) { Owner = this };
            if (win.ShowDialog() == true && win.SavedProfile != null)
            {
                RefreshPatientDropdown("");
                SelectProfile(win.SavedProfile);
            }
        }

        private void btnChangePatient_Click(object sender, RoutedEventArgs e)
        {
            ClearSelectedPatient();
            cmbPatientSearch.Focus();
            cmbPatientSearch.IsDropDownOpen = true;
            RefreshPatientDropdown("");
        }

        private void btnClearPatient_Click(object sender, RoutedEventArgs e) => ClearSelectedPatient();

        private void ClearSelectedPatient()
        {
            _selectedProfile = null;
            pnlSelectedPatient.Visibility = Visibility.Collapsed;
            txtSelectedName.Text = "";
            txtSelectedMeta.Text = "";
            _suppressPatientSelection = true;
            cmbPatientSearch.SelectedItem = null;
            cmbPatientSearch.Text = "";
            _suppressPatientSelection = false;
        }

        private Patient? BuildVisitPatientFromSelection(out string error)
        {
            error = "";
            if (_selectedProfile == null)
            {
                error = "Please select or add a patient before generating a bill.";
                return null;
            }

            var p = _selectedProfile;
            int legacyYears = p.AgeUnit switch
            {
                AgeUnits.Days => (int)Math.Round(p.AgeValue / 365.0),
                AgeUnits.Months => (int)Math.Round(p.AgeValue / 12.0),
                _ => (int)Math.Round(p.AgeValue)
            };

            var now = DateTime.Now;
            return new Patient
            {
                ProfileId = p.Id,
                Name = p.Name,
                PhoneNumber = p.PhoneNumber,
                Cnic = p.Cnic,
                Age = legacyYears,
                AgeValue = p.AgeValue,
                AgeUnit = p.AgeUnit,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                PregnancyStatus = p.PregnancyStatus,
                ReferredBy = p.ReferredBy,
                LabName = string.IsNullOrWhiteSpace(p.LabName) ? AppSettings.LabName : p.LabName,
                CreatedAt = now,
                SampleCollectedAt = now,
                PrintStatus = "NotPrinted"
            };
        }

        // ---------------- BILLING TOTALS ----------------

        private void Results_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (TestResultItem item in e.NewItems)
                    item.PropertyChanged += Row_PropertyChanged;

            if (e.OldItems != null)
                foreach (TestResultItem item in e.OldItems)
                    item.PropertyChanged -= Row_PropertyChanged;

            RecalculateTotal();
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TestResultItem.Price)
                or nameof(TestResultItem.Discount)
                or nameof(TestResultItem.DiscountMode)
                or nameof(TestResultItem.CatalogPrice)
                or nameof(TestResultItem.EntryRole))
                RecalculateTotal();
        }

        private void txtOverallDiscount_TextChanged(object sender, TextChangedEventArgs e) => RecalculateTotal();

        private void RecalculateTotal()
        {
            if (_recalculating)
                return;
            if (txtSubtotalAmount == null || txtDiscountAmount == null || txtTotalAmount == null || txtOverallDiscount == null)
                return;

            _recalculating = true;
            try
            {
                TestResultItem.ApplyPanelMemberDiscounts(_results);
                RecalculateTotalCore();
            }
            finally
            {
                _recalculating = false;
            }
        }

        private void RecalculateTotalCore()
        {

            decimal subtotal = _results.Sum(r => r.NetPrice);
            decimal.TryParse(txtOverallDiscount.Text, out decimal overallDiscountPercent);
            if (overallDiscountPercent < 0) overallDiscountPercent = 0;
            if (overallDiscountPercent > 100) overallDiscountPercent = 100;

            decimal discountAmount = Math.Round(subtotal * overallDiscountPercent / 100m, 2);
            decimal grandTotal = subtotal - discountAmount;

            txtSubtotalAmount.Text = $"Rs. {subtotal:N0}";
            txtDiscountAmount.Text = $"-Rs. {discountAmount:N0}";
            txtTotalAmount.Text = $"Rs. {grandTotal:N0}";
        }

        // ---------------- TEST AUTOCOMPLETE ----------------

        private List<TestDefinition> IndividualTests(string? typed = null)
        {
            IEnumerable<TestDefinition> tests = _testDefinitions.Where(t => t.IsActive && !t.IsPanel);
            if (!string.IsNullOrWhiteSpace(typed))
            {
                tests = tests.Where(t =>
                    t.TestName.Contains(typed, StringComparison.OrdinalIgnoreCase)
                    || t.CategoryHeading.Contains(typed, StringComparison.OrdinalIgnoreCase));
            }

            return tests.OrderBy(t => t.TestName).ToList();
        }

        private void TestNameCombo_Loaded(object sender, RoutedEventArgs e)
        {
            var combo = (ComboBox)sender;
            combo.ItemsSource = IndividualTests();
        }

        private void TestNameCombo_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter or Key.Tab or Key.Escape or Key.Up or Key.Down or Key.Left or Key.Right)
                return;

            var combo = (ComboBox)sender;
            string typed = combo.Text ?? string.Empty;
            var matches = IndividualTests(string.IsNullOrWhiteSpace(typed) ? null : typed);
            combo.ItemsSource = matches;
            combo.IsDropDownOpen = matches.Count > 0;
        }

        private void TestNameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = (ComboBox)sender;
            if (combo.DataContext is not TestResultItem row) return;

            TestDefinition? definition = combo.SelectedItem as TestDefinition;
            if (definition == null && combo.SelectedItem is string name)
            {
                definition = _testDefinitions.FirstOrDefault(
                    t => t.TestName.Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            if (definition == null)
                return;

            string restoreName = row.TestDefinitionId.HasValue
                ? _testDefinitions.FirstOrDefault(t => t.Id == row.TestDefinitionId.Value)?.TestName ?? ""
                : "";

            if (ApplyDefinitionToBillingRow(row, definition))
            {
                // Keep the closed combo showing the test name, not the type name.
                combo.Text = definition.TestName;
                combo.IsDropDownOpen = false;
                return;
            }

            row.TestName = restoreName;
            combo.SelectedItem = null;
            combo.Text = restoreName;
            combo.IsDropDownOpen = false;
        }

        private bool ApplyDefinitionToBillingRow(TestResultItem row, TestDefinition definition)
        {
            if (definition.IsPanel)
            {
                MessageBox.Show("Use Add Panel to put a package on the bill.", "Add Panel",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            bool alreadyOnBill = _results.Any(r =>
                r != row && r.TestDefinitionId == definition.Id && !string.IsNullOrWhiteSpace(r.TestName));
            if (alreadyOnBill)
            {
                MessageBox.Show($"{definition.TestName} is already on this bill, so it was not added again.",
                    "Duplicate Test", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            row.EntryRole = "Standard";
            row.IsPanelMember = false;
            row.PanelId = null;
            row.TestName = definition.TestName;
            row.CategoryHeading = definition.CategoryHeading;
            row.Price = definition.Price;
            row.CatalogPrice = definition.Price;
            row.TestDefinitionId = definition.Id;
            row.Unit = definition.Unit;
            var patient = BuildVisitPatientFromSelection(out _);
            if (patient != null)
            {
                var resolution = ReferenceRangeResolver.Resolve(definition.Id, patient, DateTime.Now);
                row.ApplyResolvedRange(resolution, patient, DateTime.Now);
                if (resolution.RequiresPregnancyWarning)
                {
                    MessageBox.Show(resolution.WarningMessage, "Pregnancy Status Required",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else if (!resolution.IsConfigured && !string.IsNullOrWhiteSpace(definition.NormalRange))
                {
                    row.NormalRange = definition.NormalRange;
                }
            }
            else
            {
                row.NormalRange = string.IsNullOrWhiteSpace(definition.NormalRange)
                    ? "Reference Range Not Configured"
                    : definition.NormalRange;
            }

            return true;
        }

        private void RefreshRangesOnGrid()
        {
            var patient = BuildVisitPatientFromSelection(out _);
            if (patient == null) return;

            foreach (var row in _results.Where(r => r.IsResultRow && !string.IsNullOrWhiteSpace(r.TestName)))
            {
                if (row.EntryRole == "PanelParameter")
                {
                    if (!row.TestDefinitionId.HasValue) continue;
                    var memberResolution = ReferenceRangeResolver.Resolve(row.TestDefinitionId.Value, patient, DateTime.Now);
                    row.ApplyResolvedRange(memberResolution, patient, DateTime.Now);
                    continue;
                }

                var definition = _testDefinitions.FirstOrDefault(
                    t => t.TestName.Equals(row.TestName, StringComparison.OrdinalIgnoreCase));
                if (definition == null || definition.IsPanel) continue;

                var resolution = ReferenceRangeResolver.Resolve(definition.Id, patient, DateTime.Now);
                row.ApplyResolvedRange(resolution, patient, DateTime.Now);
                if (!resolution.IsConfigured && !string.IsNullOrWhiteSpace(definition.NormalRange))
                    row.NormalRange = definition.NormalRange;
            }
        }

        private void btnAddRow_Click(object sender, RoutedEventArgs e) =>
            _results.Add(new TestResultItem());

        private void btnAddPanel_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPanels.SelectedItem is not TestDefinition panel)
            {
                cmbPanels.IsDropDownOpen = true;
                MessageBox.Show("Choose a panel, then click Add Panel.", "Add Panel",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddPanelToBill(panel);
        }

        private void AddPanelToBill(TestDefinition panel)
        {
            if (_results.Any(r => r.EntryRole == "PanelBill" && (r.PanelId == panel.Id || r.TestDefinitionId == panel.Id)))
            {
                MessageBox.Show($"{panel.TestName} is already on this bill.", "Duplicate Panel",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var members = DatabaseHelper.GetPanelMembers(panel.Id)
                .Where(m => m.IsActive && !m.IsPanel)
                .ToList();
            if (members.Count == 0)
            {
                MessageBox.Show($"{panel.TestName} has no tests yet. Add tests from Manage Panels.",
                    "Empty Panel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var skipped = new List<string>();
            var absorbed = new List<string>();
            var toInclude = new List<TestDefinition>();
            foreach (var member in members)
            {
                bool insideAnotherPanel = _results.Any(r =>
                    r.EntryRole == "PanelParameter" && r.TestDefinitionId == member.Id);
                if (insideAnotherPanel)
                {
                    skipped.Add(member.TestName);
                    continue;
                }

                foreach (var standalone in _results.Where(r =>
                    r.EntryRole == "Standard" && r.TestDefinitionId == member.Id).ToList())
                {
                    _results.Remove(standalone);
                    absorbed.Add(member.TestName);
                }

                toInclude.Add(member);
            }

            if (toInclude.Count == 0)
            {
                MessageBox.Show(
                    "Every test in this panel is already included in another panel on the bill, so it was not added.",
                    "Duplicate Tests", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var blank in _results.Where(r =>
                r.EntryRole == "Standard" && string.IsNullOrWhiteSpace(r.TestName)).ToList())
                _results.Remove(blank);

            _results.Add(new TestResultItem
            {
                EntryRole = "PanelBill",
                IsPanelMember = false,
                TestName = panel.TestName,
                CategoryHeading = string.IsNullOrWhiteSpace(panel.CategoryHeading) ? "General" : panel.CategoryHeading,
                Unit = "",
                NormalRange = "(panel)",
                Price = panel.Price,
                CatalogPrice = panel.Price,
                Discount = 0,
                DiscountMode = "Percent",
                PanelId = panel.Id,
                TestDefinitionId = panel.Id
            });

            var patient = BuildVisitPatientFromSelection(out _);
            foreach (var member in toInclude)
            {
                var row = new TestResultItem
                {
                    IsPanelMember = true,
                    EntryRole = "PanelParameter",
                    TestName = member.TestName,
                    CategoryHeading = string.IsNullOrWhiteSpace(member.CategoryHeading) ? panel.CategoryHeading : member.CategoryHeading,
                    Unit = member.Unit,
                    Price = 0,
                    CatalogPrice = member.Price,
                    Discount = 0,
                    DiscountMode = "Percent",
                    PanelId = panel.Id,
                    TestDefinitionId = member.Id
                };

                if (patient != null)
                {
                    var resolution = ReferenceRangeResolver.Resolve(member.Id, patient, DateTime.Now);
                    row.ApplyResolvedRange(resolution, patient, DateTime.Now);
                }
                else
                {
                    row.NormalRange = string.IsNullOrWhiteSpace(member.NormalRange)
                        ? "Reference Range Not Configured"
                        : member.NormalRange;
                }

                _results.Add(row);
            }

            cmbPanels.SelectedItem = null;
            txtStatus.Text = $"{panel.TestName} added at package price Rs. {panel.Price:N0} with {toInclude.Count} test(s).";

            if (absorbed.Count > 0 || skipped.Count > 0)
            {
                string message = "";
                if (absorbed.Count > 0)
                    message += "These tests were already on the bill individually. They are now included in the panel and charged once, at the package price:\n"
                        + string.Join(", ", absorbed.Distinct()) + "\n\n";
                if (skipped.Count > 0)
                    message += "These tests are already inside another panel on this bill and were not added again:\n"
                        + string.Join(", ", skipped.Distinct());
                MessageBox.Show(message.Trim(), "Duplicate Tests", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnRemoveThisRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: TestResultItem row })
                return;

            var doomed = new List<TestResultItem> { row };
            if (row.EntryRole == "PanelBill")
            {
                doomed.AddRange(_results.Where(r =>
                    r != row && r.EntryRole == "PanelParameter" && r.PanelId == row.PanelId));
            }

            if (_results.Count - doomed.Count <= 0)
            {
                foreach (var extra in doomed.Where(r => r != row).ToList())
                    _results.Remove(extra);
                ResetBillingRow(row);
                return;
            }

            foreach (var item in doomed)
                _results.Remove(item);

            if (row.EntryRole == "PanelParameter")
                txtStatus.Text = $"{row.TestName} removed from this bill. The panel package price is unchanged.";
        }

        private static void ResetBillingRow(TestResultItem row)
        {
            row.EntryRole = "Standard";
            row.IsPanelMember = false;
            row.PanelId = null;
            row.TestDefinitionId = null;
            row.TestName = "";
            row.CategoryHeading = "";
            row.Unit = "";
            row.NormalRange = "";
            row.Price = 0;
            row.CatalogPrice = 0;
            row.Discount = 0;
            row.DiscountMode = "Percent";
        }

        // ---------------- GENERATE BILL ----------------

        private void btnGenerateBill_Click(object sender, RoutedEventArgs e)
        {
            var patientDraft = BuildVisitPatientFromSelection(out string patientError);
            if (patientDraft == null)
            {
                MessageBox.Show(patientError, "Patient Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validResults = _results.Where(r => !string.IsNullOrWhiteSpace(r.TestName)).ToList();
            if (validResults.Count == 0)
            {
                MessageBox.Show("Please select at least one test.", "Missing Tests",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtOverallDiscount.Text.Trim(), out decimal overallDiscountPercent) ||
                overallDiscountPercent < 0 || overallDiscountPercent > 100)
            {
                MessageBox.Show("Overall Discount % must be a number between 0 and 100.",
                    "Invalid Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestResultItem.ApplyPanelMemberDiscounts(validResults);
            foreach (var row in validResults)
            {
                if (row.Discount < 0 || (!row.IsFixedDiscount && row.Discount > 100))
                {
                    string limit = row.IsFixedDiscount ? "zero or greater" : "between 0 and 100";
                    MessageBox.Show($"Discount for '{row.TestName}' must be {limit}.",
                        "Invalid Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(row.CategoryHeading))
                    row.CategoryHeading = "General";
                if (!row.TestDefinitionId.HasValue)
                {
                    var def = _testDefinitions.FirstOrDefault(
                        t => t.TestName.Equals(row.TestName, StringComparison.OrdinalIgnoreCase));
                    if (def != null) row.TestDefinitionId = def.Id;
                }

                var linked = row.TestDefinitionId.HasValue
                    ? _testDefinitions.FirstOrDefault(t => t.Id == row.TestDefinitionId.Value)
                    : null;
                if (linked != null && linked.IsPanel && row.EntryRole != "PanelBill")
                {
                    MessageBox.Show($"Use Add Panel to add {linked.TestName}.", "Add Panel",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            btnGenerateBill.IsEnabled = false;
            txtStatus.Text = "Saving visit and generating bill...";

            try
            {
                decimal subtotal = validResults.Sum(r => r.NetPrice);
                decimal discountAmount = Math.Round(subtotal * overallDiscountPercent / 100m, 2);
                decimal netTotal = subtotal - discountAmount;

                var patient = patientDraft;
                patient.ReportHeading = "Laboratory Test Report";
                patient.Status = "Pending";
                patient.TotalAmount = subtotal;
                patient.DiscountPercentage = overallDiscountPercent;
                patient.NetAmount = netTotal;
                patient.PrintStatus = "NotPrinted";

                string serialNumber = DatabaseHelper.SavePatientAndResults(patient, validResults);
                patient.SerialNumber = serialNumber;

                string reportUrl = AppSettings.ReportsBaseUrl.TrimEnd('/') + "/" + serialNumber + ".pdf";
                byte[] qrBytes = QrCodeService.GenerateQrPng(reportUrl);
                string billPath = BillService.GenerateBill(patient, validResults, qrBytes);

                try { BackupService.BackupDatabase(out _); } catch { /* never block */ }

                txtStatus.Text = $"Bill {serialNumber} generated · Payable Rs. {netTotal:N0}. Added to Pending Results.";

                MessageBox.Show(
                    $"Bill generated: {serialNumber}\n\n" +
                    $"Patient: {patient.Name}\n" +
                    $"Subtotal: Rs. {subtotal:N0}\n" +
                    $"Discount: -Rs. {discountAmount:N0}\n" +
                    $"Total Payable: Rs. {netTotal:N0}\n\n" +
                    "Enter results from the Pending Results tab when ready.",
                    "Bill Generated", MessageBoxButton.OK, MessageBoxImage.Information);

                // Reset billing rows but keep the selected patient for another bill if needed.
                _results.Clear();
                _results.Add(new TestResultItem());
                txtOverallDiscount.Text = "0";
                RefreshDashboards();
                tabPending.IsSelected = true;

                var open = MessageBox.Show("Open the bill PDF now?", "Bill", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (open == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(billPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Failed: " + ex.Message;
            }
            finally
            {
                btnGenerateBill.IsEnabled = true;
            }
        }

        // ---------------- DASHBOARDS ----------------

        private void RefreshDashboards()
        {
            RefreshHeldBills();
            RefreshPending();
            RefreshPrintQueue();
        }

        private void RefreshHeldBills()
        {
            _heldItems.Clear();
            foreach (var item in DatabaseHelper.GetHeldBills())
                _heldItems.Add(item);
            txtHeldHeading.Text = $"Held Bills ({_heldItems.Count})";
            txtHeldEmpty.Visibility = _heldItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            dgHeldBills.Visibility = _heldItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // ---------------- HOLD / RESUME BILL ----------------

        private void btnHoldBill_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null)
            {
                MessageBox.Show("Select a patient before holding a bill.", "Hold Bill",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validResults = _results.Where(r => !string.IsNullOrWhiteSpace(r.TestName)).ToList();
            if (validResults.Count == 0)
            {
                MessageBox.Show("Add at least one test before holding this bill.", "Hold Bill",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal.TryParse(txtOverallDiscount.Text.Trim(), out decimal overallDiscount);
            if (overallDiscount < 0) overallDiscount = 0;
            if (overallDiscount > 100) overallDiscount = 100;

            var held = new HeldBill
            {
                ProfileId = _selectedProfile.Id,
                PatientName = _selectedProfile.Name,
                MrNumber = _selectedProfile.MrNumber,
                PhoneNumber = _selectedProfile.PhoneNumber,
                Cnic = _selectedProfile.Cnic,
                AgeValue = _selectedProfile.AgeValue,
                AgeUnit = _selectedProfile.AgeUnit,
                DateOfBirth = _selectedProfile.DateOfBirth,
                Gender = _selectedProfile.Gender,
                PregnancyStatus = _selectedProfile.PregnancyStatus,
                ReferredBy = _selectedProfile.ReferredBy,
                LabName = _selectedProfile.LabName,
                OverallDiscount = overallDiscount,
                TestSummary = string.Join(", ", validResults.Where(r => r.IsBillingRow).Select(r => r.TestName)),
                HeldAt = DateTime.Now,
                Items = validResults.Select(r => new TestResultItem
                {
                    CategoryHeading = r.CategoryHeading,
                    TestName = r.TestName,
                    Unit = r.Unit,
                    NormalRange = r.NormalRange,
                    Price = r.Price,
                    CatalogPrice = r.CatalogPrice,
                    Discount = r.Discount,
                    DiscountMode = r.DiscountMode,
                    PanelId = r.PanelId,
                    TestDefinitionId = r.TestDefinitionId,
                    IsPanelMember = r.IsPanelMember,
                    EntryRole = r.EntryRole,
                    IsQualitative = r.IsQualitative,
                    RangeMinValue = r.RangeMinValue,
                    RangeMaxValue = r.RangeMaxValue,
                    Methodology = r.Methodology
                }).ToList()
            };

            try
            {
                DatabaseHelper.SaveHeldBill(held);
                ResetBillingForm(clearPatient: true);
                RefreshHeldBills();
                txtStatus.Text = $"Bill for {held.PatientName} held. You can create an emergency bill now.";
                MessageBox.Show(
                    $"Bill for {held.PatientName} is on hold.\n\n" +
                    "The form is clear — create the emergency bill now.\n" +
                    "Resume the held bill from the Held Bills card when ready.",
                    "Bill Held", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not hold bill:\n" + ex.Message, "Hold Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnResumeHeld_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: HeldBill header }) return;

            bool formBusy = _selectedProfile != null ||
                            _results.Any(r => !string.IsNullOrWhiteSpace(r.TestName));
            if (formBusy)
            {
                var confirm = MessageBox.Show(
                    "The current bill form has data.\n\nResume the held bill and replace what is on screen?",
                    "Resume Held Bill", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
            }

            var full = DatabaseHelper.GetHeldBillById(header.Id);
            if (full == null)
            {
                MessageBox.Show("That held bill was not found (it may have been discarded).",
                    "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshHeldBills();
                return;
            }

            ResetBillingForm(clearPatient: true);

            PatientProfile? profile = null;
            if (full.ProfileId.HasValue)
                profile = DatabaseHelper.GetPatientProfileById(full.ProfileId.Value);

            profile ??= new PatientProfile
            {
                Id = full.ProfileId ?? 0,
                MrNumber = full.MrNumber,
                Name = full.PatientName,
                PhoneNumber = full.PhoneNumber,
                Cnic = full.Cnic,
                AgeValue = full.AgeValue,
                AgeUnit = full.AgeUnit,
                DateOfBirth = full.DateOfBirth,
                Gender = full.Gender,
                PregnancyStatus = full.PregnancyStatus,
                ReferredBy = full.ReferredBy,
                LabName = full.LabName
            };

            // If profile was only a snapshot (Id 0), keep it selected in-memory without re-saving.
            if (profile.Id > 0)
                SelectProfile(profile);
            else
            {
                _selectedProfile = profile;
                pnlSelectedPatient.Visibility = Visibility.Visible;
                txtSelectedName.Text = profile.Name;
                txtSelectedMeta.Text =
                    $"{profile.MrNumber}  ·  {profile.AgeDisplay} / {profile.Gender}  ·  {profile.PhoneNumber}" +
                    (string.IsNullOrWhiteSpace(profile.Cnic) ? "" : $"  ·  CNIC {profile.Cnic}");
            }

            _results.Clear();
            foreach (var item in full.Items)
                _results.Add(item);
            if (_results.Count == 0)
                _results.Add(new TestResultItem());

            txtOverallDiscount.Text = full.OverallDiscount.ToString("0.##");
            RecalculateTotal();
            RefreshRangesOnGrid();

            DatabaseHelper.DeleteHeldBill(full.Id);
            RefreshHeldBills();
            txtStatus.Text = $"Resumed held bill for {full.PatientName}. Continue editing, then Generate Bill.";
            mainScrollViewer.ScrollToHome();
        }

        private void btnDiscardHeld_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: HeldBill bill }) return;

            var confirm = MessageBox.Show(
                $"Discard held bill for {bill.PatientName}?\nThis cannot be undone.",
                "Discard Held Bill", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            DatabaseHelper.DeleteHeldBill(bill.Id);
            RefreshHeldBills();
            txtStatus.Text = $"Discarded held bill for {bill.PatientName}.";
        }

        private void ResetBillingForm(bool clearPatient)
        {
            if (clearPatient)
                ClearSelectedPatient();

            _results.Clear();
            _results.Add(new TestResultItem());
            txtOverallDiscount.Text = "0";
            RecalculateTotal();
        }

        private void RefreshPending()
        {
            if (!_dashboardsReady) return;

            var preset = PendingPreset();
            var (from, to) = DateRangeHelper.Resolve(preset, dpPendingFrom?.SelectedDate, dpPendingTo?.SelectedDate);
            string? doctor = DoctorFilterName(cmbPendingDoctor);
            string? patient = string.IsNullOrWhiteSpace(txtPendingPatient?.Text) ? null : txtPendingPatient.Text;

            var items = DatabaseHelper.GetPendingVisitDashboardItems(
                txtPendingSearch?.Text, from, to, doctor, patient);
            _pendingItems.Clear();
            foreach (var item in items) _pendingItems.Add(item);
            txtPendingHeading.Text = $"Pending Laboratory Results ({_pendingItems.Count})";
            tabPending.Header = $"Pending Results ({_pendingItems.Count})";
            if (txtPendingSubtitle != null)
                txtPendingSubtitle.Text = $"Showing {from:dd-MMM-yyyy} to {to:dd-MMM-yyyy} · waiting for results.";
            txtPendingEmpty.Visibility = _pendingItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshPrintQueue()
        {
            if (!_dashboardsReady) return;

            var preset = PrintPreset();
            var (from, to) = DateRangeHelper.Resolve(preset, dpPrintFrom?.SelectedDate, dpPrintTo?.SelectedDate);
            string? doctor = DoctorFilterName(cmbPrintDoctor);
            string? patient = string.IsNullOrWhiteSpace(txtPrintPatient?.Text) ? null : txtPrintPatient.Text;

            // Full completed-report history for the selected range (printed + unprinted).
            var items = DatabaseHelper.GetUnprintedCompletedVisits(
                txtPrintSearch?.Text, from, to, doctor, patient, unprintedOnly: false);
            _printItems.Clear();
            foreach (var item in items) _printItems.Add(item);
            txtPrintHeading.Text = $"Reports Awaiting / History ({_printItems.Count})";
            tabReports.Header = $"Reports ({_printItems.Count})";
            if (txtPrintSubtitle != null)
                txtPrintSubtitle.Text = $"Showing {from:dd-MMM-yyyy} to {to:dd-MMM-yyyy} · preview or print any report.";
            txtPrintEmpty.Visibility = _printItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            btnPrintAll.IsEnabled = _printItems.Any(i =>
                !string.Equals(i.PrintStatus, "Printed", StringComparison.OrdinalIgnoreCase));
        }

        private static string? DoctorFilterName(ComboBox? combo)
        {
            if (combo?.SelectedItem is Doctor d && d.Id > 0)
                return d.Name;
            return null;
        }

        private DatePreset PendingPreset()
        {
            if (rbPendingYesterday?.IsChecked == true) return DatePreset.Yesterday;
            if (rbPendingMonth?.IsChecked == true) return DatePreset.ThisMonth;
            if (rbPendingCustom?.IsChecked == true) return DatePreset.Custom;
            return DatePreset.Today;
        }

        private DatePreset PrintPreset()
        {
            if (rbPrintYesterday?.IsChecked == true) return DatePreset.Yesterday;
            if (rbPrintMonth?.IsChecked == true) return DatePreset.ThisMonth;
            if (rbPrintCustom?.IsChecked == true) return DatePreset.Custom;
            return DatePreset.Today;
        }

        private void btnRefreshDashboards_Click(object sender, RoutedEventArgs e) => RefreshDashboards();

        private void txtPendingSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshPending();

        private void txtPrintSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshPrintQueue();

        private void btnTogglePendingFilters_Click(object sender, RoutedEventArgs e)
        {
            pnlPendingFilters.Visibility = pnlPendingFilters.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void btnTogglePrintFilters_Click(object sender, RoutedEventArgs e)
        {
            pnlPrintFilters.Visibility = pnlPrintFilters.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void PendingDatePreset_Checked(object sender, RoutedEventArgs e)
        {
            if (!_dashboardsReady) return;
            pnlPendingCustomDates.Visibility = rbPendingCustom.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
            RefreshPending();
        }

        private void PrintDatePreset_Checked(object sender, RoutedEventArgs e)
        {
            if (!_dashboardsReady) return;
            pnlPrintCustomDates.Visibility = rbPrintCustom.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
            RefreshPrintQueue();
        }

        private void PendingFilter_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (!_dashboardsReady) return;
            if (rbPendingCustom?.IsChecked == true || sender is ComboBox)
                RefreshPending();
        }

        private void PrintFilter_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (!_dashboardsReady) return;
            if (rbPrintCustom?.IsChecked == true || sender is ComboBox)
                RefreshPrintQueue();
        }

        private void btnEnterResultsRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: VisitDashboardItem item }) return;

            var win = new EnterResultsWindow(item.VisitId) { Owner = this };
            win.ShowDialog();
            RefreshDashboards();
        }

        private void btnRefundBill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: VisitDashboardItem item }) return;

            var confirm = MessageBox.Show(
                $"Refund bill {item.SerialNumber} for {item.PatientName}?\n\n" +
                $"Net amount PKR {item.NetAmount:N0} will be marked refunded and removed from pending results.",
                "Confirm Refund", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            if (!DatabaseHelper.RefundPendingVisit(item.VisitId, out string error))
            {
                MessageBox.Show(error, "Refund Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            txtStatus.Text = $"Refunded {item.SerialNumber} ({item.PatientName}).";
            RefreshDashboards();
        }

        private void btnPreviewReport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: VisitDashboardItem item }) return;
            if (!ReportPrintService.TryOpen(item.SerialNumber, out string error))
                MessageBox.Show(error, "Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void btnPrintReport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: VisitDashboardItem item }) return;

            if (!ReportPrintService.TryPrint(item.SerialNumber, item.VisitId, out string error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                    MessageBox.Show(error, "Print", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                txtStatus.Text = $"Sent {item.SerialNumber} to printer.";
            }

            RefreshPrintQueue();
        }

        private void btnPrintAll_Click(object sender, RoutedEventArgs e)
        {
            var queue = _printItems
                .Where(i => !string.Equals(i.PrintStatus, "Printed", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (queue.Count == 0)
            {
                MessageBox.Show("There are no unprinted reports in the current filter.", "Print Unprinted",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Print {queue.Count} unprinted report(s)?\n\nEach patient's report will be sent as a separate print job.",
                "Print Unprinted Reports", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            int ok = 0;
            foreach (var item in queue)
            {
                if (ReportPrintService.TryPrint(item.SerialNumber, item.VisitId, out _))
                    ok++;
            }

            MessageBox.Show($"Print jobs started for {ok} of {queue.Count} report(s).",
                "Bulk Print", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshPrintQueue();
        }

    }
}
