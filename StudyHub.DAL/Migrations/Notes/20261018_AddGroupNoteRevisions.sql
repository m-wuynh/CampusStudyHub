/* REVIEW ONLY. Apply after database target, restorable backup and pending migrations are reviewed.
   Existing notes receive one baseline snapshot. Earlier edits cannot be reconstructed. */
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.GroupNotes', N'U') IS NULL
    THROW 51002, 'GroupNotes is missing.', 1;
IF OBJECT_ID(N'dbo.GroupNoteRevisions', N'U') IS NOT NULL
    THROW 51002, 'GroupNoteRevisions already exists; review before rerunning.', 1;

BEGIN TRANSACTION;
CREATE TABLE dbo.GroupNoteRevisions
(
    GroupNoteRevisionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GroupNoteRevisions PRIMARY KEY,
    GroupNoteId bigint NOT NULL,
    StudyGroupId bigint NOT NULL,
    ActorUserId bigint NULL,
    Operation varchar(24) NOT NULL,
    Title nvarchar(200) NOT NULL,
    ContentDeltaJson nvarchar(max) NOT NULL,
    IsDeleted bit NOT NULL,
    IsArchived bit NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GroupNoteRevisions_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_GroupNoteRevisions_ContentDeltaJson CHECK (ISJSON(ContentDeltaJson) = 1),
    CONSTRAINT FK_GroupNoteRevisions_Note FOREIGN KEY (GroupNoteId) REFERENCES dbo.GroupNotes(GroupNoteId),
    CONSTRAINT FK_GroupNoteRevisions_Group FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId),
    CONSTRAINT FK_GroupNoteRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.Users(UserId)
);
CREATE INDEX IX_GroupNoteRevisions_GroupNoteId_CreatedAtUtc_GroupNoteRevisionId
    ON dbo.GroupNoteRevisions(GroupNoteId, CreatedAtUtc, GroupNoteRevisionId);

INSERT dbo.GroupNoteRevisions
    (GroupNoteId, StudyGroupId, ActorUserId, Operation, Title, ContentDeltaJson,
     IsDeleted, IsArchived, CreatedAtUtc)
SELECT GroupNoteId, StudyGroupId, NULL, 'Baseline', Title, ContentDeltaJson,
       IsDeleted, IsArchived, SYSUTCDATETIME()
FROM dbo.GroupNotes;
COMMIT TRANSACTION;

SELECT COUNT(*) AS BaselineRevisions FROM dbo.GroupNoteRevisions;
