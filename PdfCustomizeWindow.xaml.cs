using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LabReportApp.Config;
using LabReportApp.Models;
using LabReportApp.Services;
using Microsoft.Win32;

namespace LabReportApp
{
    public partial class PdfCustomizeWindow : Window
    {
        private readonly PdfDocumentKind _kind;
        private readonly DispatcherTimer _previewTimer;
        private PdfTemplateSettings _draft = new();
        private bool _loading;
        private bool _dirty;
        private bool _closeConfirmed;
        private PaperSizeKind _lastPaper = PaperSizeKind.A4;
        private TextBox? _colorTarget;
        private TextBox? _invalidBox;
        private string _invalidMessage = "";
        private string _logoFileName = "";

        public PdfCustomizeWindow(PdfDocumentKind kind)
        {
            InitializeComponent();
            _kind = kind;
            Title = kind == PdfDocumentKind.Bill
                ? "Customize Bill / Receipt — Lab ProX"
                : "Customize Laboratory Report — Lab ProX";
            txtWindowTitle.Text = kind == PdfDocumentKind.Bill
                ? "Customize Bill / Receipt"
                : "Customize Laboratory Report";
            txtWindowSubtitle.Text = kind == PdfDocumentKind.Bill
                ? "These controls change bills and receipts only. Laboratory report settings stay separate."
                : "These controls change laboratory reports only. Bill and receipt settings stay separate.";
            txtPaperHelp.Text = kind == PdfDocumentKind.Bill
                ? "Every bill is generated at this size so the layout matches the paper and printer."
                : "Every report is generated at this size. Jobs from the report queue are sent to the printer selected here.";
            pnlBillFields.Visibility = kind == PdfDocumentKind.Bill ? Visibility.Visible : Visibility.Collapsed;
            pnlReportFields.Visibility = kind == PdfDocumentKind.Report ? Visibility.Visible : Visibility.Collapsed;
            txtLockedFooter.Text = LabProXBrand.FooterText;

            SourceInitialized += (_, _) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _previewTimer.Tick += (_, _) =>
            {
                _previewTimer.Stop();
                RenderPreview();
            };
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _loading = true;
            FillFonts();
            FillPrinters();
            _draft = (_kind == PdfDocumentKind.Bill
                ? LabProXSettingsStore.Current.Bill
                : LabProXSettingsStore.Current.Report).Clone();
            LoadForm(_draft);
            _logoFileName = LabProXSettingsStore.Current.Customer.LogoFileName ?? "";
            ShowLogo();
            _loading = false;
            UpdateCustomPanel();
            PaintSwatches();
            RenderPreview();
        }

        private void FillFonts()
        {
            FillFontCombo(cmbHeadingFont);
            FillFontCombo(cmbBodyFont);
        }

        private static void FillFontCombo(ComboBox box)
        {
            box.Items.Clear();
            foreach (var family in PdfFontRegistry.AvailableFamilies)
                box.Items.Add(family);
        }

        private void FillPrinters()
        {
            cmbPrinter.Items.Clear();
            cmbPrinter.Items.Add(new ComboBoxItem { Content = "System default printer", Tag = "" });
            foreach (var name in InstalledPrinters.GetNames())
                cmbPrinter.Items.Add(new ComboBoxItem { Content = name, Tag = name });
        }

