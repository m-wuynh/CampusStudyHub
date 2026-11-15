using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations;

public partial class LinkStudySessionsToCalendar : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "GroupStudySessionId", table: "ScheduleEvents", type: "bigint", nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_ScheduleEvents_GroupStudySessionId", table: "ScheduleEvents", column: "GroupStudySessionId");
        migrationBuilder.CreateIndex(
            name: "IX_ScheduleEvents_UserId_GroupStudySessionId", table: "ScheduleEvents",
            columns: new[] { "UserId", "GroupStudySessionId" }, unique: true,
            filter: "[GroupStudySessionId] IS NOT NULL");
        migrationBuilder.AddForeignKey(
            name: "FK_ScheduleEvents_GroupStudySessions_GroupStudySessionId", table: "ScheduleEvents",
            column: "GroupStudySessionId", principalTable: "GroupStudySessions", principalColumn: "Id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ScheduleEvents_GroupStudySessions_GroupStudySessionId", table: "ScheduleEvents");
        migrationBuilder.DropIndex(name: "IX_ScheduleEvents_GroupStudySessionId", table: "ScheduleEvents");
        migrationBuilder.DropIndex(name: "IX_ScheduleEvents_UserId_GroupStudySessionId", table: "ScheduleEvents");
        migrationBuilder.DropColumn(name: "GroupStudySessionId", table: "ScheduleEvents");
    }
}