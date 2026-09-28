using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomSubjectNameToUserSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "UQ_UserSubjects_Enrollment",
                table: "UserSubjects");

            migrationBuilder.AlterColumn<int>(
                name: "SubjectId",
                table: "UserSubjects",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CustomSubjectName",
                table: "UserSubjects",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

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

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserSubjects_SubjectOrCustom",
                table: "UserSubjects",
                sql: "([SubjectId] IS NOT NULL AND [CustomSubjectName] IS NULL) OR ([SubjectId] IS NULL AND [CustomSubjectName] IS NOT NULL)");

            migrationBuilder.Sql(@"ALTER VIEW dbo.vw_SubjectGradeSummary AS
    SELECT us.UserSubjectId, us.UserId, us.AcademicTermId, us.SubjectId,
           COALESCE(us.CustomSubjectName, s.SubjectName) AS SubjectName,
           us.CreditWeight, us.TargetScore10,
           COUNT(g.GradeEntryId) AS EnteredAssessmentCount,
           CAST(SUM((g.Score / NULLIF(g.MaxScore, 0)) * 10.0 * g.Weight)
                / NULLIF(SUM(g.Weight), 0) AS decimal(5,2)) AS CurrentAverage10
    FROM dbo.UserSubjects AS us
    LEFT JOIN dbo.Subjects AS s ON s.SubjectId = us.SubjectId
    LEFT JOIN dbo.GradeEntries AS g ON g.UserSubjectId = us.UserSubjectId AND g.IsDeleted = 0
    GROUP BY us.UserSubjectId, us.UserId, us.AcademicTermId, us.SubjectId,
             us.CustomSubjectName, s.SubjectName,
             us.CreditWeight, us.TargetScore10;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER VIEW dbo.vw_SubjectGradeSummary AS
    SELECT us.UserSubjectId, us.UserId, us.AcademicTermId, us.SubjectId,
           s.SubjectName, us.CreditWeight, us.TargetScore10,
           COUNT(g.GradeEntryId) AS EnteredAssessmentCount,
           CAST(SUM((g.Score / NULLIF(g.MaxScore, 0)) * 10.0 * g.Weight)
                / NULLIF(SUM(g.Weight), 0) AS decimal(5,2)) AS CurrentAverage10
    FROM dbo.UserSubjects AS us
    JOIN dbo.Subjects AS s ON s.SubjectId = us.SubjectId
    LEFT JOIN dbo.GradeEntries AS g ON g.UserSubjectId = us.UserSubjectId AND g.IsDeleted = 0
    GROUP BY us.UserSubjectId, us.UserId, us.AcademicTermId, us.SubjectId,
             s.SubjectName, us.CreditWeight, us.TargetScore10;");

            migrationBuilder.DropIndex(
                name: "UQ_UserSubjects_CatalogEnrollment",
                table: "UserSubjects");

            migrationBuilder.DropIndex(
                name: "UQ_UserSubjects_CustomEnrollment",
                table: "UserSubjects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserSubjects_SubjectOrCustom",
                table: "UserSubjects");

            migrationBuilder.DropColumn(
                name: "CustomSubjectName",
                table: "UserSubjects");

            migrationBuilder.AlterColumn<int>(
                name: "SubjectId",
                table: "UserSubjects",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_UserSubjects_Enrollment",
                table: "UserSubjects",
                columns: new[] { "UserId", "AcademicTermId", "SubjectId" });
        }
    }
}