        private void LoadForm(PdfTemplateSettings template)
        {
            bool previous = _loading;
            _loading = true;

            SelectTag(cmbPaperSize, template.PaperSize.ToString());
            txtCustomWidth.Text = template.CustomWidthMm.ToString("0.##", CultureInfo.InvariantCulture);
            txtCustomHeight.Text = template.CustomHeightMm.ToString("0.##", CultureInfo.InvariantCulture);
            chkCustomContinuous.IsChecked = template.CustomContinuous;
            EnsurePrinter(template.PreferredPrinter);
            SelectTag(cmbPrinter, template.PreferredPrinter ?? "");

            txtMarginTop.Text = template.MarginTopMm.ToString("0.##", CultureInfo.InvariantCulture);
            txtMarginRight.Text = template.MarginRightMm.ToString("0.##", CultureInfo.InvariantCulture);
            txtMarginBottom.Text = template.MarginBottomMm.ToString("0.##", CultureInfo.InvariantCulture);
            txtMarginLeft.Text = template.MarginLeftMm.ToString("0.##", CultureInfo.InvariantCulture);
            txtSectionSpacing.Text = template.SectionSpacingMm.ToString("0.##", CultureInfo.InvariantCulture);
            txtCellPadding.Text = template.CellPaddingMm.ToString("0.##", CultureInfo.InvariantCulture);
            chkPageBorder.IsChecked = template.ShowPageBorder;
            txtBorderThickness.Text = template.BorderThickness.ToString("0.##", CultureInfo.InvariantCulture);
            txtBorderColor.Text = template.BorderColor;

            txtDocumentTitle.Text = template.DocumentTitle;
            txtSubtitle.Text = template.Subtitle;
            txtHeaderNote.Text = template.HeaderNote;
            SelectTag(cmbHeaderAlign, template.HeaderAlignment.ToString());
            chkShowLogo.IsChecked = template.ShowLogo;
            txtLogoHeight.Text = template.LogoHeightMm.ToString("0.##", CultureInfo.InvariantCulture);
            chkHeaderLine.IsChecked = template.ShowHeaderLine;
            txtHeaderBackground.Text = template.HeaderBackground;
            txtHeaderText.Text = template.HeaderTextColor;
            txtTitleColor.Text = template.TitleColor;

            SelectFont(cmbHeadingFont, template.HeadingFontFamily);
            txtHeadingSize.Text = template.HeadingFontSize.ToString("0.##", CultureInfo.InvariantCulture);
            chkHeadingBold.IsChecked = template.HeadingBold;
            SelectFont(cmbBodyFont, template.BodyFontFamily);
            txtBodySize.Text = template.BodyFontSize.ToString("0.##", CultureInfo.InvariantCulture);
            txtBodyColor.Text = template.BodyColor;
            txtMutedColor.Text = template.MutedColor;
            txtAccentColor.Text = template.AccentColor;
            txtTableHeaderBg.Text = template.TableHeaderBackground;
            txtTableHeaderFg.Text = template.TableHeaderColor;
            txtTableLine.Text = template.TableLineColor;
            chkTableBorders.IsChecked = template.ShowTableBorders;

            chkSerial.IsChecked = template.ShowSerial;
            chkDate.IsChecked = template.ShowDate;
            chkPatient.IsChecked = template.ShowPatientName;
            chkAge.IsChecked = template.ShowAgeGender;
            chkPhone.IsChecked = template.ShowPhone;
            chkCnic.IsChecked = template.ShowCnic;
            chkDoctor.IsChecked = template.ShowReferredBy;
            chkQr.IsChecked = template.ShowQrCode;
            chkPrice.IsChecked = template.ShowPrice;
            chkDiscount.IsChecked = template.ShowDiscount;
            chkNet.IsChecked = template.ShowNet;
            chkTotals.IsChecked = template.ShowTotals;
            chkPregnancy.IsChecked = template.ShowPregnancy;
            chkUnit.IsChecked = template.ShowUnit;
            chkRange.IsChecked = template.ShowReferenceRange;
            chkStatus.IsChecked = template.ShowStatus;
            chkCategories.IsChecked = template.ShowCategoryHeadings;
            chkEnd.IsChecked = template.ShowEndOfReport;

            chkCustomNote.IsChecked = template.ShowCustomNote;
            txtCustomNote.Text = template.CustomNote;
            chkSignature.IsChecked = template.ShowSignature;
            txtSignatureName.Text = template.SignatureName;
            txtSignatureTitle.Text = template.SignatureTitle;
            chkStamp.IsChecked = template.ShowStamp;
            txtStampText.Text = template.StampText;
            chkFooterAddress.IsChecked = template.ShowAddressInFooter;
            chkFooterContact.IsChecked = template.ShowContactInFooter;
            chkFooterLine.IsChecked = template.ShowFooterLine;
            txtFooterNote.Text = template.FooterNote;
            SelectTag(cmbFooterAlign, template.FooterAlignment.ToString());
            txtFooterBackground.Text = template.FooterBackground;

            _lastPaper = template.PaperSize;
            _loading = previous;
            UpdateCustomPanel();
            PaintSwatches();
        }

