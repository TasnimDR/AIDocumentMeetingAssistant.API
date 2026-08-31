using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Services
{
    public class ExportSectionModel
    {
        public string? Heading { get; set; }
        public string Content { get; set; } = null!;
    }

    public class ExportDataModel
    {
        public string Title { get; set; } = null!;
        public string? Subtitle { get; set; }
        public string Category { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<ExportSectionModel> Sections { get; set; } = new();
        public string FileName { get; set; } = "Export";
    }

    public interface IExportService
    {
        Task<byte[]> GeneratePdfAsync(ExportDataModel data, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateWordAsync(ExportDataModel data, CancellationToken cancellationToken = default);
    }
}
