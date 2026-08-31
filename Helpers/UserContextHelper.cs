using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AIDocumentMeetingAssistant.API.Helpers
{
    public static class UserContextHelper
    {
        public static Guid? GetUserId(ClaimsPrincipal user)
        {
            if (user == null) return null;

            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                            ?? user.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdStr, out var id) ? id : null;
        }

        public static string GetUserRole(ClaimsPrincipal user)
        {
            if (user == null) return "User";

            return user.FindFirst(ClaimTypes.Role)?.Value
                   ?? user.FindFirst("role")?.Value
                   ?? "User";
        }

        public static bool IsAdmin(ClaimsPrincipal user)
        {
            return GetUserRole(user).Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
