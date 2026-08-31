using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.Models;

public partial class Aisummary
{
    public Guid Aisummary_Id { get; set; }

    public Guid Meeting_Id { get; set; }  // Changé de MeetingId à Meeting_Id

    public string Type { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime Aisummary_CreatedAt { get; set; }

    public virtual Meeting Meeting { get; set; } = null!;
}