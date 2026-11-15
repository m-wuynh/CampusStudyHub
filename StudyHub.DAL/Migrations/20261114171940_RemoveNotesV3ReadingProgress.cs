using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotesV3ReadingProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotesV3ReadingProgress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotesV3ReadingProgress",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    LastPageNumber = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3ReadingProgress", x => new { x.UserId, x.DocumentId });
                    table.CheckConstraint("CK_NotesV3ReadingProgress_Page", "[LastPageNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_NotesV3ReadingProgress_NotesV3Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "NotesV3Documents",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_NotesV3ReadingProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3ReadingProgress_DocumentId",
                table: "NotesV3ReadingProgress",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3ReadingProgress_UserId_UpdatedAtUtc",
                table: "NotesV3ReadingProgress",
                columns: new[] { "UserId", "UpdatedAtUtc" });
        }
    }
}
