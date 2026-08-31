using System;

namespace AIDocumentMeetingAssistant.API.DTOs
{
    public class UserDto
    {
        public Guid User_Id { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string RoleName { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserRoleUpdateDto
    {
        public string RoleName { get; set; } = "User"; // "Admin" ou "User"
        public bool IsActive { get; set; } = true;
    }
}
