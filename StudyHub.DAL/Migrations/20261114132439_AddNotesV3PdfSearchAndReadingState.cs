using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyHub.DAL.Persistence;

#nullable disable

namespace StudyHub.DAL.Migrations;

[DbContext(typeof(StudyHubDbContext))]
[Migration("20261114132439_AddNotesV3PdfSearchAndReadingState")]
public partial class AddNotesV3PdfSearchAndReadingState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NotesV3DocumentPageTexts",
            columns: table => new
            {
                DocumentVersionId = table.Column<long>(type: "bigint", nullable: false),
                PhysicalPageNumber = table.Column<int>(type: "int", nullable: false),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_NotesV3DocumentPageTexts",
                    x => new { x.DocumentVersionId, x.PhysicalPageNumber });
                table.CheckConstraint(
                    "CK_NotesV3DocumentPageTexts_Page",
                    "[PhysicalPageNumber] >= 1");
                table.ForeignKey(
                    name: "FK_NotesV3DocumentPageTexts_NotesV3DocumentVersions_DocumentVersionId",
                    column: x => x.DocumentVersionId,
                    principalTable: "NotesV3DocumentVersions",
                    principalColumn: "DocumentVersionId");
            });

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
                table.PrimaryKey(
                    "PK_NotesV3ReadingProgress",
                    x => new { x.UserId, x.DocumentId });
                table.CheckConstraint(
                    "CK_NotesV3ReadingProgress_Page",
                    "[LastPageNumber] >= 1");
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

        migrationBuilder.CreateTable(
            name: "NotesV3Bookmarks",
            columns: table => new
            {
                UserId = table.Column<long>(type: "bigint", nullable: false),
                DocumentVersionId = table.Column<long>(type: "bigint", nullable: false),
                PhysicalPageNumber = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_NotesV3Bookmarks",
                    x => new { x.UserId, x.DocumentVersionId, x.PhysicalPageNumber });
                table.CheckConstraint(
                    "CK_NotesV3Bookmarks_Page",
                    "[PhysicalPageNumber] >= 1");
                table.ForeignKey(
                    name: "FK_NotesV3Bookmarks_NotesV3DocumentVersions_DocumentVersionId",
                    column: x => x.DocumentVersionId,
                    principalTable: "NotesV3DocumentVersions",
                    principalColumn: "DocumentVersionId");
                table.ForeignKey(
                    name: "FK_NotesV3Bookmarks_Users_UserId",
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

        migrationBuilder.CreateIndex(
            name: "IX_NotesV3Bookmarks_DocumentVersionId",
            table: "NotesV3Bookmarks",
            column: "DocumentVersionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "NotesV3Bookmarks");
        migrationBuilder.DropTable(name: "NotesV3DocumentPageTexts");
        migrationBuilder.DropTable(name: "NotesV3ReadingProgress");
    }
}
