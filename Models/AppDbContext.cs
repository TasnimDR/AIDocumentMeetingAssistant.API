using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AIDocumentMeetingAssistant.API.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles { get; set; }

    public DbSet<Aisummary> Aisummaries { get; set; }

    public DbSet<Answer> Answers { get; set; }

    public DbSet<Document> Documents { get; set; }

    public DbSet<Meeting> Meetings { get; set; }

    public DbSet<MeetingNote> MeetingNotes { get; set; }

    public DbSet<Question> Questions { get; set; }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>().HasKey(i => i.Role_Id);

        modelBuilder.Entity<User>().HasKey(i => i.User_Id);

        modelBuilder.Entity<User>()
            .HasOne(d => d.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(d => d.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Meeting>().HasKey(i => i.Meeting_Id);

        // 🔥 CORRECTION: Rendre CreatedById nullable
        modelBuilder.Entity<Meeting>()
            .HasOne(d => d.CreatedBy)
            .WithMany(u => u.Meetings)
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);  // AJOUTÉ - rend la relation optionnelle

        modelBuilder.Entity<Document>().HasKey(i => i.Document_Id);

        modelBuilder.Entity<Document>()
            .HasOne(d => d.Meeting)
            .WithMany(m => m.Documents)
            .HasForeignKey(d => d.Meeting_Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MeetingNote>().HasKey(i => i.MeetingNote_Id);

        modelBuilder.Entity<MeetingNote>()
            .HasOne(d => d.Meeting)
            .WithMany(m => m.MeetingNotes)
            .HasForeignKey(d => d.Meeting_Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Aisummary>().HasKey(i => i.Aisummary_Id);

        modelBuilder.Entity<Aisummary>()
            .HasOne(d => d.Meeting)
            .WithMany(m => m.Aisummaries)
            .HasForeignKey(d => d.Meeting_Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Question>().HasKey(i => i.Question_Id);

        modelBuilder.Entity<Question>()
            .HasOne(d => d.Meeting)
            .WithMany(m => m.Questions)
            .HasForeignKey(d => d.Meeting_Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Answer>().HasKey(i => i.Answer_Id);

        modelBuilder.Entity<Answer>()
            .HasOne(a => a.Question)
            .WithOne(q => q.Answer)
            .HasForeignKey<Answer>(a => a.Question_Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}