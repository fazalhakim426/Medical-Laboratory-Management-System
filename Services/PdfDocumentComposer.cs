using System;
using LabReportApp.Config;
using LabReportApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LabReportApp.Services
{
    public sealed class PdfRenderContext
    {
        public PdfTemplateSettings Template { get; init; } = new();
        public CustomerBranding Branding { get; init; } = new();
        public string? LogoPath { get; init; }
        public byte[]? QrPng { get; init; }

        public string BodyFont { get; init; } = "Lato";
        public string HeadingFont { get; init; } = "Lato";
        public float BodySize { get; init; } = 10;
        public float HeadingSize { get; init; } = 16;
        public bool UseHeadingBold { get; init; } = true;
        public bool UseBodyBold { get; init; } = true;
        public bool UseItalic { get; init; } = true;

        public Color TitleColor { get; init; }
        public Color HeaderText { get; init; }
        public Color HeaderBackground { get; init; }
        public Color BodyColor { get; init; }
        public Color MutedColor { get; init; }
        public Color AccentColor { get; init; }
        public Color TableHeaderBackground { get; init; }
        public Color TableHeaderColor { get; init; }
        public Color TableLineColor { get; init; }
        public Color BorderColor { get; init; }
        public Color FooterBackground { get; init; }

        public string HospitalName { get; init; } = "";
        public string AddressLine { get; init; } = "";
        public string ContactLine { get; init; } = "";

        public bool HasQr => Template.ShowQrCode && QrPng is { Length: > 0 };
        public bool Narrow => Template.IsNarrow;
        public bool VeryNarrow => Template.IsVeryNarrow;
    }

    /// <summary>
    /// Shared page chrome. The Lab ProX footer is composed here and is not optional.
    /// </summary>
    public static class PdfDocumentComposer
    {
        public static float Mm(double millimeters) => (float)(millimeters * 72.0 / 25.4);

        public static PdfRenderContext Create(PdfTemplateSettings template, CustomerBranding branding, byte[]? qr)
        {
            PdfFontRegistry.Ensure();
            var layout = template.Clone();
            layout.Normalize();
            var brand = branding.Clone();
            brand.Normalize();

            string headingFont = PdfFontRegistry.Resolve(layout.HeadingFontFamily);
            string bodyFont = PdfFontRegistry.Resolve(layout.BodyFontFamily);
            float body = (float)layout.BodyFontSize;
            float heading = (float)layout.HeadingFontSize;
            if (layout.PaperWidthMm <= 60)
            {
                body = Math.Min(body, 8);
                heading = Math.Min(heading, 11);
            }
            else if (layout.PaperWidthMm <= 90)
            {
                body = Math.Min(body, 9);
                heading = Math.Min(heading, 13);
            }
            else if (layout.PaperWidthMm <= 160)
            {
                heading = Math.Min(heading, 16);
            }

            return new PdfRenderContext
            {
                Template = layout,
                Branding = brand,
                LogoPath = layout.ShowLogo ? LabProXSettingsStore.ResolveLogoPath(brand) : null,
                QrPng = qr,
                BodyFont = bodyFont,
                HeadingFont = headingFont,
                BodySize = Math.Clamp(body, 6, 18),
                HeadingSize = Math.Clamp(heading, 8, 28),
                UseHeadingBold = layout.HeadingBold && PdfFontRegistry.Supports(headingFont, true, false),
                UseBodyBold = PdfFontRegistry.Supports(bodyFont, true, false),
                UseItalic = PdfFontRegistry.Supports(bodyFont, false, true),
                TitleColor = Color.FromHex(layout.TitleColor),
                HeaderText = Color.FromHex(layout.HeaderTextColor),
                HeaderBackground = Color.FromHex(layout.HeaderBackground),
                BodyColor = Color.FromHex(layout.BodyColor),
                MutedColor = Color.FromHex(layout.MutedColor),
                AccentColor = Color.FromHex(layout.AccentColor),
                TableHeaderBackground = Color.FromHex(layout.TableHeaderBackground),
                TableHeaderColor = Color.FromHex(layout.TableHeaderColor),
                TableLineColor = Color.FromHex(layout.TableLineColor),
                BorderColor = Color.FromHex(layout.BorderColor),
                FooterBackground = Color.FromHex(layout.FooterBackground),
                HospitalName = brand.ResolvedHospitalName(null),
                AddressLine = brand.ResolvedAddress(),
                ContactLine = brand.ResolvedContact()
            };
        }

        public static PdfRenderContext CreateFor(PdfTemplateSettings template, CustomerBranding branding, Models.Patient? patient, byte[]? qr) =>
            WithPatient(Create(template, branding, qr), patient);

        public static PdfRenderContext WithPatient(PdfRenderContext ctx, Models.Patient? patient)
        {
            if (patient == null)
                return ctx;
            return new PdfRenderContext
            {
                Template = ctx.Template,
                Branding = ctx.Branding,
                LogoPath = ctx.LogoPath,
                QrPng = ctx.QrPng,
                BodyFont = ctx.BodyFont,
                HeadingFont = ctx.HeadingFont,
                BodySize = ctx.BodySize,
                HeadingSize = ctx.HeadingSize,
                UseHeadingBold = ctx.UseHeadingBold,
                UseBodyBold = ctx.UseBodyBold,
                UseItalic = ctx.UseItalic,
                TitleColor = ctx.TitleColor,
                HeaderText = ctx.HeaderText,
                HeaderBackground = ctx.HeaderBackground,
                BodyColor = ctx.BodyColor,
                MutedColor = ctx.MutedColor,
                AccentColor = ctx.AccentColor,
                TableHeaderBackground = ctx.TableHeaderBackground,
                TableHeaderColor = ctx.TableHeaderColor,
                TableLineColor = ctx.TableLineColor,
                BorderColor = ctx.BorderColor,
                FooterBackground = ctx.FooterBackground,
                HospitalName = ctx.Branding.ResolvedHospitalName(patient),
                AddressLine = ctx.AddressLine,
                ContactLine = ctx.ContactLine
            };
        }

        public static void ApplyPage(PageDescriptor page, PdfRenderContext ctx)
        {
            var template = ctx.Template;
            switch (template.PaperSize)
            {
                case PaperSizeKind.A4:
                    page.Size(PageSizes.A4);
                    break;
                case PaperSizeKind.A5:
                    page.Size(PageSizes.A5);
                    break;
                case PaperSizeKind.Thermal58:
                    page.ContinuousSize(58, Unit.Millimetre);
                    break;
                case PaperSizeKind.Thermal80:
                    page.ContinuousSize(80, Unit.Millimetre);
                    break;
                default:
                    if (template.CustomContinuous)
                        page.ContinuousSize((float)template.CustomWidthMm, Unit.Millimetre);
                    else
                        page.Size((float)template.CustomWidthMm, (float)template.CustomHeightMm, Unit.Millimetre);
                    break;
            }

            page.MarginTop((float)template.MarginTopMm, Unit.Millimetre);
            page.MarginRight((float)template.MarginRightMm, Unit.Millimetre);
            page.MarginBottom((float)template.MarginBottomMm, Unit.Millimetre);
            page.MarginLeft((float)template.MarginLeftMm, Unit.Millimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(style => style
                .FontFamily(ctx.BodyFont)
                .FontSize(ctx.BodySize)
                .FontColor(ctx.BodyColor));

            if (template.ShowPageBorder)
            {
                // The border must wrap a real child or QuestPDF rejects the page.
                page.Foreground()
                    .Border((float)template.BorderThickness)
                    .BorderColor(ctx.BorderColor)
                    .ExtendHorizontal()
                    .ExtendVertical()
                    .Text(" ")
                    .FontSize(1)
                    .FontColor(Colors.White);
            }
        }

        public static void ComposeHeader(PageDescriptor page, PdfRenderContext ctx)
        {
            page.Header().Background(ctx.HeaderBackground).PaddingBottom(3).Column(col =>
            {
                DrawIdentity(col, ctx);
                if (ctx.Template.ShowHeaderLine)
                    col.Item().PaddingTop(4).LineHorizontal(0.8f).LineColor(ctx.AccentColor);
            });
        }

        public static void ComposeFields(ColumnDescriptor col, PdfRenderContext ctx, System.Collections.Generic.IReadOnlyList<(string Label, string Value)> fields)
        {
            var visible = new System.Collections.Generic.List<(string Label, string Value)>();
            foreach (var field in fields)
            {
                if (!string.IsNullOrWhiteSpace(field.Value))
                    visible.Add((field.Label, field.Value.Trim()));
            }
            if (visible.Count == 0)
                return;

            bool stack = ctx.Template.PaperWidthMm < 160;
            float pad = ctx.Narrow ? 4 : 6;
            col.Item().PaddingTop(Mm(ctx.Template.SectionSpacingMm)).Border(0.8f).BorderColor(ctx.BorderColor).Padding(pad).Column(box =>
            {
                if (stack)
                {
                    foreach (var field in visible)
                        box.Item().PaddingBottom(1).Text(text => WriteField(text, ctx, field.Label, field.Value));
                    return;
                }

                for (int i = 0; i < visible.Count; i += 2)
                {
                    var left = visible[i];
                    bool hasRight = i + 1 < visible.Count;
                    var right = hasRight ? visible[i + 1] : default;
                    box.Item().PaddingBottom(1).Row(row =>
                    {
                        row.RelativeItem().Text(text => WriteField(text, ctx, left.Label, left.Value));
                        row.RelativeItem().Text(text =>
                        {
                            if (hasRight)
                                WriteField(text, ctx, right.Label, right.Value);
                            else
                                text.Span(" ");
                        });
                    });
                }
            });
        }

        public static void ComposeClosing(ColumnDescriptor col, PdfRenderContext ctx)
        {
            var template = ctx.Template;
            if (template.ShowCustomNote && !string.IsNullOrWhiteSpace(template.CustomNote))
            {
                col.Item().PaddingTop(Mm(Math.Max(template.SectionSpacingMm, 2))).Text(text =>
                {
                    var span = text.Span(template.CustomNote.Trim())
                        .FontFamily(ctx.BodyFont)
                        .FontSize(Math.Max(7, ctx.BodySize - 1))
                        .FontColor(ctx.MutedColor);
                    if (ctx.UseItalic)
                        span.Italic();
                });
            }

            bool signature = template.ShowSignature;
            bool stamp = template.ShowStamp && !string.IsNullOrWhiteSpace(template.StampText);
            if (!signature && !stamp)
                return;

            col.Item().PaddingTop(Mm(Math.Max(template.SectionSpacingMm, 4))).Row(row =>
            {
                if (signature)
                {
                    row.RelativeItem().Column(block =>
                    {
                        block.Item().PaddingTop(ctx.Narrow ? 10 : 18).Width(ctx.Narrow ? 120 : 170)
                            .LineHorizontal(0.7f).LineColor(ctx.BodyColor);
                        if (!string.IsNullOrWhiteSpace(template.SignatureName))
                        {
                            block.Item().PaddingTop(2).Text(text =>
                            {
                                var span = text.Span(template.SignatureName.Trim()).FontFamily(ctx.BodyFont).FontSize(ctx.BodySize).FontColor(ctx.BodyColor);
                                if (ctx.UseBodyBold)
                                    span.Bold();
                            });
                        }
                        if (!string.IsNullOrWhiteSpace(template.SignatureTitle))
                        {
                            block.Item().Text(template.SignatureTitle.Trim())
                                .FontSize(Math.Max(7, ctx.BodySize - 1))
                                .FontColor(ctx.MutedColor);
                        }
                    });
                }

                if (stamp)
                {
                    row.ConstantItem(ctx.Narrow ? 88 : 130).AlignMiddle()
                        .Border(1.4f).BorderColor(ctx.AccentColor).Padding(6).AlignCenter()
                        .Text(text =>
                        {
                            var span = text.Span(template.StampText.Trim())
                                .FontFamily(ctx.BodyFont)
                                .FontSize(ctx.BodySize)
                                .FontColor(ctx.AccentColor);
                            if (ctx.UseBodyBold)
                                span.Bold();
                        });
                }
            });
        }

        public static void ComposeFooter(PageDescriptor page, PdfRenderContext ctx)
        {
            var template = ctx.Template;
            page.Footer().Column(col =>
            {
                bool address = template.ShowAddressInFooter && !string.IsNullOrWhiteSpace(ctx.AddressLine);
                bool contact = template.ShowContactInFooter && !string.IsNullOrWhiteSpace(ctx.ContactLine);
                bool note = !string.IsNullOrWhiteSpace(template.FooterNote);
                if (address || contact || note)
                {
                    col.Item().Background(ctx.FooterBackground).PaddingTop(2).Column(block =>
                    {
                        if (template.ShowFooterLine)
                            block.Item().PaddingBottom(3).LineHorizontal(0.6f).LineColor(ctx.TableLineColor);
                        if (address)
                            AddLine(block, template.FooterAlignment, ctx.AddressLine, ctx.BodyFont, Math.Max(7, ctx.BodySize - 1), ctx.MutedColor, false, false);
                        if (contact)
                            AddLine(block, template.FooterAlignment, ctx.ContactLine, ctx.BodyFont, Math.Max(7, ctx.BodySize - 1), ctx.MutedColor, false, false);
                        if (note)
                            AddLine(block, template.FooterAlignment, template.FooterNote, ctx.BodyFont, Math.Max(7, ctx.BodySize - 1), ctx.MutedColor, false, ctx.UseItalic);
                    });
                }

                // Permanent product footer. Customer template flags are not consulted.
                float size = ctx.VeryNarrow ? 6.2f : ctx.Narrow ? 7f : 8f;
                float markSize = size + 4;
                byte[]? mark = null;
                try
                {
                    mark = LabProXLogo.GetPng();
                }
                catch
                {
                    mark = null;
                }

                col.Item().PaddingTop(3).Background(Color.FromHex("#E8EEF5")).PaddingVertical(3).PaddingHorizontal(4).Row(row =>
                {
                    if (mark != null)
                    {
                        row.ConstantItem(markSize).PaddingRight(4).Height(markSize).AlignMiddle().Image(mark).FitArea();
                    }

                    row.RelativeItem().AlignMiddle().AlignCenter().Text(LabProXBrand.FooterText)
                        .FontFamily("Lato")
                        .FontSize(size)
                        .FontColor(Color.FromHex("#1E293B"));
                });
            });
        }

        public static IContainer HeaderCell(IContainer container, PdfRenderContext ctx)
        {
            var template = ctx.Template;
            if (template.ShowTableBorders)
                container = container.Border(0.4f).BorderColor(ctx.TableLineColor);
            container = container.Background(ctx.TableHeaderBackground).Padding(Mm(template.CellPaddingMm));
            return container.DefaultTextStyle(style =>
            {
                var next = style.FontFamily(ctx.BodyFont).FontSize(ctx.BodySize).FontColor(ctx.TableHeaderColor);
                return ctx.UseBodyBold ? next.Bold() : next;
            });
        }

        public static IContainer BodyCell(IContainer container, PdfRenderContext ctx)
        {
            var template = ctx.Template;
            container = template.ShowTableBorders
                ? container.Border(0.4f).BorderColor(ctx.TableLineColor)
                : container.BorderBottom(0.4f).BorderColor(ctx.TableLineColor);
            return container.Padding(Mm(template.CellPaddingMm));
        }

        private static void DrawIdentity(ColumnDescriptor col, PdfRenderContext ctx)
        {
            var template = ctx.Template;
            var align = template.HeaderAlignment;
            bool logo = !string.IsNullOrWhiteSpace(ctx.LogoPath);
            float logoMm = (float)template.LogoHeightMm;
            if (ctx.VeryNarrow) logoMm = Math.Min(logoMm, 12);
            else if (ctx.Narrow) logoMm = Math.Min(logoMm, 16);
            float logoPt = Mm(logoMm);

            void Lines(ColumnDescriptor text)
            {
                AddLine(text, align, ctx.HospitalName, ctx.HeadingFont, ctx.HeadingSize, ctx.HeaderText, ctx.UseHeadingBold, false);
                AddLine(text, align, ctx.Branding.Tagline, ctx.BodyFont, Math.Max(7, ctx.BodySize), ctx.MutedColor, false, ctx.UseItalic);
                AddLine(text, align, ctx.AddressLine, ctx.BodyFont, Math.Max(7, ctx.BodySize - 0.5f), ctx.MutedColor, false, false);
                AddLine(text, align, ctx.ContactLine, ctx.BodyFont, Math.Max(7, ctx.BodySize - 0.5f), ctx.MutedColor, false, false);
                if (!string.IsNullOrWhiteSpace(ctx.Branding.RegistrationNumber))
                    AddLine(text, align, "Reg. " + ctx.Branding.RegistrationNumber.Trim(), ctx.BodyFont, Math.Max(7, ctx.BodySize - 0.5f), ctx.MutedColor, false, false);
                if (!string.IsNullOrWhiteSpace(ctx.Branding.WorkingHours))
                    AddLine(text, align, ctx.Branding.WorkingHours.Trim(), ctx.BodyFont, Math.Max(7, ctx.BodySize - 0.5f), ctx.MutedColor, false, false);
                if (!string.IsNullOrWhiteSpace(ctx.Branding.AdditionalInfo))
                    AddLine(text, align, ctx.Branding.AdditionalInfo.Trim(), ctx.BodyFont, Math.Max(7, ctx.BodySize - 0.5f), ctx.MutedColor, false, false);
            }

            void LogoAndLines(ColumnDescriptor textCol)
            {
                if (!logo)
                {
                    Lines(textCol);
                    return;
                }

                if (align == PdfTextAlign.Center || ctx.Narrow)
                {
                    Place(textCol.Item(), align).Width(logoPt).Height(logoPt).Image(ctx.LogoPath!).FitArea();
                    textCol.Item().PaddingTop(2).Column(Lines);
                    return;
                }

                textCol.Item().Row(row =>
                {
                    if (align == PdfTextAlign.Right)
                    {
                        row.RelativeItem().Column(Lines);
                        row.ConstantItem(logoPt).PaddingLeft(6).Height(logoPt).Image(ctx.LogoPath!).FitArea();
                    }
                    else
                    {
                        row.ConstantItem(logoPt).Height(logoPt).Image(ctx.LogoPath!).FitArea();
                        row.RelativeItem().PaddingLeft(8).Column(Lines);
                    }
                });
            }

            if (ctx.HasQr && !ctx.Narrow)
            {
                float qr = QrSize(ctx);
                col.Item().Row(row =>
                {
                    if (align == PdfTextAlign.Right)
                    {
                        row.ConstantItem(qr).Height(qr).Image(ctx.QrPng!).FitArea();
                        row.RelativeItem().PaddingLeft(8).Column(LogoAndLines);
                    }
                    else
                    {
                        row.RelativeItem().PaddingRight(8).Column(LogoAndLines);
                        row.ConstantItem(qr).Height(qr).Image(ctx.QrPng!).FitArea();
                    }
                });
            }
            else
            {
                col.Item().Column(LogoAndLines);
                if (ctx.HasQr)
                {
                    float qr = QrSize(ctx);
                    Place(col.Item().PaddingTop(4), align).Width(qr).Height(qr).Image(ctx.QrPng!).FitArea();
                }
            }

            col.Item().PaddingTop(4).Column(title =>
            {
                AddLine(title, align, template.ResolvedTitle(), ctx.HeadingFont, Math.Max(8, ctx.HeadingSize - 2), ctx.TitleColor, ctx.UseHeadingBold, false);
                AddLine(title, align, template.Subtitle, ctx.BodyFont, Math.Max(7, ctx.BodySize), ctx.MutedColor, false, ctx.UseItalic);
                AddLine(title, align, template.HeaderNote, ctx.BodyFont, Math.Max(7, ctx.BodySize - 0.5f), ctx.MutedColor, false, false);
            });
        }

        private static float QrSize(PdfRenderContext ctx)
        {
            if (ctx.VeryNarrow) return Mm(22);
            if (ctx.Narrow) return Mm(26);
            if (ctx.Template.PaperWidthMm < 160) return Mm(28);
            return Mm(32);
        }

        private static void WriteField(TextDescriptor text, PdfRenderContext ctx, string label, string value)
        {
            var name = text.Span(label + ": ").FontColor(ctx.BodyColor);
            if (ctx.UseBodyBold)
                name.Bold();
            text.Span(value).FontColor(ctx.BodyColor);
        }

        private static void AddLine(ColumnDescriptor col, PdfTextAlign align, string? text, string font, float size, Color color, bool bold, bool italic)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var span = Place(col.Item().PaddingBottom(1), align).Text(text.Trim()).FontFamily(font).FontSize(size).FontColor(color);
            if (bold) span.Bold();
            if (italic) span.Italic();
        }

        private static IContainer Place(IContainer container, PdfTextAlign align) => align switch
        {
            PdfTextAlign.Center => container.AlignCenter(),
            PdfTextAlign.Right => container.AlignRight(),
            _ => container
        };
    }
}
