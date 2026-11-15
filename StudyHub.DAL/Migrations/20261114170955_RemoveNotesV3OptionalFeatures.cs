using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotesV3OptionalFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotesV3Bookmarks");

            migrationBuilder.DropTable(
                name: "NotesV3DocumentPageTexts");

            migrationBuilder.DropIndex(
                name: "IX_NotesV3Documents_OwnerUserId_IsDeleted_IsArchived_UpdatedAtUtc",
                table: "NotesV3Documents");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "NotesV3Documents");

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Documents_OwnerUserId_IsDeleted_UpdatedAtUtc",
                table: "NotesV3Documents",
                columns: new[] { "OwnerUserId", "IsDeleted", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotesV3Documents_OwnerUserId_IsDeleted_UpdatedAtUtc",
                table: "NotesV3Documents");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "NotesV3Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "NotesV3Bookmarks",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    PhysicalPageNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3Bookmarks", x => new { x.UserId, x.DocumentId, x.PhysicalPageNumber });
                    table.CheckConstraint("CK_NotesV3Bookmarks_Page", "[PhysicalPageNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_NotesV3Bookmarks_NotesV3Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "NotesV3Documents",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_NotesV3Bookmarks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "NotesV3DocumentPageTexts",
                columns: table => new
                {
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    PhysicalPageNumber = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesV3DocumentPageTexts", x => new { x.DocumentId, x.PhysicalPageNumber });
                    table.CheckConstraint("CK_NotesV3DocumentPageTexts_Page", "[PhysicalPageNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_NotesV3DocumentPageTexts_NotesV3Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "NotesV3Documents",
                        principalColumn: "DocumentId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Documents_OwnerUserId_IsDeleted_IsArchived_UpdatedAtUtc",
                table: "NotesV3Documents",
                columns: new[] { "OwnerUserId", "IsDeleted", "IsArchived", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesV3Bookmarks_DocumentId",
                table: "NotesV3Bookmarks",
                column: "DocumentId");
        }
    }
}
