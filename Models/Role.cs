using System;
using System.Collections.Generic;

namespace AIDocumentMeetingAssistant.API.Models;

public partial class Role
{
    public Guid Role_Id { get; set; }

    public string Role_Name { get; set; } = null!;

    public string? Role_Description { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}