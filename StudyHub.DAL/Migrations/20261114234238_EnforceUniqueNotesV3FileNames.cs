using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueNotesV3FileNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalFileNameKey",
                table: "NotesV3Documents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Documents_OwnerUserId_OriginalFileNameKey",
                table: "NotesV3Documents",
                columns: new[] { "OwnerUserId", "OriginalFileNameKey" },
                unique: true,
                filter: "[OriginalFileNameKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotesV3Documents_OwnerUserId_OriginalFileNameKey",
                table: "NotesV3Documents");

            migrationBuilder.DropColumn(
                name: "OriginalFileNameKey",
                table: "NotesV3Documents");
        }
    }
}
