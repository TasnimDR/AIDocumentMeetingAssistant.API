using System;

namespace AIDocumentMeetingAssistant.API.DTOs
{
    public class SummaryResponseDto
    {
        public Guid Aisummary_Id { get; set; }
        public Guid Document_Id { get; set; }
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string Source { get; set; } = null!; // "cache" ou "généré"
    }
}
