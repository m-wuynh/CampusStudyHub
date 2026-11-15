using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyHub.DAL.Persistence;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <summary>
    /// Grades & Goals – Q13: lưu thứ tự môn trong sổ điểm.
    /// Thêm cột UserSubjects.DisplayOrder và điền thứ tự ban đầu theo tên môn trong từng sổ.
    /// Không xóa hay sửa dữ liệu nào khác.
    /// </summary>
    [DbContext(typeof(StudyHubDbContext))]
    [Migration("20261102010000_AddUserSubjectDisplayOrder")]
    public partial class AddUserSubjectDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "UserSubjects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE us
                SET us.DisplayOrder = ordered.RowNumber
                FROM dbo.UserSubjects AS us
                JOIN (
                    SELECT u.UserSubjectId,
                           ROW_NUMBER() OVER (
                               PARTITION BY u.UserId, u.GradeBookId
                               ORDER BY COALESCE(u.CustomSubjectName, s.SubjectName), u.UserSubjectId) AS RowNumber
                    FROM dbo.UserSubjects AS u
                    LEFT JOIN dbo.Subjects AS s ON s.SubjectId = u.SubjectId
                ) AS ordered ON ordered.UserSubjectId = us.UserSubjectId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "UserSubjects");
        }
    }
}
