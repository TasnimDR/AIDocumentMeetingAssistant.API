using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.Models;

public partial class Document
{
    public Guid Document_Id { get; set; }

    public string Document_FileName { get; set; } = null!;

    public string Document_FilePath { get; set; } = null!;

    public string Document_FileType { get; set; } = null!;

    public string? Document_Description { get; set; }

    public string? ExtractedText { get; set; }

    public long? FileSize { get; set; }

    // Changé de MeetingId à Meeting_Id pour correspondre au modèle Meeting
    // Rendu nullable avec ? pour permettre les documents sans meeting
    public Guid? Meeting_Id { get; set; }

    public DateTime UploadedAt { get; set; }

    // Navigation property - rendue nullable car Meeting_Id peut être null
    public virtual Meeting? Meeting { get; set; }
}