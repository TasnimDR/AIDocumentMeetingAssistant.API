using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIDocumentMeetingAssistant.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Role_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role_Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role_Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Role_Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    User_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.User_Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Role_Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Meeting_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Meeting_Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Participants = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MeetingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Meeting_Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "User_Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Document_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Document_FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Document_FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Document_FileType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Document_Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Meeting_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Document_Id);
                    table.ForeignKey(
                        name: "FK_Documents_Meetings_Meeting_Id",
                        column: x => x.Meeting_Id,
                        principalTable: "Meetings",
                        principalColumn: "Meeting_Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingNotes",
                columns: table => new
                {
                    MeetingNote_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Meeting_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotesContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingNotes", x => x.MeetingNote_Id);
                    table.ForeignKey(
                        name: "FK_MeetingNotes_Meetings_Meeting_Id",
                        column: x => x.Meeting_Id,
                        principalTable: "Meetings",
                        principalColumn: "Meeting_Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Aisummaries",
                columns: table => new
                {
                    Aisummary_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Meeting_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aisummary_DocId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Aisummary_CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aisummaries", x => x.Aisummary_Id);
                    table.ForeignKey(
                        name: "FK_Aisummaries_Documents_Aisummary_DocId",
                        column: x => x.Aisummary_DocId,
                        principalTable: "Documents",
                        principalColumn: "Document_Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Aisummaries_Meetings_Meeting_Id",
                        column: x => x.Meeting_Id,
                        principalTable: "Meetings",
                        principalColumn: "Meeting_Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Question_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Meeting_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Document_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Question_Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Question_Id);
                    table.ForeignKey(
                        name: "FK_Questions_Documents_Document_Id",
                        column: x => x.Document_Id,
                        principalTable: "Documents",
                        principalColumn: "Document_Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Questions_Meetings_Meeting_Id",
                        column: x => x.Meeting_Id,
                        principalTable: "Meetings",
                        principalColumn: "Meeting_Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Answers",
                columns: table => new
                {
                    Answer_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Question_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Answer_Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Answers", x => x.Answer_Id);
                    table.ForeignKey(
                        name: "FK_Answers_Questions_Question_Id",
                        column: x => x.Question_Id,
                        principalTable: "Questions",
                        principalColumn: "Question_Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aisummaries_Aisummary_DocId",
                table: "Aisummaries",
                column: "Aisummary_DocId");

            migrationBuilder.CreateIndex(
                name: "IX_Aisummaries_Meeting_Id",
                table: "Aisummaries",
                column: "Meeting_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Answers_Question_Id",
                table: "Answers",
                column: "Question_Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Meeting_Id",
                table: "Documents",
                column: "Meeting_Id");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingNotes_Meeting_Id",
                table: "MeetingNotes",
                column: "Meeting_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_CreatedById",
                table: "Meetings",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Document_Id",
                table: "Questions",
                column: "Document_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Meeting_Id",
                table: "Questions",
                column: "Meeting_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aisummaries");

            migrationBuilder.DropTable(
                name: "Answers");

            migrationBuilder.DropTable(
                name: "MeetingNotes");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
