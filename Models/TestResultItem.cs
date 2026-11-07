using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using LabReportApp.Services;

namespace LabReportApp.Models
{
    // One billed/result row. NormalRange / AbnormalFlag / demographics are snapshotted so
    // historical reports stay frozen if master reference ranges change later.
    // Price, CatalogPrice, Discount and DiscountMode are also copied onto the bill at save time.
    public class TestResultItem : INotifyPropertyChanged
    {
        private int _id;
        private int? _testDefinitionId;
        private int? _panelId;
        private string _categoryHeading = string.Empty;
        private string _testName = string.Empty;
        private string _result = string.Empty;
        private string _unit = string.Empty;
        private string _normalRange = string.Empty;
        private decimal _price;
        private decimal _catalogPrice;
        private decimal _discount;
        private string _discountMode = "Percent";
        private decimal _memberDiscountTotal;
        private string _abnormalFlag = string.Empty;
        private string _entryRole = "Standard"; // Standard | PanelBill | PanelParameter
        private bool _isQualitative;
        private bool _isPanelMember;
        private double? _rangeMinValue;
        private double? _rangeMaxValue;
        private string _methodology = string.Empty;
        private int _ageAtTestDays;
        private string _ageAtTestDisplay = string.Empty;
        private string _genderAtTest = string.Empty;
        private string _pregnancyStatusAtTest = string.Empty;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public int? TestDefinitionId
        {
            get => _testDefinitionId;
            set { _testDefinitionId = value; OnPropertyChanged(nameof(TestDefinitionId)); }
        }

        /// <summary>Panel definition id shared by a package row and the tests included in it.</summary>
        public int? PanelId
        {
            get => _panelId;
            set { _panelId = value; OnPropertyChanged(nameof(PanelId)); }
        }

        public string CategoryHeading
        {
            get => _categoryHeading;
            set { _categoryHeading = value; OnPropertyChanged(nameof(CategoryHeading)); }
        }

        public string TestName
        {
            get => _testName;
            set { _testName = value; OnPropertyChanged(nameof(TestName)); }
        }

        public string Result
        {
            get => _result;
            set
            {
                _result = value;
                OnPropertyChanged(nameof(Result));
                RecalculateStatus();
            }
        }

        public string Unit
        {
            get => _unit;
            set { _unit = value; OnPropertyChanged(nameof(Unit)); }
        }

        public string NormalRange
        {
            get => _normalRange;
            set
            {
                _normalRange = value;
                OnPropertyChanged(nameof(NormalRange));
                OnPropertyChanged(nameof(RangeText));
                RecalculateStatus();
            }
        }

        /// <summary>Amount charged for a standard test, or the package price for a panel.</summary>
        public decimal Price
        {
            get => _price;
            set
            {
                _price = value;
                OnPropertyChanged(nameof(Price));
                NotifyPricing();
            }
        }

        /// <summary>
        /// Individual list price copied when the row was added. Panel tests are not charged this
        /// amount; it is only the base for a discount on that included test.
        /// </summary>
        public decimal CatalogPrice
        {
            get => _catalogPrice;
            set
            {
                _catalogPrice = value;
                OnPropertyChanged(nameof(CatalogPrice));
                NotifyPricing();
            }
        }

        /// <summary>Percent (0–100) or a fixed rupee amount, depending on <see cref="DiscountMode"/>.</summary>
        public decimal Discount
        {
            get => _discount;
            set
            {
                _discount = value;
                OnPropertyChanged(nameof(Discount));
                NotifyPricing();
            }
        }

        /// <summary>"Percent" (default, including bills saved before modes existed) or "Fixed".</summary>
        public string DiscountMode
        {
            get => _discountMode;
            set => SetDiscountMode(value);
        }

        /// <summary>"%" or "Rs" for the billing discount dropdown.</summary>
        public string DiscountModeLabel
        {
            get => IsFixedDiscount ? "Rs" : "%";
            set => SetDiscountMode(value);
        }

        public bool IsFixedDiscount =>
            string.Equals(_discountMode, "Fixed", StringComparison.OrdinalIgnoreCase);

        /// <summary>Price the discount is taken from. Included tests use their snapshotted list price.</summary>
        public decimal DiscountBase => EntryRole == "PanelParameter" ? CatalogPrice : Price;

