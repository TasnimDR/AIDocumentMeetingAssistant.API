using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.DTOs
{
    public class DocumentDto
    {
        public Guid Document_Id { get; set; }
        public string Document_FileName { get; set; } = null!;
        public string Document_FileType { get; set; } = null!;
        public string? Document_Description { get; set; }
        public string? ExtractedText { get; set; }
        public long? FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Status { get; set; } = "en_attente";
        public string? Preview { get; set; }
    }

    public class DocumentUploadDto
    {
        public IFormFile File { get; set; } = null!;
        public string? Description { get; set; }
        public Guid? MeetingId { get; set; } // Rendre nullable
    }

    public class DocumentResponseDto
    {
        public List<DocumentDto> Documents { get; set; } = new();
        public int TotalCount { get; set; }
        public long TotalSize { get; set; }
        public int PendingCount { get; set; }
    }
}