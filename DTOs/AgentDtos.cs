using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.DTOs
{
    public class AgentChatRequestDto
    {
        public string Question { get; set; } = null!;
        public Guid? DocumentId { get; set; }
        public Guid? MeetingId { get; set; }
        public int Limit { get; set; } = 5;
    }

    public class AgentChatResponseDto
    {
        public Guid QuestionId { get; set; }
        public Guid AnswerId { get; set; }
        public string Question { get; set; } = null!;
        public string Answer { get; set; } = null!;
        public Guid? MeetingId { get; set; }
        public List<QdrantSearchResultDto> Sources { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AgentHistoryDto
    {
        public Guid QuestionId { get; set; }
        public Guid AnswerId { get; set; }
        public Guid? MeetingId { get; set; }
        public string Question { get; set; } = null!;
        public string Answer { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
