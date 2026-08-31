using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.Models;

public partial class Answer
{
    public Guid Answer_Id { get; set; }

    public Guid Question_Id { get; set; }  // Changé de QuestionId à Question_Id

    public string Answer_Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Question Question { get; set; } = null!;
}