        public decimal DiscountAmount
        {
            get
            {
                if (Discount <= 0 || DiscountBase <= 0)
                    return 0;
                if (IsFixedDiscount)
                    return Math.Min(Discount, DiscountBase);
                decimal percent = Math.Min(Discount, 100m);
                return Math.Round(DiscountBase * percent / 100m, 2);
            }
        }

        public string DiscountText =>
            Discount <= 0 ? "-" : IsFixedDiscount ? $"Rs. {Discount:0.##}" : $"{Discount:0.##}%";

        /// <summary>
        /// Charged amount. Included tests contribute 0; their discounts are taken off the panel package.
        /// </summary>
        public decimal NetPrice
        {
            get
            {
                if (EntryRole == "PanelParameter")
                    return 0;
                decimal net = Price - DiscountAmount;
                if (EntryRole == "PanelBill")
                    net -= _memberDiscountTotal;
                return Math.Max(0, Math.Round(net, 2));
            }
        }

        public string PriceText => EntryRole == "PanelParameter"
            ? (CatalogPrice > 0 ? $"Included ({CatalogPrice:N0})" : "Included")
            : Price.ToString("N0");

        public string NetText => EntryRole == "PanelParameter"
            ? (DiscountAmount > 0 ? $"-{DiscountAmount:N0}" : "—")
            : NetPrice.ToString("N0");

        public string RangeText => EntryRole == "PanelBill" ? "Package price" : NormalRange;

        public bool IsTestEditable => EntryRole == "Standard";

        public string AbnormalFlag
        {
            get => _abnormalFlag;
            set
            {
                _abnormalFlag = value;
                OnPropertyChanged(nameof(AbnormalFlag));
                OnPropertyChanged(nameof(StatusBrush));
            }
        }

        public string EntryRole
        {
            get => _entryRole;
            set
            {
                _entryRole = string.IsNullOrWhiteSpace(value) ? "Standard" : value;
                OnPropertyChanged(nameof(EntryRole));
                OnPropertyChanged(nameof(IsBillingRow));
                OnPropertyChanged(nameof(IsResultRow));
                OnPropertyChanged(nameof(IsTestEditable));
                NotifyPricing();
            }
        }

        public bool IsPanelMember
        {
            get => _isPanelMember;
            set
            {
                _isPanelMember = value;
                if (value && EntryRole != "PanelBill")
                    EntryRole = "PanelParameter";
                OnPropertyChanged(nameof(IsPanelMember));
            }
        }

        public bool IsQualitative
        {
            get => _isQualitative;
            set { _isQualitative = value; OnPropertyChanged(nameof(IsQualitative)); RecalculateStatus(); }
        }

        public double? RangeMinValue
        {
            get => _rangeMinValue;
            set { _rangeMinValue = value; OnPropertyChanged(nameof(RangeMinValue)); RecalculateStatus(); }
        }

        public double? RangeMaxValue
        {
            get => _rangeMaxValue;
            set { _rangeMaxValue = value; OnPropertyChanged(nameof(RangeMaxValue)); RecalculateStatus(); }
        }

        public string Methodology
        {
            get => _methodology;
            set { _methodology = value; OnPropertyChanged(nameof(Methodology)); }
        }

        public int AgeAtTestDays
        {
            get => _ageAtTestDays;
            set { _ageAtTestDays = value; OnPropertyChanged(nameof(AgeAtTestDays)); }
        }

        public string AgeAtTestDisplay
        {
            get => _ageAtTestDisplay;
            set { _ageAtTestDisplay = value; OnPropertyChanged(nameof(AgeAtTestDisplay)); }
        }

        public string GenderAtTest
        {
            get => _genderAtTest;
            set { _genderAtTest = value; OnPropertyChanged(nameof(GenderAtTest)); }
        }

        public string PregnancyStatusAtTest
        {
            get => _pregnancyStatusAtTest;
            set { _pregnancyStatusAtTest = value; OnPropertyChanged(nameof(PregnancyStatusAtTest)); }
        }

        /// <summary>Alias used by DatabaseHelper column mapping.</summary>
        public string PregnancyAtTest
        {
            get => PregnancyStatusAtTest;
            set => PregnancyStatusAtTest = value;
        }

