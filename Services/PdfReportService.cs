using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LabReportApp.Config;
using LabReportApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace LabReportApp.Services
{
    public static class PdfReportService
    {
        public static string GenerateReport(Patient patient, List<TestResultItem> results, byte[] qrPngBytes)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppSettings.LocalReportsFolder);
            Directory.CreateDirectory(folder);
            string filePath = Path.Combine(folder, $"{patient.SerialNumber}.pdf");

            var settings = LabProXSettingsStore.Current;
            BuildDocument(patient, results, qrPngBytes, settings.Report, settings.Customer).GeneratePdf(filePath);
            return filePath;
        }

        public static IDocument BuildDocument(
            Patient patient,
            List<TestResultItem> results,
            byte[] qrPngBytes,
            PdfTemplateSettings template,
            CustomerBranding branding)
        {
            var ctx = PdfDocumentComposer.CreateFor(template, branding, patient, qrPngBytes);
            var resultRows = results.Where(row => row.IsResultRow).ToList();
            var grouped = resultRows
                .GroupBy(row => string.IsNullOrWhiteSpace(row.CategoryHeading) ? "General" : row.CategoryHeading)
                .ToList();

            IEnumerable<IGrouping<string, TestResultItem>> sections = ctx.Template.ShowCategoryHeadings
                ? grouped
                : resultRows.GroupBy(_ => "");

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
                        if (ctx.Template.ShowPregnancy &&
                            !string.IsNullOrWhiteSpace(patient.PregnancyStatus) &&
                            !string.Equals(patient.PregnancyStatus, "Not Applicable", StringComparison.OrdinalIgnoreCase))
                        {
                            fields.Add(("Pregnancy", patient.PregnancyStatus));
                        }
                        PdfDocumentComposer.ComposeFields(col, ctx, fields);

                        foreach (var group in sections)
                        {
                            if (ctx.Template.ShowCategoryHeadings && !string.IsNullOrWhiteSpace(group.Key))
                            {
                                col.Item().PaddingTop(PdfDocumentComposer.Mm(ctx.Template.SectionSpacingMm + 1)).Text(text =>
                                {
                                    var span = text.Span(group.Key)
                                        .FontFamily(ctx.HeadingFont)
                                        .FontSize(ctx.Narrow ? Math.Min(ctx.HeadingSize, 11) : Math.Min(ctx.HeadingSize, 14))
                                        .FontColor(ctx.AccentColor);
                                    if (ctx.UseHeadingBold)
                                        span.Bold();
                                });
                            }

                            var columns = ReportColumns(ctx);
                            col.Item().PaddingTop(PdfDocumentComposer.Mm(Math.Max(1, ctx.Template.SectionSpacingMm))).Table(table =>
                            {
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

                                foreach (var row in group)
                                {
                                    foreach (var column in columns)
                                    {
                                        var cell = table.Cell().Element(c => PdfDocumentComposer.BodyCell(c, ctx));
                                        if (column.Header == "Status")
                                        {
                                            string flag = row.AbnormalFlag ?? "";
                                            var status = cell.Text(flag);
                                            if (flag is "LOW" or "HIGH")
                                            {
                                                status.FontColor(Color.FromHex("#B91C1C"));
                                                if (ctx.UseBodyBold)
                                                    status.Bold();
                                            }
                                        }
                                        else
                                        {
                                            string value = column.Value(row);
                                            if (column.Right)
                                                cell.AlignRight().Text(value);
                                            else
                                                cell.Text(value);
                                        }
                                    }
                                }
                            });
                        }

                        PdfDocumentComposer.ComposeClosing(col, ctx);

                        if (ctx.Template.ShowEndOfReport)
                        {
                            col.Item().PaddingTop(PdfDocumentComposer.Mm(ctx.Template.SectionSpacingMm + 1)).AlignCenter().Text(text =>
                            {
                                var span = text.Span("— End of Report —")
                                    .FontSize(Math.Max(7, ctx.BodySize - 1))
                                    .FontColor(ctx.MutedColor);
                                if (ctx.UseItalic)
                                    span.Italic();
                            });
                        }
                    });

                    PdfDocumentComposer.ComposeFooter(page, ctx);
                });
            });
        }

        private static List<(string Header, float Weight, bool Right, Func<TestResultItem, string> Value)> ReportColumns(PdfRenderContext ctx)
        {
            var template = ctx.Template;
            bool unitColumn = template.ShowUnit && !ctx.VeryNarrow;
            bool rangeColumn = template.ShowReferenceRange && ctx.Template.PaperWidthMm >= 130;
            bool statusColumn = template.ShowStatus;

            string ResultText(TestResultItem row)
            {
                string text = row.Result ?? "";
                if (!unitColumn && template.ShowUnit && !string.IsNullOrWhiteSpace(row.Unit))
                    text = string.IsNullOrWhiteSpace(text) ? row.Unit : text + " " + row.Unit;
                if (!rangeColumn && template.ShowReferenceRange && !string.IsNullOrWhiteSpace(row.NormalRange))
                    text = string.IsNullOrWhiteSpace(text) ? row.NormalRange : text + "\n" + row.NormalRange;
                return text;
            }

            var columns = new List<(string Header, float Weight, bool Right, Func<TestResultItem, string> Value)>
            {
                ("Test Name", 2.6f, false, row => row.TestName),
                ("Result", 1.6f, false, ResultText)
            };
            if (unitColumn)
                columns.Add(("Unit", 1.2f, false, row => row.Unit));
            if (rangeColumn)
                columns.Add(("Reference Range", 2.2f, false, row => row.NormalRange));
            if (statusColumn)
                columns.Add(("Status", 1.3f, false, row => row.AbnormalFlag ?? ""));
            return columns;
        }
    }
}
