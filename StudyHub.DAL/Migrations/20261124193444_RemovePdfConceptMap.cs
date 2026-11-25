using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemovePdfConceptMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdfConcepts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PdfConcepts",
                columns: table => new
                {
                    ConceptId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalysisId = table.Column<long>(type: "bigint", nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    FromPage = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToPage = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UserSubjectId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfConcepts", x => x.ConceptId);
                    table.CheckConstraint("CK_PdfConcepts_Pages", "[FromPage] >= 1 AND [ToPage] >= [FromPage]");
                    table.ForeignKey(
                        name: "FK_PdfConcepts_PdfAiAnalyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "PdfAiAnalyses",
                        principalColumn: "AnalysisId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PdfConcepts_PdfDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "PdfDocuments",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_PdfConcepts_UserSubjects_UserSubjectId_OwnerUserId",
                        columns: x => new { x.UserSubjectId, x.OwnerUserId },
                        principalTable: "UserSubjects",
                        principalColumns: new[] { "UserSubjectId", "UserId" });
                    table.ForeignKey(
                        name: "FK_PdfConcepts_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdfConcepts_AnalysisId",
                table: "PdfConcepts",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfConcepts_DocumentId",
                table: "PdfConcepts",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfConcepts_OwnerUserId_UserSubjectId_Status",
                table: "PdfConcepts",
                columns: new[] { "OwnerUserId", "UserSubjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PdfConcepts_UserSubjectId_OwnerUserId",
                table: "PdfConcepts",
                columns: new[] { "UserSubjectId", "OwnerUserId" });
        }
    }
}
