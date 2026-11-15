using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations;

public partial class SimplifyNotesV3ToPdfOnly : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            "FK_NotesV3Annotations_NotesV3DocumentShares_GroupDocumentShareId",
            "NotesV3Annotations");
        migrationBuilder.DropForeignKey(
            "FK_NotesV3Annotations_NotesV3DocumentVersions_DocumentVersionId",
            "NotesV3Annotations");
        migrationBuilder.DropForeignKey(
            "FK_NotesV3Bookmarks_NotesV3DocumentVersions_DocumentVersionId",
            "NotesV3Bookmarks");
        migrationBuilder.DropForeignKey(
            "FK_NotesV3DocumentPageTexts_NotesV3DocumentVersions_DocumentVersionId",
            "NotesV3DocumentPageTexts");
        migrationBuilder.DropForeignKey(
            "FK_NotesV3Documents_NotesV3DocumentVersions_CurrentDocumentVersionId",
            "NotesV3Documents");

        migrationBuilder.AddColumn<string>(
            "StorageKey", "NotesV3Documents", "nvarchar(200)",
            maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>(
            "OriginalFileName", "NotesV3Documents", "nvarchar(200)",
            maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<int>(
            "PageCount", "NotesV3Documents", "int", nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE document
            SET StorageKey = version.StorageKey,
                OriginalFileName = version.OriginalFileName,
                PageCount = version.PageCount
            FROM NotesV3Documents AS document
            INNER JOIN NotesV3DocumentVersions AS version
                ON version.DocumentVersionId = document.CurrentDocumentVersionId;

            IF EXISTS
            (
                SELECT 1
                FROM NotesV3Documents
                WHERE StorageKey IS NULL OR OriginalFileName IS NULL OR PageCount IS NULL
            )
                THROW 51000, 'Không thể chuyển NotesV3 sang PDF-only vì metadata PDF chưa đầy đủ.', 1;
            """);

        migrationBuilder.AlterColumn<string>(
            "StorageKey", "NotesV3Documents", "nvarchar(200)",
            maxLength: 200, nullable: false,
            oldClrType: typeof(string), oldType: "nvarchar(200)",
            oldMaxLength: 200, oldNullable: true);
        migrationBuilder.AlterColumn<string>(
            "OriginalFileName", "NotesV3Documents", "nvarchar(200)",
            maxLength: 200, nullable: false,
            oldClrType: typeof(string), oldType: "nvarchar(200)",
            oldMaxLength: 200, oldNullable: true);
        migrationBuilder.AlterColumn<int>(
            "PageCount", "NotesV3Documents", "int", nullable: false,
            oldClrType: typeof(int), oldType: "int", oldNullable: true);

        migrationBuilder.CreateTable(
            name: "NotesV3PdfPageNotes",
            columns: table => new
            {
                PdfPageNoteId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DocumentId = table.Column<long>(type: "bigint", nullable: false),
                OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                PhysicalPageNumber = table.Column<int>(type: "int", nullable: false),
                ContentText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotesV3PdfPageNotes", x => x.PdfPageNoteId);
                table.CheckConstraint(
                    "CK_NotesV3PdfPageNotes_PhysicalPage",
                    "[PhysicalPageNumber] >= 1");
                table.ForeignKey(
                    "FK_NotesV3PdfPageNotes_NotesV3Documents_DocumentId",
                    x => x.DocumentId,
                    "NotesV3Documents",
                    "DocumentId");
                table.ForeignKey(
                    "FK_NotesV3PdfPageNotes_Users_OwnerUserId",
                    x => x.OwnerUserId,
                    "Users",
                    "UserId");
            });

        migrationBuilder.Sql(
            """
            INSERT INTO NotesV3PdfPageNotes
                (DocumentId, OwnerUserId, PhysicalPageNumber, ContentText)
            SELECT version.DocumentId,
                   link.OwnerUserId,
                   link.PhysicalPageNumber,
                   page.ContentText
            FROM NotesV3PdfPageLinks AS link
            INNER JOIN NotesV3Pages AS page ON page.PageId = link.PageId
            INNER JOIN NotesV3DocumentVersions AS version
                ON version.DocumentVersionId = link.DocumentVersionId
            INNER JOIN NotesV3Documents AS document
                ON document.DocumentId = version.DocumentId
               AND document.CurrentDocumentVersionId = version.DocumentVersionId
            WHERE link.OwnerUserId IS NOT NULL
              AND link.GroupDocumentShareId IS NULL
              AND page.IsDeleted = 0
              AND LEN(page.ContentText) > 0;
            """);

        migrationBuilder.DropCheckConstraint(
            "CK_NotesV3Annotations_Scope",
            "NotesV3Annotations");
        migrationBuilder.DropIndex(
            "IX_NotesV3Annotations_GroupDocumentShareId_DocumentVersionId_PhysicalPageNumber_IsDeleted",
            "NotesV3Annotations");
        migrationBuilder.DropIndex(
            "IX_NotesV3Annotations_OwnerUserId_DocumentVersionId_PhysicalPageNumber_IsDeleted",
            "NotesV3Annotations");
        migrationBuilder.DropIndex(
            "IX_NotesV3Annotations_DocumentVersionId",
            "NotesV3Annotations");

        migrationBuilder.Sql(
            """
            DELETE FROM NotesV3Annotations
            WHERE OwnerUserId IS NULL
               OR GroupDocumentShareId IS NOT NULL
               OR IsDeleted = 1
               OR NOT EXISTS
                  (
                      SELECT 1
                      FROM NotesV3Documents AS document
                      WHERE document.CurrentDocumentVersionId =
                            NotesV3Annotations.DocumentVersionId
                  );
            """);
        migrationBuilder.AddColumn<long>(
            "DocumentId", "NotesV3Annotations", "bigint", nullable: true);
        migrationBuilder.Sql(
            """
            UPDATE annotation
            SET DocumentId = version.DocumentId
            FROM NotesV3Annotations AS annotation
            INNER JOIN NotesV3DocumentVersions AS version
                ON version.DocumentVersionId = annotation.DocumentVersionId;
            """);
        migrationBuilder.AlterColumn<long>(
            "DocumentId", "NotesV3Annotations", "bigint", nullable: false,
            oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
        migrationBuilder.AlterColumn<long>(
            "OwnerUserId", "NotesV3Annotations", "bigint", nullable: false,
            oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
        migrationBuilder.DropColumn("DocumentVersionId", "NotesV3Annotations");
        migrationBuilder.DropColumn("GroupDocumentShareId", "NotesV3Annotations");
        migrationBuilder.DropColumn("IsDeleted", "NotesV3Annotations");

        migrationBuilder.DropPrimaryKey("PK_NotesV3Bookmarks", "NotesV3Bookmarks");
        migrationBuilder.DropIndex(
            "IX_NotesV3Bookmarks_DocumentVersionId",
            "NotesV3Bookmarks");
        migrationBuilder.AddColumn<long>(
            "DocumentId", "NotesV3Bookmarks", "bigint", nullable: true);
        migrationBuilder.Sql(
            """
            DELETE FROM NotesV3Bookmarks
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM NotesV3Documents AS document
                WHERE document.CurrentDocumentVersionId =
                      NotesV3Bookmarks.DocumentVersionId
            );

            UPDATE bookmark
            SET DocumentId = version.DocumentId
            FROM NotesV3Bookmarks AS bookmark
            INNER JOIN NotesV3DocumentVersions AS version
                ON version.DocumentVersionId = bookmark.DocumentVersionId;
            """);
        migrationBuilder.AlterColumn<long>(
            "DocumentId", "NotesV3Bookmarks", "bigint", nullable: false,
            oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
        migrationBuilder.DropColumn("DocumentVersionId", "NotesV3Bookmarks");
        migrationBuilder.AddPrimaryKey(
            "PK_NotesV3Bookmarks",
            "NotesV3Bookmarks",
            new[] { "UserId", "DocumentId", "PhysicalPageNumber" });

        migrationBuilder.DropPrimaryKey(
            "PK_NotesV3DocumentPageTexts",
            "NotesV3DocumentPageTexts");
        migrationBuilder.AddColumn<long>(
            "DocumentId", "NotesV3DocumentPageTexts", "bigint", nullable: true);
        migrationBuilder.Sql(
            """
            DELETE FROM NotesV3DocumentPageTexts
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM NotesV3Documents AS document
                WHERE document.CurrentDocumentVersionId =
                      NotesV3DocumentPageTexts.DocumentVersionId
            );

            UPDATE pageText
            SET DocumentId = version.DocumentId
            FROM NotesV3DocumentPageTexts AS pageText
            INNER JOIN NotesV3DocumentVersions AS version
                ON version.DocumentVersionId = pageText.DocumentVersionId;
            """);
        migrationBuilder.AlterColumn<long>(
            "DocumentId", "NotesV3DocumentPageTexts", "bigint", nullable: false,
            oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
        migrationBuilder.DropColumn(
            "DocumentVersionId",
            "NotesV3DocumentPageTexts");
        migrationBuilder.AddPrimaryKey(
            "PK_NotesV3DocumentPageTexts",
            "NotesV3DocumentPageTexts",
            new[] { "DocumentId", "PhysicalPageNumber" });

        migrationBuilder.DropIndex(
            "IX_NotesV3Documents_CurrentDocumentVersionId",
            "NotesV3Documents");
        migrationBuilder.DropColumn(
            "CurrentDocumentVersionId",
            "NotesV3Documents");

        migrationBuilder.DropTable("NotesV3PdfPageLinks");
        migrationBuilder.DropTable("NotesV3DocumentVersions");
        migrationBuilder.DropTable("NotesV3Pages");
        migrationBuilder.DropTable("NotesV3Sections");
        migrationBuilder.DropTable("NotesV3Notebooks");

        migrationBuilder.CreateIndex(
            "IX_NotesV3Documents_StorageKey",
            "NotesV3Documents",
            "StorageKey",
            unique: true);
        migrationBuilder.AddCheckConstraint(
            "CK_NotesV3Documents_PageCount",
            "NotesV3Documents",
            "[PageCount] >= 1");

        migrationBuilder.CreateIndex(
            "IX_NotesV3Annotations_OwnerUserId_DocumentId_PhysicalPageNumber",
            "NotesV3Annotations",
            new[] { "OwnerUserId", "DocumentId", "PhysicalPageNumber" });
        migrationBuilder.CreateIndex(
            "IX_NotesV3Annotations_DocumentId",
            "NotesV3Annotations",
            "DocumentId");
        migrationBuilder.AddForeignKey(
            "FK_NotesV3Annotations_NotesV3Documents_DocumentId",
            "NotesV3Annotations",
            "DocumentId",
            "NotesV3Documents",
            principalColumn: "DocumentId");

        migrationBuilder.CreateIndex(
            "IX_NotesV3Bookmarks_DocumentId",
            "NotesV3Bookmarks",
            "DocumentId");
        migrationBuilder.AddForeignKey(
            "FK_NotesV3Bookmarks_NotesV3Documents_DocumentId",
            "NotesV3Bookmarks",
            "DocumentId",
            "NotesV3Documents",
            principalColumn: "DocumentId");

        migrationBuilder.AddForeignKey(
            "FK_NotesV3DocumentPageTexts_NotesV3Documents_DocumentId",
            "NotesV3DocumentPageTexts",
            "DocumentId",
            "NotesV3Documents",
            principalColumn: "DocumentId");

        migrationBuilder.CreateIndex(
            "IX_NotesV3PdfPageNotes_DocumentId",
            "NotesV3PdfPageNotes",
            "DocumentId");
        migrationBuilder.CreateIndex(
            "IX_NotesV3PdfPageNotes_OwnerUserId_DocumentId_PhysicalPageNumber",
            "NotesV3PdfPageNotes",
            new[] { "OwnerUserId", "DocumentId", "PhysicalPageNumber" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Migration PDF-only xóa dữ liệu Notebook và annotation nhóm nên không thể rollback an toàn.");
}
