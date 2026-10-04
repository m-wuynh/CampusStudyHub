using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RefactorGradesAndGoalsForYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goals_SubjectOwner",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_Goals_User",
                table: "Goals");



            migrationBuilder.AlterColumn<long>(
                name: "AcademicTermId",
                table: "UserSubjects",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "GradeBookId",
                table: "UserSubjects",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Score",
                table: "GradeEntries",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(8,2)");

            
            migrationBuilder.AlterColumn<long>(
                name: "GradeColumnId",
                table: "GradeEntries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);


            

            

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EndDate",
                table: "Goals",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            
            migrationBuilder.DropIndex(name: "UQ_GradeBooks_UserTerm", table: "GradeBooks");
            migrationBuilder.DropForeignKey(name: "FK_GradeBooks_TermOwner", table: "GradeBooks");
            migrationBuilder.DropColumn(name: "AcademicTermId", table: "GradeBooks");
            migrationBuilder.AddColumn<int>(name: "FromYear", table: "GradeBooks", type: "int", nullable: false, defaultValue: 2026);
            migrationBuilder.AddColumn<int>(name: "ToYear", table: "GradeBooks", type: "int", nullable: false, defaultValue: 2027);


            

            migrationBuilder.CreateIndex(
                name: "IX_UserSubjects_GradeBookId",
                table: "UserSubjects",
                column: "GradeBookId");

            

            

            

            

            migrationBuilder.CreateIndex(
                name: "UQ_GradeBooks_UserYear",
                table: "GradeBooks",
                columns: new[] { "UserId", "FromYear", "ToYear" },
                unique: true);

            

            

            

            

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubjects_GradeBook",
                table: "UserSubjects",
                column: "GradeBookId",
                principalTable: "GradeBooks",
                principalColumn: "GradeBookId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goals_SubjectOwner",
                table: "Goals");

            migrationBuilder.DropForeignKey(
                name: "FK_GradeEntries_Column",
                table: "GradeEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSubjects_GradeBook",
                table: "UserSubjects");

            migrationBuilder.DropTable(
                name: "GradeColumns");

            migrationBuilder.DropTable(
                name: "GradeBooks");

            migrationBuilder.DropIndex(
                name: "IX_UserSubjects_GradeBookId",
                table: "UserSubjects");

            migrationBuilder.DropIndex(
                name: "IX_GradeEntries_Column",
                table: "GradeEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GradeEntries_Score",
                table: "GradeEntries");

            migrationBuilder.DropIndex(
                name: "IX_Goals_User",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "UQ_Goals_SubjectTargetValue",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "GradeBookId",
                table: "UserSubjects");

            migrationBuilder.DropColumn(
                name: "GradeColumnId",
                table: "GradeEntries");

            migrationBuilder.AlterColumn<long>(
                name: "AcademicTermId",
                table: "UserSubjects",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetScore10",
                table: "UserSubjects",
                type: "decimal(4,2)",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Score",
                table: "GradeEntries",
                type: "decimal(8,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.AddColumn<string>(
                name: "AssessmentType",
                table: "GradeEntries",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxScore",
                table: "GradeEntries",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "GradeEntries",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "GradeEntries",
                type: "decimal(6,2)",
                nullable: false,
                defaultValue: 1m);

            

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentValue",
                table: "Goals",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Goals",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "UnitCode",
                table: "Goals",
                type: "varchar(15)",
                unicode: false,
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            

            
        }
    }
}





