using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LabReportApp.Config;
using LabReportApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LabReportApp.Services
{
    public static class BillService
    {
        public static string GenerateBill(Patient patient, List<TestResultItem> tests, byte[] qrPngBytes)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppSettings.LocalBillsFolder);
            Directory.CreateDirectory(folder);
            string filePath = Path.Combine(folder, $"{patient.SerialNumber}_Bill.pdf");

            var settings = LabProXSettingsStore.Current;
            BuildDocument(patient, tests, qrPngBytes, settings.Bill, settings.Customer).GeneratePdf(filePath);
            return filePath;
        }

        public static IDocument BuildDocument(
            Patient patient,
            List<TestResultItem> tests,
            byte[] qrPngBytes,
            PdfTemplateSettings template,
            CustomerBranding branding)
        {
            var ctx = PdfDocumentComposer.CreateFor(template, branding, patient, qrPngBytes);
            decimal subtotal = tests.Sum(t => t.NetPrice);
            decimal overallDiscountAmount = Math.Round(subtotal * patient.DiscountPercentage / 100m, 2);
            decimal grandTotal = subtotal - overallDiscountAmount;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    PdfDocumentComposer.ApplyPage(page, ctx);
                    PdfDocumentComposer.ComposeHeader(page, ctx);

                    page.Content().PaddingVertical(ctx.Narrow ? 4 : 8).Column(col =>
                    {
                        var fields = new List<(string Label, string Value)>();
                        if (ctx.Template.ShowSerial) fields.Add(("Serial No", patient.SerialNumber));
                        if (ctx.Template.ShowDate) fields.Add(("Date", patient.CreatedAt.ToString("dd-MMM-yyyy hh:mm tt")));
                        if (ctx.Template.ShowPatientName) fields.Add(("Patient Name", patient.Name));
                        if (ctx.Template.ShowAgeGender) fields.Add(("Age/Gender", $"{patient.AgeDisplay} / {patient.Gender}"));
                        if (ctx.Template.ShowPhone) fields.Add(("Contact No", patient.PhoneNumber));
                        if (ctx.Template.ShowCnic) fields.Add(("CNIC", patient.Cnic));
                        if (ctx.Template.ShowReferredBy) fields.Add(("Referred By", patient.ReferredBy));
                        PdfDocumentComposer.ComposeFields(col, ctx, fields);

                        col.Item().PaddingTop(PdfDocumentComposer.Mm(ctx.Template.SectionSpacingMm)).Table(table =>
                        {
                            var columns = BillColumns(ctx);
                            table.ColumnsDefinition(definition =>
                            {
                                foreach (var column in columns)
                                    definition.RelativeColumn(column.Weight);
                            });

                            table.Header(header =>
                            {
                                foreach (var column in columns)
                                {
                                    var cell = header.Cell().Element(c => PdfDocumentComposer.HeaderCell(c, ctx));
                                    if (column.Right)
                                        cell.AlignRight().Text(column.Header);
                                    else
                                        cell.Text(column.Header);
                                }
                            });

                            foreach (var item in tests)
                            {
                                foreach (var column in columns)
                                {
                                    var cell = table.Cell().Element(c => PdfDocumentComposer.BodyCell(c, ctx));
                                    string value = column.Value(item);
                                    if (column.Right)
                                        cell.AlignRight().Text(value);
                                    else
                                        cell.Text(value);
                                }
                            }
                        });

                        if (ctx.Template.ShowTotals)
                        {
                            void Totals(ColumnDescriptor totals)
                            {
                                totals.Item().Row(row =>
                                {
                                    row.RelativeItem().Text("Subtotal:");
                                    row.ConstantItem(ctx.VeryNarrow ? 70 : 90).AlignRight().Text($"Rs. {subtotal:N0}");
                                });

                                if (patient.DiscountPercentage > 0)
                                {
                                    totals.Item().Row(row =>
                                    {
                                        row.RelativeItem().Text($"Overall Discount ({patient.DiscountPercentage:0.##}%):");
                                        row.ConstantItem(ctx.VeryNarrow ? 70 : 90).AlignRight().Text($"-Rs. {overallDiscountAmount:N0}").FontColor(Colors.Red.Darken1);
                                    });
                                }

                                totals.Item().PaddingTop(4).BorderTop(1).BorderColor(ctx.TableLineColor).PaddingTop(4).Row(row =>
                                {
                                    var label = row.RelativeItem().Text("Total Payable:").FontSize(ctx.Narrow ? ctx.BodySize : ctx.BodySize + 2);
                                    if (ctx.UseBodyBold) label.Bold();
                                    var amount = row.ConstantItem(ctx.VeryNarrow ? 70 : 90).AlignRight().Text($"Rs. {grandTotal:N0}").FontSize(ctx.Narrow ? ctx.BodySize : ctx.BodySize + 2);
                                    if (ctx.UseBodyBold) amount.Bold();
                                });

                                if (tests.Any(t => t.EntryRole == "PanelParameter" && t.DiscountAmount > 0))
                                {
                                    totals.Item().PaddingTop(6).Text("Discounts on tests included in a panel are taken off that panel's package price.")
                                        .FontSize(Math.Max(6.5f, ctx.BodySize - 1.5f))
                                        .FontColor(ctx.MutedColor);
                                }
                            }

                            var totalsItem = col.Item().PaddingTop(PdfDocumentComposer.Mm(ctx.Template.SectionSpacingMm + 1));
                            if (ctx.Narrow)
                                totalsItem.Column(Totals);
                            else
                                totalsItem.AlignRight().Width(230).Column(Totals);
                        }

                        PdfDocumentComposer.ComposeClosing(col, ctx);
                    });

                    PdfDocumentComposer.ComposeFooter(page, ctx);
                });
            });
        }

        private static List<(string Header, float Weight, bool Right, Func<TestResultItem, string> Value)> BillColumns(PdfRenderContext ctx)
        {
            var template = ctx.Template;
            bool showPrice = template.ShowPrice && !ctx.VeryNarrow;
            bool showDiscount = template.ShowDiscount && !ctx.Narrow;
            bool showNet = template.ShowNet || (ctx.VeryNarrow && (template.ShowPrice || template.ShowNet || template.ShowTotals));

            var columns = new List<(string Header, float Weight, bool Right, Func<TestResultItem, string> Value)>
            {
                ("Test Name", 4, false, item => item.EntryRole == "PanelParameter" ? "• " + item.TestName : item.TestName)
            };
            if (showPrice)
            {
                columns.Add(("Price", 1.7f, true, item =>
                    item.EntryRole == "PanelParameter" ? "Included" : item.Price.ToString("N0")));
            }
            if (showDiscount)
                columns.Add(("Discount", 1.5f, true, item => item.DiscountText));
            if (showNet)
            {
                columns.Add(("Net", 1.7f, true, item =>
                    item.EntryRole == "PanelParameter" ? "—" : item.NetPrice.ToString("N0")));
            }
            return columns;
        }
    }
}
