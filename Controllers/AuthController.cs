using Microsoft.AspNetCore.Mvc;
using AIDocumentMeetingAssistant.API.DTOs;
using AIDocumentMeetingAssistant.API.Models;
using AIDocumentMeetingAssistant.API.Services;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtService _jwtService;

        public AuthController(AppDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        /// <summary>
        /// POST: api/auth/register - Inscription d'un nouvel utilisateur (Role: User ou Admin)
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { message = "L'adresse email et le mot de passe sont obligatoires." });
            }

            // Vérifier si l'email existe déjà
            bool emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower());
            if (emailExists)
            {
                return BadRequest(new { message = "Cet email est déjà utilisé." });
            }

            // Déterminer le nom du rôle (Admin ou User, par défaut User)
            string targetRoleName = "User";
            if (!string.IsNullOrWhiteSpace(dto.RoleName) &&
                dto.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                targetRoleName = "Admin";
            }

            // Récupérer le rôle en base de données
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Role_Name == targetRoleName);
            if (role == null)
            {
                role = new Role
                {
                    Role_Id = Guid.NewGuid(),
                    Role_Name = targetRoleName,
                    Role_Description = targetRoleName == "Admin" ? "Administrateur système" : "Utilisateur standard"
                };
                _context.Roles.Add(role);
                await _context.SaveChangesAsync();
            }

            var user = new User
            {
                User_Id = Guid.NewGuid(),
                FullName = dto.FullName,
                Email = dto.Email,
                UserName = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                RoleId = role.Role_Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                message = "Utilisateur enregistré avec succès.",
                token = token,
                user = new UserDto
                {
                    User_Id = user.User_Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    RoleName = role.Role_Name,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                }
            });
        }

        /// <summary>
        /// POST: api/auth/login - Connexion utilisateur (Vérification des rôles Admin / User)
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { message = "Veuillez fournir un email et un mot de passe." });
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());

            if (user == null)
            {
                return Unauthorized(new { message = "Email ou mot de passe incorrect." });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new { message = "Ce compte a été désactivé. Veuillez contacter un administrateur." });
            }

            bool passwordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!passwordValid)
            {
                return Unauthorized(new { message = "Email ou mot de passe incorrect." });
            }

            var token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                message = "Connexion réussie",
                token = token,
                user = new UserDto
                {
                    User_Id = user.User_Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    RoleName = user.Role?.Role_Name ?? "User",
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                }
            });
        }

        /// <summary>
        /// GET: api/auth/me - Obtenir le profil de l'utilisateur actuellement connecté
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetMe()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { message = "Token JWT invalide ou utilisateur introuvable." });
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.User_Id == userId);

            if (user == null)
            {
                return NotFound(new { message = "Utilisateur non trouvé." });
            }

            return Ok(new UserDto
            {
                User_Id = user.User_Id,
                FullName = user.FullName,
                Email = user.Email,
                RoleName = user.Role?.Role_Name ?? "User",
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            });
        }

        /// <summary>
        /// GET: api/auth/users - [Admin uniquement] Obtenir la liste complète des utilisateurs
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("users")]
        public async Task<ActionResult<List<UserDto>>> GetAllUsers()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var userDtos = users.Select(u => new UserDto
            {
                User_Id = u.User_Id,
                FullName = u.FullName,
                Email = u.Email,
                RoleName = u.Role?.Role_Name ?? "User",
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            }).ToList();

            return Ok(userDtos);
        }

        /// <summary>
        /// PUT: api/auth/users/{id}/role - [Admin uniquement] Modifier le rôle ou le statut d'un utilisateur
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UserRoleUpdateDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.User_Id == id);

            if (user == null)
            {
                return NotFound(new { message = "Utilisateur introuvable." });
            }

            string targetRoleName = dto.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "User";
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Role_Name == targetRoleName);

            if (role == null)
            {
                role = new Role
                {
                    Role_Id = Guid.NewGuid(),
                    Role_Name = targetRoleName,
                    Role_Description = targetRoleName == "Admin" ? "Administrateur système" : "Utilisateur standard"
                };
                _context.Roles.Add(role);
                await _context.SaveChangesAsync();
            }

            user.RoleId = role.Role_Id;
            user.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Utilisateur mis à jour avec le rôle '{role.Role_Name}'.",
                user = new UserDto
                {
                    User_Id = user.User_Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    RoleName = role.Role_Name,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                }
            });
        }
    }
}