        private PdfTemplateSettings Build(bool strict)
        {
            _invalidBox = null;
            _invalidMessage = "";
            var template = _kind == PdfDocumentKind.Bill
                ? PdfTemplateSettings.CreateBillDefaults()
                : PdfTemplateSettings.CreateReportDefaults();
            template.Kind = _kind;
            template.PaperSize = ReadPaper();
            template.CustomWidthMm = Number(txtCustomWidth, "Custom width", 40, 500, _draft.CustomWidthMm, strict);
            template.CustomHeightMm = Number(txtCustomHeight, "Custom height", 40, 1200, _draft.CustomHeightMm, strict);
            template.CustomContinuous = chkCustomContinuous.IsChecked == true;
            template.PreferredPrinter = SelectedTag(cmbPrinter, "");

            double marginMax = template.IsNarrow ? 5 : 25;
            template.MarginTopMm = Number(txtMarginTop, "Top margin", 0, marginMax, _draft.MarginTopMm, strict);
            template.MarginRightMm = Number(txtMarginRight, "Right margin", 0, marginMax, _draft.MarginRightMm, strict);
            template.MarginBottomMm = Number(txtMarginBottom, "Bottom margin", 0, marginMax, _draft.MarginBottomMm, strict);
            template.MarginLeftMm = Number(txtMarginLeft, "Left margin", 0, marginMax, _draft.MarginLeftMm, strict);
            template.SectionSpacingMm = Number(txtSectionSpacing, "Section spacing", 0, 15, _draft.SectionSpacingMm, strict);
            template.CellPaddingMm = Number(txtCellPadding, "Cell padding", 0.3, 5, _draft.CellPaddingMm, strict);
            template.ShowPageBorder = chkPageBorder.IsChecked == true;
            template.BorderThickness = Number(txtBorderThickness, "Border thickness", 0.3, 4, _draft.BorderThickness, strict);
            template.BorderColor = ColorOf(txtBorderColor, "Border color", _draft.BorderColor, strict);

            template.DocumentTitle = txtDocumentTitle.Text.Trim();
            template.Subtitle = txtSubtitle.Text.Trim();
            template.HeaderNote = txtHeaderNote.Text.Trim();
            template.HeaderAlignment = ReadAlign(cmbHeaderAlign);
            template.ShowLogo = chkShowLogo.IsChecked == true;
            template.LogoHeightMm = Number(txtLogoHeight, "Logo height", 8, 40, _draft.LogoHeightMm, strict);
            template.ShowHeaderLine = chkHeaderLine.IsChecked == true;
            template.HeaderBackground = ColorOf(txtHeaderBackground, "Header background", _draft.HeaderBackground, strict);
            template.HeaderTextColor = ColorOf(txtHeaderText, "Hospital name color", _draft.HeaderTextColor, strict);
            template.TitleColor = ColorOf(txtTitleColor, "Title color", _draft.TitleColor, strict);

            template.HeadingFontFamily = cmbHeadingFont.SelectedItem as string ?? "Lato";
            template.HeadingFontSize = Number(txtHeadingSize, "Heading size", 8, 32, _draft.HeadingFontSize, strict);
            template.HeadingBold = chkHeadingBold.IsChecked == true;
            template.BodyFontFamily = cmbBodyFont.SelectedItem as string ?? "Lato";
            template.BodyFontSize = Number(txtBodySize, "Body size", 6, 18, _draft.BodyFontSize, strict);
            template.BodyColor = ColorOf(txtBodyColor, "Body color", _draft.BodyColor, strict);
            template.MutedColor = ColorOf(txtMutedColor, "Muted color", _draft.MutedColor, strict);
            template.AccentColor = ColorOf(txtAccentColor, "Accent color", _draft.AccentColor, strict);
            template.TableHeaderBackground = ColorOf(txtTableHeaderBg, "Table header background", _draft.TableHeaderBackground, strict);
            template.TableHeaderColor = ColorOf(txtTableHeaderFg, "Table header text", _draft.TableHeaderColor, strict);
            template.TableLineColor = ColorOf(txtTableLine, "Table line color", _draft.TableLineColor, strict);
            template.ShowTableBorders = chkTableBorders.IsChecked == true;

            template.ShowSerial = chkSerial.IsChecked == true;
            template.ShowDate = chkDate.IsChecked == true;
            template.ShowPatientName = chkPatient.IsChecked == true;
            template.ShowAgeGender = chkAge.IsChecked == true;
            template.ShowPhone = chkPhone.IsChecked == true;
            template.ShowCnic = chkCnic.IsChecked == true;
            template.ShowReferredBy = chkDoctor.IsChecked == true;
            template.ShowQrCode = chkQr.IsChecked == true;
            template.ShowPrice = chkPrice.IsChecked == true;
            template.ShowDiscount = chkDiscount.IsChecked == true;
            template.ShowNet = chkNet.IsChecked == true;
            template.ShowTotals = chkTotals.IsChecked == true;
            template.ShowPregnancy = chkPregnancy.IsChecked == true;
            template.ShowUnit = chkUnit.IsChecked == true;
            template.ShowReferenceRange = chkRange.IsChecked == true;
            template.ShowStatus = chkStatus.IsChecked == true;
            template.ShowCategoryHeadings = chkCategories.IsChecked == true;
            template.ShowEndOfReport = chkEnd.IsChecked == true;

            template.ShowCustomNote = chkCustomNote.IsChecked == true;
            template.CustomNote = txtCustomNote.Text.Trim();
            template.ShowSignature = chkSignature.IsChecked == true;
            template.SignatureName = txtSignatureName.Text.Trim();
            template.SignatureTitle = txtSignatureTitle.Text.Trim();
            template.ShowStamp = chkStamp.IsChecked == true;
            template.StampText = txtStampText.Text.Trim();
            template.ShowAddressInFooter = chkFooterAddress.IsChecked == true;
            template.ShowContactInFooter = chkFooterContact.IsChecked == true;
            template.ShowFooterLine = chkFooterLine.IsChecked == true;
            template.FooterNote = txtFooterNote.Text.Trim();
            template.FooterAlignment = ReadAlign(cmbFooterAlign);
            template.FooterBackground = ColorOf(txtFooterBackground, "Footer background", _draft.FooterBackground, strict);
            return template;
        }

