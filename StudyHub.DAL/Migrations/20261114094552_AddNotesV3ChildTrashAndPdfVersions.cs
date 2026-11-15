using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyHub.DAL.Persistence;

#nullable disable

namespace StudyHub.DAL.Migrations;

[DbContext(typeof(StudyHubDbContext))]
[Migration("20261114094552_AddNotesV3ChildTrashAndPdfVersions")]
public partial class AddNotesV3ChildTrashAndPdfVersions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAtUtc",
            table: "NotesV3Sections",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAtUtc",
            table: "NotesV3Pages",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "CurrentDocumentVersionId",
            table: "NotesV3Documents",
            type: "bigint",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE document
            SET CurrentDocumentVersionId = currentVersion.DocumentVersionId
            FROM NotesV3Documents AS document
            CROSS APPLY
            (
                SELECT TOP (1) version.DocumentVersionId
                FROM NotesV3DocumentVersions AS version
                WHERE version.DocumentId = document.DocumentId
                ORDER BY version.DocumentVersionId DESC
            ) AS currentVersion;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_NotesV3Documents_CurrentDocumentVersionId",
            table: "NotesV3Documents",
            column: "CurrentDocumentVersionId");

        migrationBuilder.AddForeignKey(
            name: "FK_NotesV3Documents_NotesV3DocumentVersions_CurrentDocumentVersionId",
            table: "NotesV3Documents",
            column: "CurrentDocumentVersionId",
            principalTable: "NotesV3DocumentVersions",
            principalColumn: "DocumentVersionId",
            onDelete: ReferentialAction.NoAction);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_NotesV3Documents_NotesV3DocumentVersions_CurrentDocumentVersionId",
            table: "NotesV3Documents");

        migrationBuilder.DropIndex(
            name: "IX_NotesV3Documents_CurrentDocumentVersionId",
            table: "NotesV3Documents");

        migrationBuilder.DropColumn(
            name: "DeletedAtUtc",
            table: "NotesV3Sections");

        migrationBuilder.DropColumn(
            name: "DeletedAtUtc",
            table: "NotesV3Pages");

        migrationBuilder.DropColumn(
            name: "CurrentDocumentVersionId",
            table: "NotesV3Documents");
    }
}
