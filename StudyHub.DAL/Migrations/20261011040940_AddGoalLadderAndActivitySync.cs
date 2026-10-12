using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalLadderAndActivitySync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_UserSubjects_CatalogEnrollment",
                table: "UserSubjects");

            migrationBuilder.DropIndex(
                name: "UQ_UserSubjects_CustomEnrollment",
                table: "UserSubjects");

            migrationBuilder.AddColumn<long>(
                name: "GoalId",
                table: "ScheduleEvents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AchievedAtUtc",
                table: "Goals",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AchievedScore",
                table: "Goals",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Goals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsBaseGoal",
                table: "Goals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Goals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ToDo");

            migrationBuilder.CreateTable(
                name: "GoalHistories",
                columns: table => new
                {
                    GoalHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoalId = table.Column<long>(type: "bigint", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    AchievedScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    AchievedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalHistories", x => x.GoalHistoryId);
                    table.ForeignKey(
                        name: "FK_GoalHistories_Goal",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "GoalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_UserSubjects_CatalogEnrollment",
                table: "UserSubjects",
                columns: new[] { "UserId", "GradeBookId", "SubjectId" },
                unique: true,
                filter: "[SubjectId] IS NOT NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_UserSubjects_CustomEnrollment",
                table: "UserSubjects",
                columns: new[] { "UserId", "GradeBookId", "CustomSubjectName" },
                unique: true,
                filter: "[CustomSubjectName] IS NOT NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEvents_GoalId",
                table: "ScheduleEvents",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalHistories_GoalId",
                table: "GoalHistories",
                column: "GoalId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEvents_Goal",
                table: "ScheduleEvents",
                column: "GoalId",
                principalTable: "Goals",
                principalColumn: "GoalId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEvents_Goal",
                table: "ScheduleEvents");

            migrationBuilder.DropTable(
                name: "GoalHistories");

            migrationBuilder.DropIndex(
                name: "UQ_UserSubjects_CatalogEnrollment",
                table: "UserSubjects");

            migrationBuilder.DropIndex(
                name: "UQ_UserSubjects_CustomEnrollment",
                table: "UserSubjects");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEvents_GoalId",
                table: "ScheduleEvents");

            migrationBuilder.DropColumn(
                name: "GoalId",
                table: "ScheduleEvents");

            migrationBuilder.DropColumn(
                name: "AchievedAtUtc",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "AchievedScore",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "IsBaseGoal",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Goals");

            migrationBuilder.CreateIndex(
                name: "UQ_UserSubjects_CatalogEnrollment",
                table: "UserSubjects",
                columns: new[] { "UserId", "AcademicTermId", "SubjectId" },
                unique: true,
                filter: "[SubjectId] IS NOT NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_UserSubjects_CustomEnrollment",
                table: "UserSubjects",
                columns: new[] { "UserId", "AcademicTermId", "CustomSubjectName" },
                unique: true,
                filter: "[CustomSubjectName] IS NOT NULL AND [IsArchived] = 0");
        }
    }
}
