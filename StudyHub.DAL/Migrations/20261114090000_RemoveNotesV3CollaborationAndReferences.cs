using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyHub.DAL.Persistence;

#nullable disable

namespace StudyHub.DAL.Migrations;

[DbContext(typeof(StudyHubDbContext))]
[Migration("20261114090000_RemoveNotesV3CollaborationAndReferences")]
public partial class RemoveNotesV3CollaborationAndReferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "NotesV3CollabUpdates");

        migrationBuilder.DropTable(
            name: "NotesV3References");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
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
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false,
                    defaultValueSql: "SYSUTCDATETIME()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotesV3CollabUpdates", x => x.UpdateId);
                table.CheckConstraint(
                    "CK_NotesV3CollabUpdates_Bytes",
                    "DATALENGTH([UpdateBytes]) BETWEEN 1 AND 262144");
                table.CheckConstraint(
                    "CK_NotesV3CollabUpdates_Operation",
                    "[OperationId] <> '00000000-0000-0000-0000-000000000000'");
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
                CapturedText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000,
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotesV3References", x => x.ReferenceId);
                table.CheckConstraint(
                    "CK_NotesV3References_AnchorJson",
                    "[AnchorJson] IS NULL OR ISJSON([AnchorJson]) = 1");
                table.CheckConstraint(
                    "CK_NotesV3References_PhysicalPage",
                    "[PhysicalPageNumber] >= 1");
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
    }
}
