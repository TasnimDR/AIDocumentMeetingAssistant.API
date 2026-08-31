using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.DTOs
{
    public class HistoryItemDto
    {
        public Guid Id { get; set; }
        public string Category { get; set; } = null!; // "Summary", "Minutes", "Q&A"
        public string Type { get; set; } = null!;     // "summary", "minutes", "chat"
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public Guid? MeetingId { get; set; }
        public string? MeetingTitle { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class HistoryResponseDto
    {
        public List<HistoryItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
