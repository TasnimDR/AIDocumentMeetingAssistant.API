using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AIDocumentMeetingAssistant.API.Services
{
    public class ExportService : IExportService
    {
        public ExportService()
        {
            // Configuration de la licence communautaire QuestPDF (Gratuite & Open-Source)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public Task<byte[]> GeneratePdfAsync(ExportDataModel data, CancellationToken cancellationToken = default)
        {
            var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));

                    // En-tête du document PDF
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(titleCol =>
                            {
                                titleCol.Item().Text("AI Document & Meeting Assistant").FontSize(10).SemiBold().FontColor(Colors.Blue.Darken2);
                                titleCol.Item().Text(data.Category.ToUpper()).FontSize(9).FontColor(Colors.Grey.Medium);
                            });

                            row.ConstantItem(150).AlignRight().Text(data.CreatedAt.ToString("dd/MM/yyyy HH:mm")).FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Blue.Lighten3);
                    });

                    // Contenu principal du PDF
                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        // Titre principal
                        col.Item().Text(data.Title).FontSize(20).Bold().FontColor(Colors.Blue.Darken3);

                        if (!string.IsNullOrWhiteSpace(data.Subtitle))
                        {
                            col.Item().PaddingTop(4).Text(data.Subtitle).FontSize(13).Italic().FontColor(Colors.Grey.Darken1);
                        }

                        // Métadonnées (Cartouche d'information)
                        if (data.Metadata != null && data.Metadata.Any())
                        {
                            col.Item().PaddingVertical(10).Background(Colors.Grey.Lighten4).Padding(10).Column(metaCol =>
                            {
                                foreach (var kvp in data.Metadata)
                                {
                                    metaCol.Item().Row(r =>
                                    {
                                        r.ConstantItem(140).Text($"{kvp.Key} :").SemiBold().FontSize(10);
                                        r.RelativeItem().Text(kvp.Value).FontSize(10);
                                    });
                                }
                            });
                        }

                        col.Item().PaddingVertical(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                        // Sections du document
                        foreach (var section in data.Sections)
                        {
                            if (!string.IsNullOrWhiteSpace(section.Heading))
                            {
                                col.Item().PaddingTop(12).PaddingBottom(4).Text(section.Heading).FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                            }

                            // Traitement du texte (lignes)
                            var paragraphs = section.Content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                            foreach (var para in paragraphs)
                            {
                                if (string.IsNullOrWhiteSpace(para))
                                {
                                    col.Item().PaddingBottom(4);
                                }
                                else
                                {
                                    col.Item().PaddingBottom(4).Text(para).FontSize(11).LineHeight(1.3f);
                                }
                            }
                        }
                    });

                    // Pied de page
                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text("Généré automatiquement par AI Document Meeting Assistant").FontSize(8).FontColor(Colors.Grey.Medium);
                            row.RelativeItem().AlignRight().Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" / ");
                                x.TotalPages();
                            });
                        });
                    });
                });
            }).GeneratePdf();

            return Task.FromResult(pdfBytes);
        }

        public Task<byte[]> GenerateWordAsync(ExportDataModel data, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();

            using (var wordDocument = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new W.Document();
                W.Body body = mainPart.Document.AppendChild(new W.Body());

                // En-tête / Catégorie
                var categoryPara = body.AppendChild(new W.Paragraph());
                var categoryRun = categoryPara.AppendChild(new W.Run(new W.Text($"[ {data.Category.ToUpper()} ]")));
                categoryRun.RunProperties = new W.RunProperties(
                    new W.Color { Val = "005A9E" },
                    new W.FontSize { Val = "18" },
                    new W.Bold()
                );

                // Titre
                var titlePara = body.AppendChild(new W.Paragraph());
                var titleRun = titlePara.AppendChild(new W.Run(new W.Text(data.Title)));
                titleRun.RunProperties = new W.RunProperties(
                    new W.Color { Val = "1F4E78" },
                    new W.FontSize { Val = "36" },
                    new W.Bold()
                );

                // Sous-titre
                if (!string.IsNullOrWhiteSpace(data.Subtitle))
                {
                    var subPara = body.AppendChild(new W.Paragraph());
                    var subRun = subPara.AppendChild(new W.Run(new W.Text(data.Subtitle)));
                    subRun.RunProperties = new W.RunProperties(
                        new W.Color { Val = "595959" },
                        new W.FontSize { Val = "24" },
                        new W.Italic()
                    );
                }

                // Date
                var datePara = body.AppendChild(new W.Paragraph());
                var dateRun = datePara.AppendChild(new W.Run(new W.Text($"Date d'exportation : {data.CreatedAt:dd/MM/yyyy HH:mm}")));
                dateRun.RunProperties = new W.RunProperties(
                    new W.Color { Val = "7F7F7F" },
                    new W.FontSize { Val = "18" },
                    new W.Italic()
                );

                // Ligne de séparation
                body.AppendChild(new W.Paragraph(new W.Run(new W.Text("------------------------------------------------------------------------------------------------")) { RunProperties = new W.RunProperties(new W.Color { Val = "D9D9D9" }) }));

                // Métadonnées
                if (data.Metadata != null && data.Metadata.Any())
                {
                    foreach (var kvp in data.Metadata)
                    {
                        var metaPara = body.AppendChild(new W.Paragraph());
                        var keyRun = metaPara.AppendChild(new W.Run(new W.Text($"{kvp.Key} : ")));
                        keyRun.RunProperties = new W.RunProperties(new W.Bold(), new W.FontSize { Val = "20" });

                        var valRun = metaPara.AppendChild(new W.Run(new W.Text(kvp.Value)));
                        valRun.RunProperties = new W.RunProperties(new W.FontSize { Val = "20" });
                    }

                    body.AppendChild(new W.Paragraph(new W.Run(new W.Text("------------------------------------------------------------------------------------------------")) { RunProperties = new W.RunProperties(new W.Color { Val = "D9D9D9" }) }));
                }

                // Sections
                foreach (var section in data.Sections)
                {
                    if (!string.IsNullOrWhiteSpace(section.Heading))
                    {
                        var hPara = body.AppendChild(new W.Paragraph());
                        var hRun = hPara.AppendChild(new W.Run(new W.Text(section.Heading)));
                        hRun.RunProperties = new W.RunProperties(
                            new W.Color { Val = "2E75B6" },
                            new W.FontSize { Val = "26" },
                            new W.Bold()
                        );
                    }

                    var lines = section.Content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        var p = body.AppendChild(new W.Paragraph());
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var r = p.AppendChild(new W.Run(new W.Text(line)));
                            r.RunProperties = new W.RunProperties(new W.FontSize { Val = "22" });
                        }
                    }
                }

                mainPart.Document.Save();
            }

            return Task.FromResult(ms.ToArray());
        }
    }
}
