using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyHub.DAL.Persistence;

namespace StudyHub.DAL.Migrations;

[DbContext(typeof(StudyHubDbContext))]
[Migration("20261101153546_AddNotesV3TrashRecoveryWindow")]
public sealed class AddNotesV3TrashRecoveryWindow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAtUtc",
            table: "NotesV3Notebooks",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAtUtc",
            table: "NotesV3Documents",
            type: "datetime2",
            nullable: true);

        // Notebook cũ không lưu ngày xóa, nên tính 30 ngày từ lúc cập nhật DB.
        migrationBuilder.Sql("UPDATE NotesV3Notebooks SET DeletedAtUtc = SYSUTCDATETIME() WHERE IsDeleted = 1");
        migrationBuilder.Sql("UPDATE NotesV3Documents SET DeletedAtUtc = UpdatedAtUtc WHERE IsDeleted = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "NotesV3Notebooks");
        migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "NotesV3Documents");
    }
}
