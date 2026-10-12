/* M2 Notes/Reference schema. Apply after 06_M1_NotesPdf.sql. */
IF COL_LENGTH('dbo.Notes', 'ContentDeltaJson') IS NULL
    ALTER TABLE dbo.Notes ADD ContentDeltaJson nvarchar(max) NULL;
GO

IF COL_LENGTH('dbo.Notes', 'IsArchived') IS NULL
    ALTER TABLE dbo.Notes ADD IsArchived bit NOT NULL
        CONSTRAINT DF_Notes_IsArchived DEFAULT (0) WITH VALUES;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Notes_ContentDeltaJson' AND parent_object_id = OBJECT_ID('dbo.Notes'))
    ALTER TABLE dbo.Notes ADD CONSTRAINT CK_Notes_ContentDeltaJson
        CHECK (ContentDeltaJson IS NULL OR ISJSON(ContentDeltaJson) = 1);
GO

IF OBJECT_ID('dbo.NoteReferenceLinks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NoteReferenceLinks (
        NoteReferenceLinkId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NoteReferenceLinks PRIMARY KEY,
        NoteId bigint NOT NULL,
        DocumentVersionId bigint NOT NULL,
        PageIndex int NOT NULL,
        CaptureType varchar(10) NOT NULL,
        AnchorJson nvarchar(max) NOT NULL,
        CapturedText nvarchar(max) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_NoteReferenceLinks_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_NoteReferenceLinks_Note FOREIGN KEY (NoteId)
            REFERENCES dbo.Notes(NoteId) ON DELETE CASCADE,
        CONSTRAINT FK_NoteReferenceLinks_DocumentVersion FOREIGN KEY (DocumentVersionId)
            REFERENCES dbo.DocumentVersions(DocumentVersionId) ON DELETE CASCADE,
        CONSTRAINT CK_NoteReferenceLinks_PageIndex CHECK (PageIndex >= 0),
        CONSTRAINT CK_NoteReferenceLinks_CaptureType CHECK (CaptureType IN ('Text','Area')),
        CONSTRAINT CK_NoteReferenceLinks_AnchorJson CHECK (ISJSON(AnchorJson) = 1)
    );
    CREATE INDEX IX_NoteReferenceLinks_Note_Created
        ON dbo.NoteReferenceLinks(NoteId, CreatedAtUtc DESC);
END;
GO
