using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RebuildPdfLearningModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PdfDocumentPageNotes_PdfDocuments_DocumentId",
                table: "PdfDocumentPageNotes");

            migrationBuilder.DropTable(
                name: "PdfAnnotations");

            migrationBuilder.DropTable(
                name: "PdfDocumentShares");

            migrationBuilder.DropIndex(
                name: "IX_PdfDocuments_OwnerUserId_IsDeleted_UpdatedAtUtc",
                table: "PdfDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PdfDocuments_OwnerUserId_OriginalFileNameKey",
                table: "PdfDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PdfDocuments_FileSizeBytes",
                table: "PdfDocuments");

            migrationBuilder.Sql("""
                UPDATE [PdfDocuments]
                SET [OriginalFileNameKey] = CONCAT('legacy-', [DocumentId], '.pdf')
                WHERE [OriginalFileNameKey] IS NULL OR LTRIM(RTRIM([OriginalFileNameKey])) = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileNameKey",
                table: "PdfDocuments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UserSubjectId",
                table: "PdfDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "PdfDocumentPageNotes",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.CreateTable(
                name: "PdfAiAnalyses",
                columns: table => new
                {
                    AnalysisId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    FromPage = table.Column<int>(type: "int", nullable: false),
                    ToPage = table.Column<int>(type: "int", nullable: false),
                    SourceHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfAiAnalyses", x => x.AnalysisId);
                    table.CheckConstraint("CK_PdfAiAnalyses_Pages", "[FromPage] >= 1 AND [ToPage] >= [FromPage]");
                    table.CheckConstraint("CK_PdfAiAnalyses_ResultJson", "ISJSON([ResultJson]) = 1");
                    table.ForeignKey(
                        name: "FK_PdfAiAnalyses_PdfDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "PdfDocuments",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PdfAiAnalyses_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "PdfConcepts",
                columns: table => new
                {
                    ConceptId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    UserSubjectId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    AnalysisId = table.Column<long>(type: "bigint", nullable: false),
                    FromPage = table.Column<int>(type: "int", nullable: false),
                    ToPage = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "IX_PdfDocuments_OwnerUserId_OriginalFileNameKey",
                table: "PdfDocuments",
                columns: new[] { "OwnerUserId", "OriginalFileNameKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PdfDocuments_OwnerUserId_UserSubjectId_IsDeleted_UpdatedAtUtc",
                table: "PdfDocuments",
                columns: new[] { "OwnerUserId", "UserSubjectId", "IsDeleted", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PdfDocuments_UserSubjectId_OwnerUserId",
                table: "PdfDocuments",
                columns: new[] { "UserSubjectId", "OwnerUserId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PdfDocuments_FileSizeBytes",
                table: "PdfDocuments",
                sql: "[FileSizeBytes] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_PdfAiAnalyses_DocumentId",
                table: "PdfAiAnalyses",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfAiAnalyses_OwnerUserId_DocumentId_FromPage_ToPage_SourceHash_Model_PromptVersion",
                table: "PdfAiAnalyses",
                columns: new[] { "OwnerUserId", "DocumentId", "FromPage", "ToPage", "SourceHash", "Model", "PromptVersion" });

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

            migrationBuilder.AddForeignKey(
                name: "FK_PdfDocumentPageNotes_PdfDocuments_DocumentId",
                table: "PdfDocumentPageNotes",
                column: "DocumentId",
                principalTable: "PdfDocuments",
                principalColumn: "DocumentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PdfDocuments_UserSubjects_UserSubjectId_OwnerUserId",
                table: "PdfDocuments",
                columns: new[] { "UserSubjectId", "OwnerUserId" },
                principalTable: "UserSubjects",
                principalColumns: new[] { "UserSubjectId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PdfDocumentPageNotes_PdfDocuments_DocumentId",
                table: "PdfDocumentPageNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_PdfDocuments_UserSubjects_UserSubjectId_OwnerUserId",
                table: "PdfDocuments");

            migrationBuilder.DropTable(
                name: "PdfConcepts");

            migrationBuilder.DropTable(
                name: "PdfAiAnalyses");

            migrationBuilder.DropIndex(
                name: "IX_PdfDocuments_OwnerUserId_OriginalFileNameKey",
                table: "PdfDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PdfDocuments_OwnerUserId_UserSubjectId_IsDeleted_UpdatedAtUtc",
                table: "PdfDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PdfDocuments_UserSubjectId_OwnerUserId",
                table: "PdfDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PdfDocuments_FileSizeBytes",
                table: "PdfDocuments");

            migrationBuilder.DropColumn(
                name: "UserSubjectId",
                table: "PdfDocuments");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "PdfDocumentPageNotes");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileNameKey",
                table: "PdfDocuments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateTable(
                name: "PdfAnnotations",
                columns: table => new
                {
                    AnnotationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhysicalPageNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfAnnotations", x => x.AnnotationId);
                    table.CheckConstraint("CK_PdfAnnotations_PayloadJson", "ISJSON([PayloadJson]) = 1");
                    table.CheckConstraint("CK_PdfAnnotations_PhysicalPage", "[PhysicalPageNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_PdfAnnotations_PdfDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "PdfDocuments",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_PdfAnnotations_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "PdfDocumentShares",
                columns: table => new
                {
                    PdfDocumentShareId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SharedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    SharedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    UnsharedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfDocumentShares", x => x.PdfDocumentShareId);
                    table.CheckConstraint("CK_PdfDocumentShares_State", "([IsActive] = 1 AND [UnsharedAtUtc] IS NULL)\nOR\n([IsActive] = 0 AND [UnsharedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PdfDocumentShares_PdfDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "PdfDocuments",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_PdfDocumentShares_StudyGroups_StudyGroupId",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_PdfDocumentShares_Users_SharedByUserId",
                        column: x => x.SharedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdfDocuments_OwnerUserId_IsDeleted_UpdatedAtUtc",
                table: "PdfDocuments",
                columns: new[] { "OwnerUserId", "IsDeleted", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PdfDocuments_OwnerUserId_OriginalFileNameKey",
                table: "PdfDocuments",
                columns: new[] { "OwnerUserId", "OriginalFileNameKey" },
                unique: true,
                filter: "[OriginalFileNameKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PdfDocuments_FileSizeBytes",
                table: "PdfDocuments",
                sql: "[FileSizeBytes] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_PdfAnnotations_DocumentId",
                table: "PdfAnnotations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfAnnotations_OwnerUserId_DocumentId_PhysicalPageNumber",
                table: "PdfAnnotations",
                columns: new[] { "OwnerUserId", "DocumentId", "PhysicalPageNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PdfDocumentShares_DocumentId_StudyGroupId",
                table: "PdfDocumentShares",
                columns: new[] { "DocumentId", "StudyGroupId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PdfDocumentShares_SharedByUserId",
                table: "PdfDocumentShares",
                column: "SharedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfDocumentShares_StudyGroupId_IsActive",
                table: "PdfDocumentShares",
                columns: new[] { "StudyGroupId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_PdfDocumentPageNotes_PdfDocuments_DocumentId",
                table: "PdfDocumentPageNotes",
                column: "DocumentId",
                principalTable: "PdfDocuments",
                principalColumn: "DocumentId");
        }
    }
}
