using System;
using System.Linq;
using AIDocumentMeetingAssistant.API.Models;
using BCrypt.Net;

namespace AIDocumentMeetingAssistant.API.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // S'assurer que la base de données est créée
            context.Database.EnsureCreated();

            // 1. Initialiser les rôles "Admin" et "User" s'ils n'existent pas
            var adminRole = context.Roles.FirstOrDefault(r => r.Role_Name == "Admin");
            if (adminRole == null)
            {
                adminRole = new Role
                {
                    Role_Id = Guid.NewGuid(),
                    Role_Name = "Admin",
                    Role_Description = "Administrateur du système avec accès complet"
                };
                context.Roles.Add(adminRole);
            }

            var userRole = context.Roles.FirstOrDefault(r => r.Role_Name == "User");
            if (userRole == null)
            {
                userRole = new Role
                {
                    Role_Id = Guid.NewGuid(),
                    Role_Name = "User",
                    Role_Description = "Utilisateur standard de l'application"
                };
                context.Roles.Add(userRole);
            }

            context.SaveChanges();

            // 2. Créer un utilisateur Administrateur par défaut si aucun Admin n'existe
            bool hasAdminUser = context.Users.Any(u => u.RoleId == adminRole.Role_Id);
            if (!hasAdminUser)
            {
                var defaultAdmin = new User
                {
                    User_Id = Guid.NewGuid(),
                    FullName = "Administrateur Système",
                    Email = "admin@assistant.com",
                    UserName = "admin@assistant.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                    RoleId = adminRole.Role_Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(defaultAdmin);
                context.SaveChanges();
            }
        }
    }
}
