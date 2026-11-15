using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations;

public partial class RenamePdfModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RenameModuleSql(
            oldPrefix: "NotesV3",
            newPrefix: "Pdf",
            oldPageNoteTable: "NotesV3PdfPageNotes",
            newPageNoteTable: "PdfDocumentPageNotes",
            oldPageNoteKey: "PdfPageNoteId",
            newPageNoteKey: "PdfDocumentPageNoteId"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RenameModuleSql(
            oldPrefix: "Pdf",
            newPrefix: "NotesV3",
            oldPageNoteTable: "PdfDocumentPageNotes",
            newPageNoteTable: "NotesV3PdfPageNotes",
            oldPageNoteKey: "PdfDocumentPageNoteId",
            newPageNoteKey: "PdfPageNoteId"));
    }

    private static string RenameModuleSql(
        string oldPrefix,
        string newPrefix,
        string oldPageNoteTable,
        string newPageNoteTable,
        string oldPageNoteKey,
        string newPageNoteKey)
    {
        var oldDocuments = $"{oldPrefix}Documents";
        var newDocuments = $"{newPrefix}Documents";
        var oldShares = $"{oldPrefix}DocumentShares";
        var newShares = $"{newPrefix}DocumentShares";
        var oldAnnotations = $"{oldPrefix}Annotations";
        var newAnnotations = $"{newPrefix}Annotations";

        var renames = new[]
        {
            Table(oldDocuments, newDocuments),
            Table(oldShares, newShares),
            Table(oldAnnotations, newAnnotations),
            Table(oldPageNoteTable, newPageNoteTable),
            Column(newPageNoteTable, oldPageNoteKey, newPageNoteKey),

            Object(newDocuments, $"PK_{oldDocuments}", $"PK_{newDocuments}"),
            Object(newShares, $"PK_{oldShares}", $"PK_{newShares}"),
            Object(newAnnotations, $"PK_{oldAnnotations}", $"PK_{newAnnotations}"),
            Object(newPageNoteTable, $"PK_{oldPageNoteTable}", $"PK_{newPageNoteTable}"),

            Object(newDocuments, $"CK_{oldDocuments}_PageCount", $"CK_{newDocuments}_PageCount"),
            Object(newDocuments, $"CK_{oldDocuments}_FileSizeBytes", $"CK_{newDocuments}_FileSizeBytes"),
            Object(newShares, $"CK_{oldShares}_State", $"CK_{newShares}_State"),
            Object(newAnnotations, $"CK_{oldAnnotations}_PhysicalPage", $"CK_{newAnnotations}_PhysicalPage"),
            Object(newAnnotations, $"CK_{oldAnnotations}_PayloadJson", $"CK_{newAnnotations}_PayloadJson"),
            Object(newPageNoteTable, $"CK_{oldPageNoteTable}_PhysicalPage", $"CK_{newPageNoteTable}_PhysicalPage"),

            Object(newDocuments, $"FK_{oldDocuments}_Users_OwnerUserId", $"FK_{newDocuments}_Users_OwnerUserId"),
            Object(newShares, $"FK_{oldShares}_{oldDocuments}_DocumentId", $"FK_{newShares}_{newDocuments}_DocumentId"),
            Object(newShares, $"FK_{oldShares}_StudyGroups_StudyGroupId", $"FK_{newShares}_StudyGroups_StudyGroupId"),
            Object(newShares, $"FK_{oldShares}_Users_SharedByUserId", $"FK_{newShares}_Users_SharedByUserId"),
            Object(newAnnotations, $"FK_{oldAnnotations}_{oldDocuments}_DocumentId", $"FK_{newAnnotations}_{newDocuments}_DocumentId"),
            Object(newAnnotations, $"FK_{oldAnnotations}_Users_OwnerUserId", $"FK_{newAnnotations}_Users_OwnerUserId"),
            Object(newPageNoteTable, $"FK_{oldPageNoteTable}_{oldDocuments}_DocumentId", $"FK_{newPageNoteTable}_{newDocuments}_DocumentId"),
            Object(newPageNoteTable, $"FK_{oldPageNoteTable}_Users_OwnerUserId", $"FK_{newPageNoteTable}_Users_OwnerUserId"),

            Index(newDocuments, $"IX_{oldDocuments}_StorageKey", $"IX_{newDocuments}_StorageKey"),
            Index(newDocuments, $"IX_{oldDocuments}_OwnerUserId_OriginalFileNameKey", $"IX_{newDocuments}_OwnerUserId_OriginalFileNameKey"),
            Index(newDocuments, $"IX_{oldDocuments}_OwnerUserId_IsDeleted_UpdatedAtUtc", $"IX_{newDocuments}_OwnerUserId_IsDeleted_UpdatedAtUtc"),
            Index(newShares, $"IX_{oldShares}_DocumentId_StudyGroupId", $"IX_{newShares}_DocumentId_StudyGroupId"),
            Index(newShares, $"IX_{oldShares}_SharedByUserId", $"IX_{newShares}_SharedByUserId"),
            Index(newShares, $"IX_{oldShares}_StudyGroupId_IsActive", $"IX_{newShares}_StudyGroupId_IsActive"),
            Index(newAnnotations, $"IX_{oldAnnotations}_DocumentId", $"IX_{newAnnotations}_DocumentId"),
            Index(newAnnotations, $"IX_{oldAnnotations}_OwnerUserId_DocumentId_PhysicalPageNumber", $"IX_{newAnnotations}_OwnerUserId_DocumentId_PhysicalPageNumber"),
            Index(newPageNoteTable, $"IX_{oldPageNoteTable}_DocumentId", $"IX_{newPageNoteTable}_DocumentId"),
            Index(newPageNoteTable, $"IX_{oldPageNoteTable}_OwnerUserId_DocumentId_PhysicalPageNumber", $"IX_{newPageNoteTable}_OwnerUserId_DocumentId_PhysicalPageNumber")
        };

        return string.Join(Environment.NewLine, renames);
    }

    private static string Table(string oldName, string newName) =>
        $"EXEC sp_rename N'[dbo].[{oldName}]', N'{newName}';";

    private static string Column(string table, string oldName, string newName) =>
        $"EXEC sp_rename N'[dbo].[{table}].[{oldName}]', N'{newName}', N'COLUMN';";

    private static string Object(string _, string oldName, string newName) =>
        $"EXEC sp_rename N'[dbo].[{oldName}]', N'{newName}', N'OBJECT';";

    private static string Index(string table, string oldName, string newName) =>
        $"EXEC sp_rename N'[dbo].[{table}].[{oldName}]', N'{newName}', N'INDEX';";
}
