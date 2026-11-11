using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyHub.DAL.Persistence;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <summary>
    /// Grades & Goals – Hành trình mục tiêu: thêm bảng GoalProgressLogs (chỉ thêm mới, không sửa dữ liệu cũ).
    /// Trên DB thật áp dụng bằng script deploy_journey_20261104\01_create_goal_progress_logs.sql
    /// (không dùng dotnet ef database update vì lịch sử migration đang lệch).
    /// </summary>
    [DbContext(typeof(StudyHubDbContext))]
    [Migration("20261104090000_AddGoalProgressLogs")]
    public partial class AddGoalProgressLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoalProgressLogs",
                columns: table => new
                {
                    GoalProgressLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    UserSubjectId = table.Column<long>(type: "bigint", nullable: false),
                    GoalId = table.Column<long>(type: "bigint", nullable: true),
                    EventType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    AverageScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    PreviousValue = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    TargetValue = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ProgressPercent = table.Column<int>(type: "int", nullable: true),
                    StatusFrom = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StatusTo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalProgressLogs", x => x.GoalProgressLogId);
                    table.ForeignKey(
                        name: "FK_GoalProgressLogs_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_GoalProgressLogs_UserSubject",
                        column: x => x.UserSubjectId,
                        principalTable: "UserSubjects",
                        principalColumn: "UserSubjectId");
                    table.ForeignKey(
                        name: "FK_GoalProgressLogs_Goal",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "GoalId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressLogs_Subject",
                table: "GoalProgressLogs",
                columns: new[] { "UserSubjectId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressLogs_Goal",
                table: "GoalProgressLogs",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressLogs_UserId",
                table: "GoalProgressLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GoalProgressLogs");
        }
    }
}
