using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.DTOs
{
    public class QdrantSearchResultDto
    {
        public string ChunkId { get; set; } = null!;
        public Guid DocumentId { get; set; }
        public string DocumentName { get; set; } = null!;
        public Guid? MeetingId { get; set; }
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = null!;
        public float Score { get; set; }
    }

    public class AskQuestionDto
    {
        public string Question { get; set; } = null!;
        public Guid? DocumentId { get; set; }
        public Guid? MeetingId { get; set; }
        public int Limit { get; set; } = 5;
    }

    public class AskQuestionResponseDto
    {
        public string Question { get; set; } = null!;
        public string Answer { get; set; } = null!;
        public Guid? DocumentId { get; set; }
        public List<QdrantSearchResultDto> Sources { get; set; } = new();
        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
    }
}
