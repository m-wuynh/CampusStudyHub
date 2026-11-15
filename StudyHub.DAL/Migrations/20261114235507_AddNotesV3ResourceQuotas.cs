using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddNotesV3ResourceQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "NotesV3Documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotesV3Documents_FileSizeBytes",
                table: "NotesV3Documents",
                sql: "[FileSizeBytes] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_NotesV3Documents_FileSizeBytes",
                table: "NotesV3Documents");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "NotesV3Documents");
        }
    }
}
