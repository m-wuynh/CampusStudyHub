/* M1 PDF Library schema. Run once after the base StudyHub schema. */
IF COL_LENGTH('dbo.Documents', 'IsArchived') IS NULL
    ALTER TABLE dbo.Documents ADD IsArchived bit NOT NULL
        CONSTRAINT DF_Documents_IsArchived DEFAULT (0) WITH VALUES;
GO

IF OBJECT_ID('dbo.DocumentVersions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentVersions (
        DocumentVersionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentVersions PRIMARY KEY,
        DocumentId bigint NOT NULL,
        StorageKey nvarchar(512) NOT NULL,
        Sha256 varchar(64) NOT NULL,
        OriginalFileName nvarchar(255) NOT NULL,
        MimeType varchar(150) NOT NULL CONSTRAINT DF_DocumentVersions_MimeType DEFAULT ('application/pdf'),
        FileSizeBytes bigint NOT NULL,
        PageCount int NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DocumentVersions_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_DocumentVersions_StorageKey UNIQUE (StorageKey),
        CONSTRAINT FK_DocumentVersions_Document FOREIGN KEY (DocumentId)
            REFERENCES dbo.Documents(DocumentId) ON DELETE CASCADE,
        CONSTRAINT CK_DocumentVersions_Size CHECK (FileSizeBytes > 0)
    );
    CREATE INDEX IX_DocumentVersions_Document_Created
        ON dbo.DocumentVersions(DocumentId, CreatedAtUtc DESC, DocumentVersionId DESC);
END;
GO

IF OBJECT_ID('dbo.DocumentAnnotations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentAnnotations (
        DocumentVersionId bigint NOT NULL,
        OwnerUserId bigint NOT NULL,
        AnnotationKey varchar(100) NOT NULL,
        PageIndex int NOT NULL,
        PayloadJson nvarchar(max) NOT NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DocumentAnnotations_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DocumentAnnotations PRIMARY KEY (DocumentVersionId, OwnerUserId, AnnotationKey),
        CONSTRAINT FK_DocumentAnnotations_Version FOREIGN KEY (DocumentVersionId)
            REFERENCES dbo.DocumentVersions(DocumentVersionId) ON DELETE CASCADE,
        CONSTRAINT FK_DocumentAnnotations_User FOREIGN KEY (OwnerUserId)
            REFERENCES dbo.Users(UserId) ON DELETE CASCADE,
        CONSTRAINT CK_DocumentAnnotations_Page CHECK (PageIndex >= 0),
        CONSTRAINT CK_DocumentAnnotations_Json CHECK (ISJSON(PayloadJson) = 1)
    );
    CREATE INDEX IX_DocumentAnnotations_Owner_Version
        ON dbo.DocumentAnnotations(OwnerUserId, DocumentVersionId);
END;
GO
