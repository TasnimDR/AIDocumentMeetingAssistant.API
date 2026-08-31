using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIDocumentMeetingAssistant.API.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAisummariesAndQuestionsRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Aisummaries_Documents_Aisummary_DocId",
                table: "Aisummaries");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Documents_Document_Id",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_Document_Id",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Aisummaries_Aisummary_DocId",
                table: "Aisummaries");

            migrationBuilder.DropColumn(
                name: "Document_Id",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Aisummary_DocId",
                table: "Aisummaries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Document_Id",
                table: "Questions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Aisummary_DocId",
                table: "Aisummaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Document_Id",
                table: "Questions",
                column: "Document_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Aisummaries_Aisummary_DocId",
                table: "Aisummaries",
                column: "Aisummary_DocId");

            migrationBuilder.AddForeignKey(
                name: "FK_Aisummaries_Documents_Aisummary_DocId",
                table: "Aisummaries",
                column: "Aisummary_DocId",
                principalTable: "Documents",
                principalColumn: "Document_Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Documents_Document_Id",
                table: "Questions",
                column: "Document_Id",
                principalTable: "Documents",
                principalColumn: "Document_Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
