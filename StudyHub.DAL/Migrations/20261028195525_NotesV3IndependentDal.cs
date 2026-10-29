using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class NotesV3IndependentDal : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotesV3Documents",
                columns: table => new
                {
                    DocumentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3Documents", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_NotesV3Documents_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "NotesV3Notebooks",
                columns: table => new
                {
                    NotebookId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3Notebooks", x => x.NotebookId);
                    table.CheckConstraint("CK_NotesV3Notebooks_SortOrder", "[SortOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_NotesV3Notebooks_StudyGroups_StudyGroupId",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_NotesV3Notebooks_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "NotesV3DocumentShares",
                columns: table => new
                {
                    GroupDocumentShareId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    SharedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SharedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UnsharedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3DocumentShares", x => x.GroupDocumentShareId);
                    table.CheckConstraint("CK_NotesV3DocumentShares_State", "([IsActive] = 1 AND [UnsharedAtUtc] IS NULL)\r\nOR\r\n([IsActive] = 0 AND [UnsharedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_NotesV3DocumentShares_NotesV3Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "NotesV3Documents",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_NotesV3DocumentShares_StudyGroups_StudyGroupId",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_NotesV3DocumentShares_Users_SharedByUserId",
                        column: x => x.SharedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "NotesV3DocumentVersions",
                columns: table => new
                {
                    DocumentVersionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3DocumentVersions", x => x.DocumentVersionId);
                    table.CheckConstraint("CK_NotesV3DocumentVersions_FileSize", "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 26214400");
                    table.CheckConstraint("CK_NotesV3DocumentVersions_PageCount", "[PageCount] IS NULL OR [PageCount] >= 1");
                    table.ForeignKey(
                        name: "FK_NotesV3DocumentVersions_NotesV3Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "NotesV3Documents",
                        principalColumn: "DocumentId");
                });

            migrationBuilder.CreateTable(
                name: "NotesV3Sections",
                columns: table => new
                {
                    SectionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotebookId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3Sections", x => x.SectionId);
                    table.CheckConstraint("CK_NotesV3Sections_SortOrder", "[SortOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_NotesV3Sections_NotesV3Notebooks_NotebookId",
                        column: x => x.NotebookId,
                        principalTable: "NotesV3Notebooks",
                        principalColumn: "NotebookId");
                });

            migrationBuilder.CreateTable(
                name: "NotesV3Annotations",
                columns: table => new
                {
                    AnnotationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentVersionId = table.Column<long>(type: "bigint", nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: true),
                    GroupDocumentShareId = table.Column<long>(type: "bigint", nullable: true),
                    PhysicalPageNumber = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3Annotations", x => x.AnnotationId);
                    table.CheckConstraint("CK_NotesV3Annotations_PayloadJson", "ISJSON([PayloadJson]) = 1");
                    table.CheckConstraint("CK_NotesV3Annotations_PhysicalPage", "[PhysicalPageNumber] >= 1");
                    table.CheckConstraint("CK_NotesV3Annotations_Scope", "([OwnerUserId] IS NOT NULL AND [GroupDocumentShareId] IS NULL)\r\nOR\r\n([OwnerUserId] IS NULL AND [GroupDocumentShareId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_NotesV3Annotations_NotesV3DocumentShares_GroupDocumentShareId",
                        column: x => x.GroupDocumentShareId,
                        principalTable: "NotesV3DocumentShares",
                        principalColumn: "GroupDocumentShareId");
                    table.ForeignKey(
                        name: "FK_NotesV3Annotations_NotesV3DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "NotesV3DocumentVersions",
                        principalColumn: "DocumentVersionId");
                    table.ForeignKey(
                        name: "FK_NotesV3Annotations_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "NotesV3Pages",
                columns: table => new
                {
                    PageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ContentDeltaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3Pages", x => x.PageId);
                    table.CheckConstraint("CK_NotesV3Pages_DeltaJson", "ISJSON([ContentDeltaJson]) = 1");
                    table.CheckConstraint("CK_NotesV3Pages_SortOrder", "[SortOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_NotesV3Pages_NotesV3Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "NotesV3Sections",
                        principalColumn: "SectionId");
                });

            migrationBuilder.CreateTable(
                name: "NotesV3CollabUpdates",
                columns: table => new
                {
                    UpdateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageId = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: false),
                    UpdateBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3CollabUpdates", x => x.UpdateId);
                    table.CheckConstraint("CK_NotesV3CollabUpdates_Bytes", "DATALENGTH([UpdateBytes]) BETWEEN 1 AND 262144");
                    table.CheckConstraint("CK_NotesV3CollabUpdates_Operation", "[OperationId] <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "FK_NotesV3CollabUpdates_NotesV3Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "NotesV3Pages",
                        principalColumn: "PageId");
                    table.ForeignKey(
                        name: "FK_NotesV3CollabUpdates_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "NotesV3PdfPageLinks",
                columns: table => new
                {
                    PageId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentVersionId = table.Column<long>(type: "bigint", nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: true),
                    GroupDocumentShareId = table.Column<long>(type: "bigint", nullable: true),
                    PhysicalPageNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3PdfPageLinks", x => x.PageId);
                    table.CheckConstraint("CK_NotesV3PdfPageLinks_PhysicalPage", "[PhysicalPageNumber] >= 1");
                    table.CheckConstraint("CK_NotesV3PdfPageLinks_Scope", "([OwnerUserId] IS NOT NULL AND [GroupDocumentShareId] IS NULL)\r\nOR\r\n([OwnerUserId] IS NULL AND [GroupDocumentShareId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_NotesV3PdfPageLinks_NotesV3DocumentShares_GroupDocumentShareId",
                        column: x => x.GroupDocumentShareId,
                        principalTable: "NotesV3DocumentShares",
                        principalColumn: "GroupDocumentShareId");
                    table.ForeignKey(
                        name: "FK_NotesV3PdfPageLinks_NotesV3DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "NotesV3DocumentVersions",
                        principalColumn: "DocumentVersionId");
                    table.ForeignKey(
                        name: "FK_NotesV3PdfPageLinks_NotesV3Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "NotesV3Pages",
                        principalColumn: "PageId");
                    table.ForeignKey(
                        name: "FK_NotesV3PdfPageLinks_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "NotesV3References",
                columns: table => new
                {
                    ReferenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentVersionId = table.Column<long>(type: "bigint", nullable: false),
                    GroupDocumentShareId = table.Column<long>(type: "bigint", nullable: true),
                    PhysicalPageNumber = table.Column<int>(type: "int", nullable: false),
                    AnchorJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CapturedText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3References", x => x.ReferenceId);
                    table.CheckConstraint("CK_NotesV3References_AnchorJson", "[AnchorJson] IS NULL OR ISJSON([AnchorJson]) = 1");
                    table.CheckConstraint("CK_NotesV3References_PhysicalPage", "[PhysicalPageNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_NotesV3References_NotesV3DocumentShares_GroupDocumentShareId",
                        column: x => x.GroupDocumentShareId,
                        principalTable: "NotesV3DocumentShares",
                        principalColumn: "GroupDocumentShareId");
                    table.ForeignKey(
                        name: "FK_NotesV3References_NotesV3DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "NotesV3DocumentVersions",
                        principalColumn: "DocumentVersionId");
                    table.ForeignKey(
                        name: "FK_NotesV3References_NotesV3Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "NotesV3Pages",
                        principalColumn: "PageId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Annotations_DocumentVersionId",
                table: "NotesV3Annotations",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Annotations_GroupDocumentShareId_DocumentVersionId_PhysicalPageNumber_IsDeleted",
                table: "NotesV3Annotations",
                columns: new[] { "GroupDocumentShareId", "DocumentVersionId", "PhysicalPageNumber", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Annotations_OwnerUserId_DocumentVersionId_PhysicalPageNumber_IsDeleted",
                table: "NotesV3Annotations",
                columns: new[] { "OwnerUserId", "DocumentVersionId", "PhysicalPageNumber", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3CollabUpdates_ActorUserId",
                table: "NotesV3CollabUpdates",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3CollabUpdates_PageId_OperationId",
                table: "NotesV3CollabUpdates",
                columns: new[] { "PageId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3CollabUpdates_PageId_UpdateId",
                table: "NotesV3CollabUpdates",
                columns: new[] { "PageId", "UpdateId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Documents_OwnerUserId_IsDeleted_IsArchived_UpdatedAtUtc",
                table: "NotesV3Documents",
                columns: new[] { "OwnerUserId", "IsDeleted", "IsArchived", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3DocumentShares_DocumentId_StudyGroupId",
                table: "NotesV3DocumentShares",
                columns: new[] { "DocumentId", "StudyGroupId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3DocumentShares_SharedByUserId",
                table: "NotesV3DocumentShares",
                column: "SharedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3DocumentShares_StudyGroupId_IsActive",
                table: "NotesV3DocumentShares",
                columns: new[] { "StudyGroupId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3DocumentVersions_DocumentId_DocumentVersionId",
                table: "NotesV3DocumentVersions",
                columns: new[] { "DocumentId", "DocumentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3DocumentVersions_StorageKey",
                table: "NotesV3DocumentVersions",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Notebooks_OwnerUserId_IsDeleted_SortOrder",
                table: "NotesV3Notebooks",
                columns: new[] { "OwnerUserId", "IsDeleted", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Notebooks_StudyGroupId_IsDeleted_SortOrder",
                table: "NotesV3Notebooks",
                columns: new[] { "StudyGroupId", "IsDeleted", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Pages_SectionId_IsDeleted_SortOrder",
                table: "NotesV3Pages",
                columns: new[] { "SectionId", "IsDeleted", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3PdfPageLinks_DocumentVersionId",
                table: "NotesV3PdfPageLinks",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3PdfPageLinks_GroupDocumentShareId_DocumentVersionId_PhysicalPageNumber",
                table: "NotesV3PdfPageLinks",
                columns: new[] { "GroupDocumentShareId", "DocumentVersionId", "PhysicalPageNumber" },
                unique: true,
                filter: "[GroupDocumentShareId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3PdfPageLinks_OwnerUserId_DocumentVersionId_PhysicalPageNumber",
                table: "NotesV3PdfPageLinks",
                columns: new[] { "OwnerUserId", "DocumentVersionId", "PhysicalPageNumber" },
                unique: true,
                filter: "[OwnerUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3References_DocumentVersionId",
                table: "NotesV3References",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3References_GroupDocumentShareId",
                table: "NotesV3References",
                column: "GroupDocumentShareId");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3References_PageId",
                table: "NotesV3References",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Sections_NotebookId_IsDeleted_SortOrder",
                table: "NotesV3Sections",
                columns: new[] { "NotebookId", "IsDeleted", "SortOrder" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotesV3Annotations");

            migrationBuilder.DropTable(
                name: "NotesV3CollabUpdates");

            migrationBuilder.DropTable(
                name: "NotesV3PdfPageLinks");

            migrationBuilder.DropTable(
                name: "NotesV3References");

            migrationBuilder.DropTable(
                name: "NotesV3DocumentShares");

            migrationBuilder.DropTable(
                name: "NotesV3DocumentVersions");

            migrationBuilder.DropTable(
                name: "NotesV3Pages");

            migrationBuilder.DropTable(
                name: "NotesV3Documents");

            migrationBuilder.DropTable(
                name: "NotesV3Sections");

            migrationBuilder.DropTable(
                name: "NotesV3Notebooks");
        }
    }
}

