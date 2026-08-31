namespace AIDocumentMeetingAssistant.API.DTOs
{
    public class RegisterDto
    {
        public string FullName { get; set; } = "";

        public string Email { get; set; } = "";

        public string Password { get; set; } = "";

        public string RoleName { get; set; } = "User"; // "User" par défaut, ou "Admin"
    }
}