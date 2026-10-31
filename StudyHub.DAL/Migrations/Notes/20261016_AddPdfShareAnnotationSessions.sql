/* Notes/PDF schema migration. REVIEW ONLY until the target DB and restoreable backup are confirmed.
   Existing GroupAnnotationSets are retained as legacy history. A share with any earlier share
   for the same document/group starts with an empty annotation layer to prevent old data leakage. */
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.GroupDocumentShares', N'U') IS NULL
    THROW 51000, 'GroupDocumentShares is missing; apply the existing Notes schema first.', 1;
IF OBJECT_ID(N'dbo.GroupAnnotationSets', N'U') IS NULL
    THROW 51000, 'GroupAnnotationSets is missing; apply the existing Notes schema first.', 1;
IF OBJECT_ID(N'dbo.DocumentVersions', N'U') IS NULL
    THROW 51000, 'DocumentVersions is missing; apply the existing Notes schema first.', 1;
IF OBJECT_ID(N'dbo.GroupAnnotationSessions', N'U') IS NOT NULL
    THROW 51000, 'GroupAnnotationSessions already exists; review its schema and data before rerunning.', 1;

BEGIN TRANSACTION;

CREATE TABLE dbo.GroupAnnotationSessions
(
    GroupDocumentShareId bigint NOT NULL,
    DocumentVersionId bigint NOT NULL,
    PayloadJson nvarchar(max) NOT NULL,
    UpdatedAtUtc datetime2(3) NOT NULL
        CONSTRAINT DF_GroupAnnotationSessions_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_GroupAnnotationSessions PRIMARY KEY (GroupDocumentShareId, DocumentVersionId),
    CONSTRAINT CK_GroupAnnotationSessions_PayloadJson CHECK (ISJSON(PayloadJson) = 1),
    CONSTRAINT FK_GroupAnnotationSessions_Share FOREIGN KEY (GroupDocumentShareId)
        REFERENCES dbo.GroupDocumentShares(GroupDocumentShareId),
    CONSTRAINT FK_GroupAnnotationSessions_Version FOREIGN KEY (DocumentVersionId)
        REFERENCES dbo.DocumentVersions(DocumentVersionId)
);

CREATE INDEX IX_GroupAnnotationSessions_DocumentVersionId
    ON dbo.GroupAnnotationSessions(DocumentVersionId);

INSERT dbo.GroupAnnotationSessions
    (GroupDocumentShareId, DocumentVersionId, PayloadJson, UpdatedAtUtc)
SELECT
    share.GroupDocumentShareId,
    version.DocumentVersionId,
    CASE WHEN EXISTS
    (
        SELECT 1 FROM dbo.GroupDocumentShares AS previous
        WHERE previous.DocumentId = share.DocumentId
          AND previous.StudyGroupId = share.StudyGroupId
          AND previous.GroupDocumentShareId < share.GroupDocumentShareId
    ) THEN N'[]' ELSE COALESCE(legacy.PayloadJson, N'[]') END,
    CASE WHEN EXISTS
    (
        SELECT 1 FROM dbo.GroupDocumentShares AS previous
        WHERE previous.DocumentId = share.DocumentId
          AND previous.StudyGroupId = share.StudyGroupId
          AND previous.GroupDocumentShareId < share.GroupDocumentShareId
    ) THEN SYSUTCDATETIME() ELSE COALESCE(legacy.UpdatedAtUtc, SYSUTCDATETIME()) END
FROM dbo.GroupDocumentShares AS share
JOIN dbo.DocumentVersions AS version ON version.DocumentId = share.DocumentId
LEFT JOIN dbo.GroupAnnotationSets AS legacy
    ON legacy.StudyGroupId = share.StudyGroupId
   AND legacy.DocumentVersionId = version.DocumentVersionId
WHERE share.IsActive = 1;

COMMIT TRANSACTION;

SELECT COUNT(*) AS ActiveSessionLayers FROM dbo.GroupAnnotationSessions;
