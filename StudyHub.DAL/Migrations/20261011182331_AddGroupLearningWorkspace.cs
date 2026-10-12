using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupLearningWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "GroupStudySessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Preparation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MeetingUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupStudySessions", x => x.Id);
                    table.CheckConstraint("CK_GroupStudySessions_Status", "Status IN ('Scheduled','Completed','Cancelled')");
                    table.CheckConstraint("CK_GroupStudySessions_Time", "EndsAtUtc > StartsAtUtc");
                    table.ForeignKey(
                        name: "FK_GroupStudySessions_StudyGroups_StudyGroupId",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_GroupStudySessions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GroupWeeklyGoals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetSessions = table.Column<int>(type: "int", nullable: false),
                    TargetAssignments = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupWeeklyGoals", x => x.Id);
                    table.CheckConstraint("CK_GroupWeeklyGoals_Target", "TargetSessions BETWEEN 0 AND 100 AND TargetAssignments BETWEEN 0 AND 100 AND TargetSessions + TargetAssignments > 0");
                    table.ForeignKey(
                        name: "FK_GroupWeeklyGoals_StudyGroups_StudyGroupId",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                });

            migrationBuilder.CreateTable(
                name: "GroupAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AllowLateSubmission = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupAssignments", x => x.Id);
                    table.CheckConstraint("CK_GroupAssignments_Status", "Status IN ('Open','Closed')");
                    table.ForeignKey(
                        name: "FK_GroupAssignments_GroupStudySessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GroupStudySessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GroupAssignments_StudyGroups_StudyGroupId",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_GroupAssignments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GroupSessionAttendances",
                columns: table => new
                {
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Attended = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupSessionAttendances", x => new { x.SessionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_GroupSessionAttendances_GroupStudySessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GroupStudySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupSessionAttendances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GroupAssignmentAssignees",
                columns: table => new
                {
                    AssignmentId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupAssignmentAssignees", x => new { x.AssignmentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_GroupAssignmentAssignees_GroupAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "GroupAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupAssignmentAssignees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GroupSubmissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    ResourceUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupSubmissions", x => x.Id);
                    table.CheckConstraint("CK_GroupSubmissions_Status", "Status IN ('Submitted','NeedsRevision','Completed')");
                    table.ForeignKey(
                        name: "FK_GroupSubmissions_GroupAssignmentAssignees_AssignmentId_UserId",
                        columns: x => new { x.AssignmentId, x.UserId },
                        principalTable: "GroupAssignmentAssignees",
                        principalColumns: new[] { "AssignmentId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupSubmissions_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GroupAnswers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupAnswers", x => x.Id);
                    table.UniqueConstraint("AK_GroupAnswers_QuestionId_Id", x => new { x.QuestionId, x.Id });
                    table.ForeignKey(
                        name: "FK_GroupAnswers_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GroupQuestions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAnswerId = table.Column<long>(type: "bigint", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupQuestions_GroupAnswers_Id_AcceptedAnswerId",
                        columns: x => new { x.Id, x.AcceptedAnswerId },
                        principalTable: "GroupAnswers",
                        principalColumns: new[] { "QuestionId", "Id" });
                    table.ForeignKey(
                        name: "FK_GroupQuestions_StudyGroups_StudyGroupId",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_GroupQuestions_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupAnswers_AuthorUserId",
                table: "GroupAnswers",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupAssignmentAssignees_UserId",
                table: "GroupAssignmentAssignees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupAssignments_CreatedByUserId",
                table: "GroupAssignments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupAssignments_SessionId",
                table: "GroupAssignments",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupAssignments_StudyGroupId_DueAtUtc",
                table: "GroupAssignments",
                columns: new[] { "StudyGroupId", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupQuestions_AuthorUserId",
                table: "GroupQuestions",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupQuestions_Id_AcceptedAnswerId",
                table: "GroupQuestions",
                columns: new[] { "Id", "AcceptedAnswerId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupQuestions_StudyGroupId_CreatedAtUtc",
                table: "GroupQuestions",
                columns: new[] { "StudyGroupId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupSessionAttendances_UserId",
                table: "GroupSessionAttendances",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupStudySessions_CreatedByUserId",
                table: "GroupStudySessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupStudySessions_StudyGroupId_StartsAtUtc",
                table: "GroupStudySessions",
                columns: new[] { "StudyGroupId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupSubmissions_AssignmentId_UserId_Version",
                table: "GroupSubmissions",
                columns: new[] { "AssignmentId", "UserId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupSubmissions_ReviewedByUserId",
                table: "GroupSubmissions",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupWeeklyGoals_StudyGroupId_WeekStart",
                table: "GroupWeeklyGoals",
                columns: new[] { "StudyGroupId", "WeekStart" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupAnswers_GroupQuestions_QuestionId",
                table: "GroupAnswers",
                column: "QuestionId",
                principalTable: "GroupQuestions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupAnswers_GroupQuestions_QuestionId",
                table: "GroupAnswers");

            migrationBuilder.DropTable(
                name: "GroupSessionAttendances");

            migrationBuilder.DropTable(
                name: "GroupSubmissions");

            migrationBuilder.DropTable(
                name: "GroupWeeklyGoals");

            migrationBuilder.DropTable(
                name: "GroupAssignmentAssignees");

            migrationBuilder.DropTable(
                name: "GroupAssignments");

            migrationBuilder.DropTable(
                name: "GroupStudySessions");

            migrationBuilder.DropTable(
                name: "GroupQuestions");

            migrationBuilder.DropTable(
                name: "GroupAnswers");
        }
    }
}