        private void RenderPreview()
        {
            try
            {
                var template = Build(strict: false);
                var branding = LabProXSettingsStore.Current.Customer.Clone();
                branding.LogoFileName = _logoFileName;
                byte[]? png = PdfPreviewService.Render(_kind, template, branding);
                if (png == null || png.Length == 0)
                {
                    txtPreviewStatus.Text = "Preview did not return a page.";
                    return;
                }

                imgPreview.Source = LoadImage(png);
                string saved = _kind == PdfDocumentKind.Bill
                    ? "Sample bill at the selected paper size."
                    : "Sample laboratory report at the selected paper size.";
                txtPreviewStatus.Text = _dirty ? "Unsaved changes. " + saved : saved;
            }
            catch (Exception ex)
            {
                txtPreviewStatus.Text = "Preview could not be updated. " + Innermost(ex);
            }
        }

        private bool TrySave()
        {
            var template = Build(strict: true);
            if (_invalidBox != null)
            {
                MessageBox.Show(_invalidMessage, "Lab ProX", MessageBoxButton.OK, MessageBoxImage.Warning);
                _invalidBox.Focus();
                _invalidBox.SelectAll();
                return false;
            }

            template.Kind = _kind;
            template.Normalize();
            LabProXSettingsStore.SaveDocument(template);
            SaveLogo();
            _draft = template.Clone();
            _dirty = false;
            LoadForm(template);
            RenderPreview();
            txtPreviewStatus.Text = "Saved. Lab ProX footer stays on every PDF.";
            return true;
        }