        public bool IsBillingRow => EntryRole is "Standard" or "PanelBill";
        public bool IsResultRow => EntryRole is "Standard" or "PanelParameter";

        /// <summary>Fold included-test discounts into each panel package on the bill.</summary>
        public void SetMemberDiscountTotal(decimal total)
        {
            total = Math.Max(0, Math.Round(total, 2));
            if (_memberDiscountTotal == total)
                return;
            _memberDiscountTotal = total;
            OnPropertyChanged(nameof(NetPrice));
            OnPropertyChanged(nameof(NetText));
        }

        public static void ApplyPanelMemberDiscounts(IEnumerable<TestResultItem> rows)
        {
            var list = rows as IList<TestResultItem> ?? rows.ToList();
            foreach (var panel in list.Where(r => r.EntryRole == "PanelBill"))
            {
                int? id = panel.PanelId ?? panel.TestDefinitionId;
                decimal sum = list
                    .Where(m => m.EntryRole == "PanelParameter" && m.PanelId == id)
                    .Sum(m => m.DiscountAmount);
                panel.SetMemberDiscountTotal(sum);
            }
        }

        public Brush StatusBrush
        {
            get
            {
                return AbnormalFlag switch
                {
                    ResultStatusEvaluator.Low => new SolidColorBrush(Color.FromRgb(0xEF, 0x6C, 0x7E)),
                    ResultStatusEvaluator.High => new SolidColorBrush(Color.FromRgb(0xF0, 0xA5, 0x00)),
                    ResultStatusEvaluator.Normal => new SolidColorBrush(Color.FromRgb(0x22, 0xC1, 0xA3)),
                    ResultStatusEvaluator.NotConfigured => new SolidColorBrush(Color.FromRgb(0xE6, 0xB8, 0x00)),
                    _ => new SolidColorBrush(Color.FromRgb(0x8A, 0x94, 0xA6))
                };
            }
        }

        public void RecalculateStatus()
        {
            if (!IsResultRow)
            {
                AbnormalFlag = "";
                return;
            }

            AbnormalFlag = ResultStatusEvaluator.Evaluate(this);
        }

        public void ApplyResolvedRange(ReferenceRangeResolution resolution, Patient patient, DateTime asOf)
        {
            NormalRange = resolution.DisplayText;
            if (!string.IsNullOrWhiteSpace(resolution.Unit))
                Unit = resolution.Unit;

            if (resolution.Range != null)
            {
                RangeMinValue = resolution.Range.MinValue;
                RangeMaxValue = resolution.Range.MaxValue;
                IsQualitative = resolution.Range.IsQualitative;
                Methodology = resolution.Range.Methodology ?? "";
            }
            else
            {
                RangeMinValue = null;
                RangeMaxValue = null;
                IsQualitative = false;
                Methodology = "";
            }

            AgeAtTestDays = AgeCalculator.GetAgeInDays(patient, asOf);
            AgeAtTestDisplay = AgeCalculator.FormatAgeDisplay(patient);
            GenderAtTest = patient.Gender;
            PregnancyStatusAtTest = patient.PregnancyStatus;
            RecalculateStatus();
        }

        private void SetDiscountMode(string? value)
        {
            string text = value?.Trim() ?? "";
            string mode = text.Equals("Fixed", StringComparison.OrdinalIgnoreCase)
                          || text.Equals("Rs", StringComparison.OrdinalIgnoreCase)
                          || text.Equals("Rs.", StringComparison.OrdinalIgnoreCase)
                ? "Fixed"
                : "Percent";
            if (_discountMode == mode)
            {
                OnPropertyChanged(nameof(DiscountModeLabel));
                return;
            }

            _discountMode = mode;
            OnPropertyChanged(nameof(DiscountMode));
            OnPropertyChanged(nameof(DiscountModeLabel));
            NotifyPricing();
        }

        private void NotifyPricing()
        {
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(DiscountText));
            OnPropertyChanged(nameof(NetPrice));
            OnPropertyChanged(nameof(PriceText));
            OnPropertyChanged(nameof(NetText));
            OnPropertyChanged(nameof(RangeText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
