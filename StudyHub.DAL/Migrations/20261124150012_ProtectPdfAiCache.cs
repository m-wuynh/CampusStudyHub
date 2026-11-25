using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class ProtectPdfAiCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PdfAiAnalyses_OwnerUserId_DocumentId_FromPage_ToPage_SourceHash_Model_PromptVersion",
                table: "PdfAiAnalyses");

            migrationBuilder.Sql(
                """
                WITH RankedAnalyses AS
                (
                    SELECT [AnalysisId],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [OwnerUserId], [DocumentId],
                                            [FromPage], [ToPage], [SourceHash],
                                            [Model], [PromptVersion]
                               ORDER BY CASE WHEN [Status] = 'Accepted' THEN 0 ELSE 1 END,
                                        [AnalysisId] DESC
                           ) AS [DuplicateRank]
                    FROM [PdfAiAnalyses]
                    WHERE [Status] <> 'Discarded'
                )
                UPDATE [PdfAiAnalyses]
                SET [Status] = 'Discarded',
                    [UpdatedAtUtc] = SYSUTCDATETIME()
                WHERE [AnalysisId] IN
                (
                    SELECT [AnalysisId]
                    FROM RankedAnalyses
                    WHERE [DuplicateRank] > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PdfAiAnalyses_OwnerUserId_DocumentId_FromPage_ToPage_SourceHash_Model_PromptVersion",
                table: "PdfAiAnalyses",
                columns: new[] { "OwnerUserId", "DocumentId", "FromPage", "ToPage", "SourceHash", "Model", "PromptVersion" },
                unique: true,
                filter: "[Status] <> 'Discarded'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PdfAiAnalyses_OwnerUserId_DocumentId_FromPage_ToPage_SourceHash_Model_PromptVersion",
                table: "PdfAiAnalyses");

            migrationBuilder.CreateIndex(
                name: "IX_PdfAiAnalyses_OwnerUserId_DocumentId_FromPage_ToPage_SourceHash_Model_PromptVersion",
                table: "PdfAiAnalyses",
                columns: new[] { "OwnerUserId", "DocumentId", "FromPage", "ToPage", "SourceHash", "Model", "PromptVersion" });
        }
    }
}
