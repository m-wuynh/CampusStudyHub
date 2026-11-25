using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AllowDuplicatePdfFileNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PdfDocuments_OwnerUserId_OriginalFileNameKey",
                table: "PdfDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_PdfDocuments_OwnerUserId_OriginalFileNameKey",
                table: "PdfDocuments",
                columns: new[] { "OwnerUserId", "OriginalFileNameKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PdfDocuments_OwnerUserId_OriginalFileNameKey",
                table: "PdfDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_PdfDocuments_OwnerUserId_OriginalFileNameKey",
                table: "PdfDocuments",
                columns: new[] { "OwnerUserId", "OriginalFileNameKey" },
                unique: true);
        }
    }
}