        private void MarkChanged(object sender, RoutedEventArgs e)
        {
            if (_loading)
                return;
            if (ReferenceEquals(sender, cmbPaperSize))
                ApplyNarrowMarginsIfNeeded();
            _dirty = true;
            UpdateCustomPanel();
            PaintSwatches();
            SchedulePreview();
        }

        private void Text_Changed(object sender, TextChangedEventArgs e) => MarkChanged(sender, e);

        private void Selection_Changed(object sender, SelectionChangedEventArgs e) => MarkChanged(sender, e);

        private void ColorBox_GotFocus(object sender, RoutedEventArgs e) => _colorTarget = sender as TextBox;

        private void PresetColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string hex)
                return;
            (_colorTarget ?? txtTitleColor).Text = hex;
        }

        private void btnBrowseLogo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Upload hospital logo",
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
                chkShowLogo.IsChecked = true;
                ShowLogo();
                _dirty = true;
                SchedulePreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not copy the logo.\n" + ex.Message, "Lab ProX",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnClearLogo_Click(object sender, RoutedEventArgs e)
        {
            _logoFileName = "";
            ShowLogo();
            _dirty = true;
            SchedulePreview();
        }

        private void SaveLogo()
        {
            var branding = LabProXSettingsStore.Current.Customer.Clone();
            branding.LogoFileName = _logoFileName;
            branding.Normalize();
            LabProXSettingsStore.SaveCustomer(branding);
            _logoFileName = branding.LogoFileName;
            ShowLogo();
        }

        private void ShowLogo()
        {
            string? path = LabProXSettingsStore.ResolveLogoPath(new CustomerBranding { LogoFileName = _logoFileName });
            if (path == null)
            {
                imgHospitalLogo.Source = null;
                txtNoLogo.Visibility = Visibility.Visible;
                txtNoLogo.Text = "No logo uploaded.";
                txtLogoFile.Text = "";
                return;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            imgHospitalLogo.Source = image;
            txtNoLogo.Visibility = Visibility.Collapsed;
            txtLogoFile.Text = Path.GetFileName(path);
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e) => RenderPreview();

        private void btnSave_Click(object sender, RoutedEventArgs e) => TrySave();

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            _draft = _kind == PdfDocumentKind.Bill
                ? PdfTemplateSettings.CreateBillDefaults()
                : PdfTemplateSettings.CreateReportDefaults();
            LoadForm(_draft);
            _dirty = true;
            RenderPreview();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_closeConfirmed || !_dirty)
            {
                _previewTimer.Stop();
                base.OnClosing(e);
                return;
            }

            var choice = MessageBox.Show(
                "Save this PDF layout before closing?",
                "Lab ProX",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (choice == MessageBoxResult.Yes && !TrySave())
            {
                e.Cancel = true;
                return;
            }

            _closeConfirmed = true;
            _previewTimer.Stop();
            base.OnClosing(e);
        }

        private void SchedulePreview()
        {
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        private void ApplyNarrowMarginsIfNeeded()
        {
            var size = ReadPaper();
            bool narrow = IsNarrow(size, ReadLoose(txtCustomWidth, _draft.CustomWidthMm));
            bool wasNarrow = IsNarrow(_lastPaper, _draft.CustomWidthMm);
            _lastPaper = size;
            if (!narrow || wasNarrow)
                return;
            Tighten(txtMarginTop);
            Tighten(txtMarginRight);
            Tighten(txtMarginBottom);
            Tighten(txtMarginLeft);
        }

        private static bool IsNarrow(PaperSizeKind size, double customWidth) =>
            size is PaperSizeKind.Thermal58 or PaperSizeKind.Thermal80 || (size == PaperSizeKind.Custom && customWidth < 100);

        private void Tighten(TextBox box)
        {
            if (ReadLoose(box, 8) > 5)
                box.Text = "2.5";
        }

        private void UpdateCustomPanel()
        {
            bool custom = ReadPaper() == PaperSizeKind.Custom;
            pnlCustomSize.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            pnlCustomHeight.Visibility = custom && chkCustomContinuous.IsChecked != true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void PaintSwatches()
        {
            Paint(swatchBorder, txtBorderColor, "#94A3B8");
            Paint(swatchHeaderBg, txtHeaderBackground, "#FFFFFF");
            Paint(swatchHeaderText, txtHeaderText, "#0F172A");
            Paint(swatchTitle, txtTitleColor, "#1457B8");
            Paint(swatchBody, txtBodyColor, "#1E293B");
            Paint(swatchMuted, txtMutedColor, "#64748B");
            Paint(swatchAccent, txtAccentColor, "#1457B8");
            Paint(swatchTableBg, txtTableHeaderBg, "#E8EEF5");
            Paint(swatchTableFg, txtTableHeaderFg, "#0F172A");
            Paint(swatchTableLine, txtTableLine, "#CBD5E1");
            Paint(swatchFooterBg, txtFooterBackground, "#FFFFFF");
        }

        private static void Paint(Border swatch, TextBox box, string fallback)
        {
            string hex = PdfColors.TryHex(box.Text, out var parsed) ? parsed : fallback;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex)!;
                swatch.Background = new SolidColorBrush(color);
            }
            catch
            {
                swatch.Background = Brushes.LightGray;
            }
        }

        private PaperSizeKind ReadPaper()
        {
            string tag = SelectedTag(cmbPaperSize, "A4");
            return Enum.TryParse(tag, out PaperSizeKind size) ? size : PaperSizeKind.A4;
        }

        private static PdfTextAlign ReadAlign(ComboBox box)
        {
            string tag = SelectedTag(box, "Left");
            return Enum.TryParse(tag, out PdfTextAlign align) ? align : PdfTextAlign.Left;
        }

        private double Number(TextBox box, string label, double min, double max, double fallback, bool strict)
        {
            if (TryParse(box.Text, out var value) && value >= min && value <= max)
                return value;
            if (strict && _invalidBox == null)
            {
                _invalidBox = box;
                _invalidMessage = $"{label} must be a number from {min:0.##} to {max:0.##}.";
            }
            return Math.Clamp(fallback, min, max);
        }

        private string ColorOf(TextBox box, string label, string fallback, bool strict)
        {
            if (PdfColors.TryHex(box.Text, out var hex))
                return hex;
            if (strict && _invalidBox == null)
            {
                _invalidBox = box;
                _invalidMessage = $"{label} must be a color such as #1457B8.";
            }
            string safe = PdfColors.Normalize(fallback, "#1457B8");
            return safe;
        }

        private void EnsurePrinter(string? printer)
        {
            if (string.IsNullOrWhiteSpace(printer) || HasTag(cmbPrinter, printer))
                return;
            cmbPrinter.Items.Add(new ComboBoxItem { Content = printer + " (saved)", Tag = printer });
        }

        private static void SelectFont(ComboBox box, string? family)
        {
            string name = string.IsNullOrWhiteSpace(family) ? "Lato" : family;
            if (!box.Items.Contains(name))
                box.Items.Add(name);
            box.SelectedItem = name;
        }

        private static void SelectTag(ComboBox box, string tag)
        {
            foreach (var item in box.Items)
            {
                if (item is ComboBoxItem option && (option.Tag as string) == tag)
                {
                    box.SelectedItem = option;
                    return;
                }
            }
            if (box.Items.Count > 0)
                box.SelectedIndex = 0;
        }

        private static string SelectedTag(ComboBox box, string fallback) =>
            (box.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;

        private static bool HasTag(ComboBox box, string tag)
        {
            foreach (var item in box.Items)
            {
                if (item is ComboBoxItem option && (option.Tag as string) == tag)
                    return true;
            }
            return false;
        }

        private static double ReadLoose(TextBox box, double fallback) =>
            TryParse(box.Text, out var value) ? value : fallback;

        private static bool TryParse(string? text, out double value)
        {
            text = (text ?? "").Trim().Replace(',', '.');
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static BitmapImage LoadImage(byte[] bytes)
        {
            var image = new BitmapImage();
            using var stream = new MemoryStream(bytes);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static string Innermost(Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return ex.Message;
        }
    }
}
