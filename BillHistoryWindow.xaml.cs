using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class BillHistoryWindow : Window
    {
        private bool _ready;

        public BillHistoryWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");

            var doctors = new List<Doctor> { new Doctor { Id = 0, Name = "All Doctors" } };
            doctors.AddRange(DatabaseHelper.GetDoctors());
            cmbDoctor.ItemsSource = doctors;
            cmbDoctor.SelectedIndex = 0;

            dpFrom.SelectedDate = DateTime.Today;
            dpTo.SelectedDate = DateTime.Today;
            _ready = true;
            Refresh();
        }

        private DatePreset CurrentPreset()
        {
            if (rbYesterday.IsChecked == true) return DatePreset.Yesterday;
            if (rbWeek.IsChecked == true) return DatePreset.ThisWeek;
            if (rbMonth.IsChecked == true) return DatePreset.ThisMonth;
            if (rbCustom.IsChecked == true) return DatePreset.Custom;
            return DatePreset.Today;
        }

        private void DatePreset_Checked(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            pnlCustomDates.Visibility = rbCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        }

        private void CustomDate_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (!_ready || rbCustom.IsChecked != true) return;
            Refresh();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            Refresh();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            Refresh();
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e) => Refresh();

        private void Refresh()
        {
            var (from, to) = DateRangeHelper.Resolve(CurrentPreset(), dpFrom.SelectedDate, dpTo.SelectedDate);
            string? doctor = null;
            if (cmbDoctor.SelectedItem is Doctor d && d.Id > 0)
                doctor = d.Name;

            var items = DatabaseHelper.GetBillHistory(from, to, txtSearch.Text, doctor);
            dgBills.ItemsSource = items;

            decimal net = items.Where(i => !string.Equals(i.Status, "Refunded", StringComparison.OrdinalIgnoreCase))
                .Sum(i => i.NetAmount);
            int refunded = items.Count(i => string.Equals(i.Status, "Refunded", StringComparison.OrdinalIgnoreCase));
            txtSummary.Text =
                $"{items.Count} bill(s)  ·  {from:dd-MMM-yyyy} to {to:dd-MMM-yyyy}  ·  Net billed PKR {net:N0}" +
                (refunded > 0 ? $"  ·  {refunded} refunded" : "");
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}