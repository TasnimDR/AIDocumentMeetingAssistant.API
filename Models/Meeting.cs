using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.Models;

public partial class Meeting
{
    public Guid Meeting_Id { get; set; }

    public string Meeting_Title { get; set; } = null!;

    public string? Participants { get; set; }

    public DateTime MeetingDate { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public Guid? CreatedById { get; set; }

 
    public User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<MeetingNote> MeetingNotes { get; set; } = new List<MeetingNote>();

    public virtual ICollection<Aisummary> Aisummaries { get; set; } = new List<Aisummary>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}