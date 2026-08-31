using AIDocumentMeetingAssistant.API.Data;
using AIDocumentMeetingAssistant.API.Models;
using AIDocumentMeetingAssistant.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// Controllers
// ===============================
builder.Services.AddControllers();

// ===============================
// CORS
// ===============================
builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ===============================
// Database SQL Server
// ===============================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// ===============================
// Services - INJECTION DES DÉPENDANCES
// ===============================

// Service JWT existant
builder.Services.AddScoped<JwtService>();

// NOUVEAU: Service d'extraction de texte
builder.Services.AddScoped<ITextExtractionService, TextExtractionService>();

// NOUVEAU: Service Ollama
builder.Services.AddHttpClient<IOllamaService, OllamaService>();

// NOUVEAU: Service Qdrant Vector Database
builder.Services.AddHttpClient<IQdrantService, QdrantService>();

// NOUVEAU: Service Agent IA Conversationnel (Qdrant + Ollama)
builder.Services.AddScoped<IAIAgentService, AIAgentService>();

// NOUVEAU: Service d'Exportation PDF et Word
builder.Services.AddScoped<IExportService, ExportService>();

// Service pour l'environnement d'hébergement (accès aux fichiers)
builder.Services.AddHttpContextAccessor();

// ===============================
// Configuration des fichiers uploadés
// ===============================

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue; // Taille maximale: 2GB
    options.MemoryBufferThreshold = int.MaxValue;
});

// ===============================
// JWT Authentication
// ===============================

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrEmpty(jwtKey))
{
    throw new Exception("Jwt Key manquante dans appsettings.json");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        // Vérifier signature
        ValidateIssuerSigningKey = true,

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        ),

        // Vérifier issuer
        ValidateIssuer = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],

        // Vérifier audience
        ValidateAudience = true,

        ValidAudience = builder.Configuration["Jwt:Audience"],

        // Vérifier expiration
        ValidateLifetime = true,

        RoleClaimType = ClaimTypes.Role,

        NameClaimType = ClaimTypes.NameIdentifier,

        ClockSkew = TimeSpan.Zero
    };
});

// Autorisation
builder.Services.AddAuthorization();

// ===============================
// Swagger JWT
// ===============================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1",
        new OpenApiInfo
        {
            Title = "AI Document Meeting Assistant API",
            Version = "v1"
        });

    options.AddSecurityDefinition("Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Entrez uniquement votre token JWT"
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

    // Support pour les uploads de fichiers dans Swagger
    options.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});

// ===============================
// Build
// ===============================

var app = builder.Build();

// ===============================
// Initialisation BDD (Seeding Rôles & Admin)
// ===============================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DbInitializer.Initialize(dbContext);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Program] Erreur lors de l'initialisation de la base de données: {ex.Message}");
    }
}

// ===============================
// Middleware
// ===============================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Servir les fichiers statiques (pour les uploads)
app.UseStaticFiles();

app.UseCors("Angular");

// IMPORTANT
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ===============================
// Création du dossier d'uploads si inexistant
// ===============================

try
{
    var uploadPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads");
    if (!Directory.Exists(uploadPath))
    {
        Directory.CreateDirectory(uploadPath);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Erreur lors de la création du dossier uploads: {ex.Message}");
}

app.Run();