using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.Models;

public partial class Question
{
    public Guid Question_Id { get; set; }

    public Guid Meeting_Id { get; set; }  // Changé de MeetingId à Meeting_Id

    public string Question_Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Answer? Answer { get; set; }

    public virtual Meeting Meeting { get; set; } = null!;
}