using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.DTOs
{
    public class MeetingCreateDto
    {
        public string Meeting_Title { get; set; } = null!;
        public string? Participants { get; set; }
        public DateTime MeetingDate { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class MeetingUpdateDto
    {
        public string Meeting_Title { get; set; } = null!;
        public string? Participants { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class MeetingDto
    {
        public Guid Meeting_Id { get; set; }
        public string Meeting_Title { get; set; } = null!;
        public string? Participants { get; set; }
        public DateTime MeetingDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double DurationMinutes => (EndTime - StartTime).TotalMinutes;
        public Guid? CreatedById { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<MeetingNoteDto> MeetingNotes { get; set; } = new();
        public List<AisummaryDto> Aisummaries { get; set; } = new();
        public List<DocumentDto> Documents { get; set; } = new();
    }

    public class CalendarMeetingDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Participants { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public double DurationMinutes => (End - Start).TotalMinutes;
        public Guid? CreatedById { get; set; }
        public string? CreatedByName { get; set; }
    }

    public class MeetingNoteDto
    {
        public Guid MeetingNote_Id { get; set; }
        public string NotesContent { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

    public class MeetingNoteCreateDto
    {
        public string NotesContent { get; set; } = null!;
    }

    public class AisummaryDto
    {
        public Guid Aisummary_Id { get; set; }
        public string Type { get; set; } = null!;
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
