/* REVIEW ONLY. Confirm database target, restorable backup and pending Notes migrations before applying.
   Backfill only notes referencing exactly one PDF page when no other note claims that page.
   Ambiguous legacy notes remain standalone for manual review; no content is merged or deleted. */
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Notes', N'U') IS NULL OR OBJECT_ID(N'dbo.Documents', N'U') IS NULL
    THROW 51003, 'Notes or Documents is missing.', 1;
IF OBJECT_ID(N'dbo.PdfPageNotes', N'U') IS NOT NULL
    THROW 51003, 'PdfPageNotes already exists; review before rerunning.', 1;

BEGIN TRANSACTION;
CREATE TABLE dbo.PdfPageNotes
(
    NoteId bigint NOT NULL CONSTRAINT PK_PdfPageNotes PRIMARY KEY,
    OwnerUserId bigint NOT NULL,
    DocumentId bigint NOT NULL,
    PageIndex int NOT NULL,
    CONSTRAINT CK_PdfPageNotes_PageIndex CHECK (PageIndex >= 0),
    CONSTRAINT FK_PdfPageNotes_Note FOREIGN KEY (NoteId) REFERENCES dbo.Notes(NoteId) ON DELETE CASCADE,
    CONSTRAINT FK_PdfPageNotes_Document FOREIGN KEY (DocumentId) REFERENCES dbo.Documents(DocumentId)
);
CREATE UNIQUE INDEX UX_PdfPageNotes_Owner_Document_Page
    ON dbo.PdfPageNotes(OwnerUserId, DocumentId, PageIndex);
;WITH Candidates AS
(
    SELECT n.NoteId, n.OwnerUserId, MIN(v.DocumentId) AS DocumentId, MIN(r.PageIndex) AS PageIndex
    FROM dbo.Notes n
    JOIN dbo.NoteReferenceLinks r ON r.NoteId = n.NoteId
    JOIN dbo.DocumentVersions v ON v.DocumentVersionId = r.DocumentVersionId
    JOIN dbo.Documents d ON d.DocumentId = v.DocumentId AND d.OwnerUserId = n.OwnerUserId
    WHERE n.IsDeleted = 0 AND n.StudyGroupId IS NULL AND n.Visibility = 'Private'
      AND d.IsDeleted = 0 AND d.ResourceType = 'File'
    GROUP BY n.NoteId, n.OwnerUserId
    HAVING MIN(v.DocumentId) = MAX(v.DocumentId) AND MIN(r.PageIndex) = MAX(r.PageIndex)
), UniquePages AS
(
    SELECT OwnerUserId, DocumentId, PageIndex
    FROM Candidates
    GROUP BY OwnerUserId, DocumentId, PageIndex
    HAVING COUNT(*) = 1
)
INSERT dbo.PdfPageNotes(NoteId, OwnerUserId, DocumentId, PageIndex)
SELECT c.NoteId, c.OwnerUserId, c.DocumentId, c.PageIndex
FROM Candidates c
JOIN UniquePages u ON u.OwnerUserId = c.OwnerUserId AND u.DocumentId = c.DocumentId
    AND u.PageIndex = c.PageIndex;
DECLARE @Backfilled int = @@ROWCOUNT;
COMMIT TRANSACTION;

SELECT @Backfilled AS NotesAssignedToPdfPages, COUNT(*) AS LegacyNotesWithPdfReferences
FROM dbo.Notes n
WHERE NOT EXISTS (SELECT 1 FROM dbo.PdfPageNotes p WHERE p.NoteId = n.NoteId)
  AND EXISTS (SELECT 1 FROM dbo.NoteReferenceLinks r WHERE r.NoteId = n.NoteId);
