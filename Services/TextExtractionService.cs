using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AIDocumentMeetingAssistant.API.Services
{
    public class TextExtractionService : ITextExtractionService
    {
        public async Task<string> ExtractTextAsync(IFormFile file)
        {
            var extension = GetFileExtension(file.FileName).ToLower();

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            try
            {
                return extension switch
                {
                    ".pdf" => ExtractPdfText(memoryStream),
                    ".txt" => ExtractTxtText(memoryStream),
                    ".docx" => ExtractDocxText(memoryStream),
                    ".doc" => "Les fichiers .doc (ancien format) ne sont pas supportés. Veuillez les convertir en .docx.",
                    _ => string.Empty // Retourner vide pour les formats non supportés
                };
            }
            catch (Exception ex)
            {
                return $"Erreur lors de l'extraction: {ex.Message}";
            }
        }

        public string GetFileExtension(string fileName)
        {
            return Path.GetExtension(fileName).ToLower();
        }

        // ========== EXTRACTION PDF ==========
        private string ExtractPdfText(Stream stream)
        {
            try
            {
                using var pdf = PdfDocument.Open(stream);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    sb.Append(page.Text);
                }
                var result = sb.ToString();
                return string.IsNullOrWhiteSpace(result) ? "Aucun texte trouvé dans le PDF." : result;
            }
            catch (Exception ex)
            {
                return $"Erreur lors de l'extraction du PDF: {ex.Message}";
            }
        }

        // ========== EXTRACTION TXT ==========
        private string ExtractTxtText(Stream stream)
        {
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                return $"Erreur lors de l'extraction du TXT: {ex.Message}";
            }
        }

        // ========== EXTRACTION DOCX ==========
        private string ExtractDocxText(Stream stream)
        {
            try
            {
                using var document = WordprocessingDocument.Open(stream, false);
                var body = document.MainDocumentPart?.Document.Body;

                if (body == null)
                    return "Aucun contenu trouvé dans le document.";

                var sb = new StringBuilder();
                foreach (var paragraph in body.Elements<Paragraph>())
                {
                    foreach (var run in paragraph.Elements<Run>())
                    {
                        foreach (var text in run.Elements<DocumentFormat.OpenXml.Wordprocessing.Text>())
                        {
                            if (!string.IsNullOrWhiteSpace(text.Text))
                            {
                                sb.Append(text.Text);
                            }
                        }
                    }
                    sb.AppendLine();
                }

                var result = sb.ToString();
                return string.IsNullOrWhiteSpace(result) ? "Aucun texte trouvé dans le DOCX." : result;
            }
            catch (Exception ex)
            {
                return $"Erreur lors de l'extraction du DOCX: {ex.Message}";
            }
        }
    }
}