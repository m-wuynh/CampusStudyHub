using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyNotesAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Optional legacy tables may already be absent. Drop children before parents.
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[AuditLogs];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[ContentReports];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[DocumentAnnotations];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[DocumentBookmarks];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[GroupAnnotationSessions];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[GroupAnnotationSets];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[GroupChangeRequests];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[GroupNoteRevisions];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[NoteReferenceLinks];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[NotesOutboxEvents];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[PdfPageNotes];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[GroupDocumentShares];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[GroupNotes];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[DocumentVersions];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[Notes];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[Documents];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    ActionCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    EntityKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditLogId);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Actor",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    DocumentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: true),
                    UserSubjectId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExternalUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    MimeType = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ResourceType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    Visibility = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "Private")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_Documents_Group",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_Documents_SubjectOwner",
                        columns: x => new { x.UserSubjectId, x.OwnerUserId },
                        principalTable: "UserSubjects",
                        principalColumns: new[] { "UserSubjectId", "UserId" });
                    table.ForeignKey(
                        name: "FK_Documents_User",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    NoteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: true),
                    UserSubjectId = table.Column<long>(type: "bigint", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    BodyFormat = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "Markdown"),
                    ContentDeltaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    Visibility = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "Private")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.NoteId);
                    table.ForeignKey(
                        name: "FK_Notes_Group",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_Notes_SubjectOwner",
                        columns: x => new { x.UserSubjectId, x.OwnerUserId },
                        principalTable: "UserSubjects",
                        principalColumns: new[] { "UserSubjectId", "UserId" });
                    table.ForeignKey(
                        name: "FK_Notes_User",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "NotesOutboxEvents",
                columns: table => new
                {
                    NotesOutboxEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    EventName = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotesOutboxEvents", x => x.NotesOutboxEventId);
                    table.CheckConstraint("CK_NotesOutboxEvents_PayloadJson", "ISJSON([PayloadJson]) = 1");
                    table.ForeignKey(
                        name: "FK_NotesOutboxEvents_Group",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                });

            migrationBuilder.CreateTable(
                name: "DocumentBookmarks",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentBookmarks", x => new { x.UserId, x.DocumentId });
                    table.ForeignKey(
                        name: "FK_DocumentBookmarks_Document",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_DocumentBookmarks_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    DocumentVersionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.DocumentVersionId);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Document",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                });

            migrationBuilder.CreateTable(
                name: "GroupDocumentShares",
                columns: table => new
                {
                    GroupDocumentShareId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SharedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    SharedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    UnsharedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupDocumentShares", x => x.GroupDocumentShareId);
                    table.ForeignKey(
                        name: "FK_GroupDocumentShares_Document",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_GroupDocumentShares_Group",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_GroupDocumentShares_User",
                        column: x => x.SharedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "ContentReports",
                columns: table => new
                {
                    ReportId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeckId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
                    GroupPostId = table.Column<long>(type: "bigint", nullable: true),
                    NoteId = table.Column<long>(type: "bigint", nullable: true),
                    ReporterUserId = table.Column<long>(type: "bigint", nullable: false),
                    ResolvedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    Status = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false, defaultValue: "Open")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReports", x => x.ReportId);
                    table.ForeignKey(
                        name: "FK_ContentReports_Deck",
                        column: x => x.DeckId,
                        principalTable: "FlashcardDecks",
                        principalColumn: "DeckId");
                    table.ForeignKey(
                        name: "FK_ContentReports_Document",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_ContentReports_Note",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "NoteId");
                    table.ForeignKey(
                        name: "FK_ContentReports_Post",
                        column: x => x.GroupPostId,
                        principalTable: "GroupPosts",
                        principalColumn: "GroupPostId");
                    table.ForeignKey(
                        name: "FK_ContentReports_Reporter",
                        column: x => x.ReporterUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_ContentReports_Resolver",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GroupNotes",
                columns: table => new
                {
                    GroupNoteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContentDeltaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SourceNoteId = table.Column<long>(type: "bigint", nullable: true),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupNotes", x => x.GroupNoteId);
                    table.CheckConstraint("CK_GroupNotes_ContentDeltaJson", "ISJSON([ContentDeltaJson]) = 1");
                    table.ForeignKey(
                        name: "FK_GroupNotes_Group",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_GroupNotes_Owner",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_GroupNotes_SourceNote",
                        column: x => x.SourceNoteId,
                        principalTable: "Notes",
                        principalColumn: "NoteId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PdfPageNotes",
                columns: table => new
                {
                    NoteId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    PageIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfPageNotes", x => x.NoteId);
                    table.CheckConstraint("CK_PdfPageNotes_PageIndex", "[PageIndex] >= 0");
                    table.ForeignKey(
                        name: "FK_PdfPageNotes_Document",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_PdfPageNotes_Note",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "NoteId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAnnotations",
                columns: table => new
                {
                    DocumentVersionId = table.Column<long>(type: "bigint", nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    AnnotationKey = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    PageIndex = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAnnotations", x => new { x.DocumentVersionId, x.OwnerUserId, x.AnnotationKey });
                    table.ForeignKey(
                        name: "FK_DocumentAnnotations_User",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_DocumentAnnotations_Version",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "DocumentVersionId");
                });

            migrationBuilder.CreateTable(
                name: "GroupAnnotationSets",
                columns: table => new
                {
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentVersionId = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupAnnotationSets", x => new { x.StudyGroupId, x.DocumentVersionId });
                    table.CheckConstraint("CK_GroupAnnotationSets_PayloadJson", "ISJSON([PayloadJson]) = 1");
                    table.ForeignKey(
                        name: "FK_GroupAnnotationSets_Group",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_GroupAnnotationSets_Version",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "DocumentVersionId");
                });

            migrationBuilder.CreateTable(
                name: "NoteReferenceLinks",
                columns: table => new
                {
                    NoteReferenceLinkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnchorJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CaptureType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CapturedText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    DocumentVersionId = table.Column<long>(type: "bigint", nullable: false),
                    NoteId = table.Column<long>(type: "bigint", nullable: false),
                    PageIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteReferenceLinks", x => x.NoteReferenceLinkId);
                    table.CheckConstraint("CK_NoteReferenceLinks_AnchorJson", "ISJSON([AnchorJson]) = 1");
                    table.CheckConstraint("CK_NoteReferenceLinks_CaptureType", "[CaptureType] IN ('Text','Area')");
                    table.CheckConstraint("CK_NoteReferenceLinks_PageIndex", "[PageIndex] >= 0");
                    table.ForeignKey(
                        name: "FK_NoteReferenceLinks_DocumentVersion",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "DocumentVersionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NoteReferenceLinks_Note",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "NoteId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupAnnotationSessions",
                columns: table => new
                {
                    GroupDocumentShareId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentVersionId = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupAnnotationSessions", x => new { x.GroupDocumentShareId, x.DocumentVersionId });
                    table.CheckConstraint("CK_GroupAnnotationSessions_PayloadJson", "ISJSON([PayloadJson]) = 1");
                    table.ForeignKey(
                        name: "FK_GroupAnnotationSessions_Share",
                        column: x => x.GroupDocumentShareId,
                        principalTable: "GroupDocumentShares",
                        principalColumn: "GroupDocumentShareId");
                    table.ForeignKey(
                        name: "FK_GroupAnnotationSessions_Version",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "DocumentVersionId");
                });

            migrationBuilder.CreateTable(
                name: "GroupChangeRequests",
                columns: table => new
                {
                    GroupChangeRequestId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseVersionToken = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    DocumentVersionId = table.Column<long>(type: "bigint", nullable: true),
                    Operation = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ProposedPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProposedTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequesterUserId = table.Column<long>(type: "bigint", nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ReviewerUserId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false, defaultValue: "Pending"),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    TargetGroupNoteId = table.Column<long>(type: "bigint", nullable: true),
                    TargetType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupChangeRequests", x => x.GroupChangeRequestId);
                    table.CheckConstraint("CK_GroupChangeRequests_BaseVersion", "([TargetType] = 'GroupNote' AND [Operation] = 'CREATE' AND [BaseVersionToken] IS NULL) OR ((([TargetType] = 'GroupNote' AND [Operation] IN ('UPDATE','DELETE')) OR [TargetType] = 'GroupAnnotation') AND [BaseVersionToken] IS NOT NULL)");
                    table.CheckConstraint("CK_GroupChangeRequests_Operation", "[Operation] IN ('CREATE','UPDATE','DELETE')");
                    table.CheckConstraint("CK_GroupChangeRequests_ProposedContent", "([TargetType] = 'GroupNote' AND ([Operation] = 'DELETE' OR ([ProposedTitle] IS NOT NULL AND [ProposedPayloadJson] IS NOT NULL))) OR ([TargetType] = 'GroupAnnotation' AND [ProposedPayloadJson] IS NOT NULL)");
                    table.CheckConstraint("CK_GroupChangeRequests_ProposedJson", "[ProposedPayloadJson] IS NULL OR ISJSON([ProposedPayloadJson]) = 1");
                    table.CheckConstraint("CK_GroupChangeRequests_Status", "[Status] IN ('Pending','Approved','Rejected','Conflict','Cancelled')");
                    table.CheckConstraint("CK_GroupChangeRequests_Target", "([TargetType] = 'GroupNote' AND [DocumentVersionId] IS NULL AND (([Operation] = 'CREATE' AND [TargetGroupNoteId] IS NULL) OR ([Operation] IN ('UPDATE','DELETE') AND [TargetGroupNoteId] IS NOT NULL))) OR ([TargetType] = 'GroupAnnotation' AND [TargetGroupNoteId] IS NULL AND [DocumentVersionId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GroupChangeRequests_Group",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_GroupChangeRequests_GroupNote",
                        column: x => x.TargetGroupNoteId,
                        principalTable: "GroupNotes",
                        principalColumn: "GroupNoteId");
                    table.ForeignKey(
                        name: "FK_GroupChangeRequests_Requester",
                        column: x => x.RequesterUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_GroupChangeRequests_Reviewer",
                        column: x => x.ReviewerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_GroupChangeRequests_Version",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "DocumentVersionId");
                });

            migrationBuilder.CreateTable(
                name: "GroupNoteRevisions",
                columns: table => new
                {
                    GroupNoteRevisionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    ContentDeltaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    GroupNoteId = table.Column<long>(type: "bigint", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Operation = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    StudyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupNoteRevisions", x => x.GroupNoteRevisionId);
                    table.CheckConstraint("CK_GroupNoteRevisions_ContentDeltaJson", "ISJSON([ContentDeltaJson]) = 1");
                    table.ForeignKey(
                        name: "FK_GroupNoteRevisions_Actor",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_GroupNoteRevisions_Group",
                        column: x => x.StudyGroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "StudyGroupId");
                    table.ForeignKey(
                        name: "FK_GroupNoteRevisions_Note",
                        column: x => x.GroupNoteId,
                        principalTable: "GroupNotes",
                        principalColumn: "GroupNoteId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Date",
                table: "AuditLogs",
                column: "CreatedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_DeckId",
                table: "ContentReports",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_DocumentId",
                table: "ContentReports",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_GroupPostId",
                table: "ContentReports",
                column: "GroupPostId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_NoteId",
                table: "ContentReports",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_Queue",
                table: "ContentReports",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ReporterUserId",
                table: "ContentReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ResolvedByUserId",
                table: "ContentReports",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAnnotations_OwnerUserId",
                table: "DocumentAnnotations",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBookmarks_DocumentId",
                table: "DocumentBookmarks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Group",
                table: "Documents",
                columns: new[] { "StudyGroupId", "Visibility", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Owner",
                table: "Documents",
                columns: new[] { "OwnerUserId", "IsDeleted", "CreatedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Subject",
                table: "Documents",
                columns: new[] { "UserSubjectId", "Visibility", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserSubjectId_OwnerUserId",
                table: "Documents",
                columns: new[] { "UserSubjectId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId_CreatedAtUtc",
                table: "DocumentVersions",
                columns: new[] { "DocumentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_StorageKey",
                table: "DocumentVersions",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupAnnotationSessions_DocumentVersionId",
                table: "GroupAnnotationSessions",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupAnnotationSets_DocumentVersionId",
                table: "GroupAnnotationSets",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupChangeRequests_DocumentVersionId",
                table: "GroupChangeRequests",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupChangeRequests_RequesterUserId",
                table: "GroupChangeRequests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupChangeRequests_ReviewerUserId",
                table: "GroupChangeRequests",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupChangeRequests_StudyGroupId_Status_CreatedAtUtc",
                table: "GroupChangeRequests",
                columns: new[] { "StudyGroupId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupChangeRequests_TargetGroupNoteId",
                table: "GroupChangeRequests",
                column: "TargetGroupNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupDocumentShares_DocumentId_StudyGroupId",
                table: "GroupDocumentShares",
                columns: new[] { "DocumentId", "StudyGroupId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_GroupDocumentShares_SharedByUserId",
                table: "GroupDocumentShares",
                column: "SharedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupDocumentShares_StudyGroupId_IsActive_SharedAtUtc",
                table: "GroupDocumentShares",
                columns: new[] { "StudyGroupId", "IsActive", "SharedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupNoteRevisions_ActorUserId",
                table: "GroupNoteRevisions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupNoteRevisions_GroupNoteId_CreatedAtUtc_GroupNoteRevisionId",
                table: "GroupNoteRevisions",
                columns: new[] { "GroupNoteId", "CreatedAtUtc", "GroupNoteRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupNoteRevisions_StudyGroupId",
                table: "GroupNoteRevisions",
                column: "StudyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupNotes_OwnerUserId",
                table: "GroupNotes",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupNotes_SourceNoteId_StudyGroupId",
                table: "GroupNotes",
                columns: new[] { "SourceNoteId", "StudyGroupId" },
                unique: true,
                filter: "[SourceNoteId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GroupNotes_StudyGroupId_IsDeleted_UpdatedAtUtc",
                table: "GroupNotes",
                columns: new[] { "StudyGroupId", "IsDeleted", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteReferenceLinks_DocumentVersionId",
                table: "NoteReferenceLinks",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteReferenceLinks_Note_Created",
                table: "NoteReferenceLinks",
                columns: new[] { "NoteId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_Group",
                table: "Notes",
                columns: new[] { "StudyGroupId", "Visibility", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_Owner",
                table: "Notes",
                columns: new[] { "OwnerUserId", "IsDeleted", "UpdatedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UserSubjectId_OwnerUserId",
                table: "Notes",
                columns: new[] { "UserSubjectId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesOutboxEvents_PublishedAtUtc_LeaseUntilUtc_NotesOutboxEventId",
                table: "NotesOutboxEvents",
                columns: new[] { "PublishedAtUtc", "LeaseUntilUtc", "NotesOutboxEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotesOutboxEvents_StudyGroupId",
                table: "NotesOutboxEvents",
                column: "StudyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfPageNotes_DocumentId",
                table: "PdfPageNotes",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "UX_PdfPageNotes_Owner_Document_Page",
                table: "PdfPageNotes",
                columns: new[] { "OwnerUserId", "DocumentId", "PageIndex" },
                unique: true);
        }
    }
}
