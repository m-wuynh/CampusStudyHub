/* REVIEW ONLY. Apply after database target, backup and pending migrations are reconciled. */
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.StudyGroups', N'U') IS NULL
    THROW 51001, 'StudyGroups is missing.', 1;
IF OBJECT_ID(N'dbo.NotesOutboxEvents', N'U') IS NOT NULL
    THROW 51001, 'NotesOutboxEvents already exists; review before rerunning.', 1;

BEGIN TRANSACTION;
CREATE TABLE dbo.NotesOutboxEvents
(
    NotesOutboxEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotesOutboxEvents PRIMARY KEY,
    StudyGroupId bigint NOT NULL,
    EventName varchar(64) NOT NULL,
    PayloadJson nvarchar(max) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_NotesOutboxEvents_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    PublishedAtUtc datetime2(3) NULL,
    LeaseUntilUtc datetime2(3) NULL,
    AttemptCount int NOT NULL CONSTRAINT DF_NotesOutboxEvents_AttemptCount DEFAULT (0),
    CONSTRAINT CK_NotesOutboxEvents_PayloadJson CHECK (ISJSON(PayloadJson) = 1),
    CONSTRAINT FK_NotesOutboxEvents_Group FOREIGN KEY (StudyGroupId)
        REFERENCES dbo.StudyGroups(StudyGroupId)
);
CREATE INDEX IX_NotesOutboxEvents_PublishedAtUtc_LeaseUntilUtc_NotesOutboxEventId
    ON dbo.NotesOutboxEvents(PublishedAtUtc, LeaseUntilUtc, NotesOutboxEventId);
COMMIT TRANSACTION;
