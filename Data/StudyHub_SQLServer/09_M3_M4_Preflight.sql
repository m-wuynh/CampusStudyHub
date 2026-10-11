/* Run after scripts 06, 07 and 08 to catch schema/model drift before the first app run. */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.DocumentVersions', 'U') IS NULL THROW 51000, 'Missing dbo.DocumentVersions: run 06_M1_NotesPdf.sql first.', 1;
IF OBJECT_ID('dbo.NoteReferenceLinks', 'U') IS NULL THROW 51000, 'Missing dbo.NoteReferenceLinks: run 07_M2_NotesReferences.sql first.', 1;
IF OBJECT_ID('dbo.GroupDocumentShares', 'U') IS NULL THROW 51000, 'Missing dbo.GroupDocumentShares: run 08_M3_GroupApproval.sql.', 1;
IF OBJECT_ID('dbo.GroupNotes', 'U') IS NULL THROW 51000, 'Missing dbo.GroupNotes: run 08_M3_GroupApproval.sql.', 1;
IF OBJECT_ID('dbo.GroupAnnotationSets', 'U') IS NULL THROW 51000, 'Missing dbo.GroupAnnotationSets: run 08_M3_GroupApproval.sql.', 1;
IF OBJECT_ID('dbo.GroupChangeRequests', 'U') IS NULL THROW 51000, 'Missing dbo.GroupChangeRequests: run 08_M3_GroupApproval.sql.', 1;

IF COL_LENGTH('dbo.Notes', 'ContentDeltaJson') IS NULL THROW 51000, 'Missing Notes.ContentDeltaJson.', 1;
IF COL_LENGTH('dbo.Notes', 'IsArchived') IS NULL THROW 51000, 'Missing Notes.IsArchived.', 1;
IF COL_LENGTH('dbo.Documents', 'IsArchived') IS NULL THROW 51000, 'Missing Documents.IsArchived.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.NoteReferenceLinks')
      AND name = 'CapturedText' AND system_type_id = TYPE_ID('nvarchar') AND max_length = -1)
    THROW 51000, 'NoteReferenceLinks.CapturedText must be nvarchar(max); do not use nvarchar(12000).', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.GroupNotes') AND name = 'RowVersion' AND system_type_id = TYPE_ID('timestamp'))
    THROW 51000, 'GroupNotes.RowVersion must be SQL Server rowversion.', 1;
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.GroupAnnotationSets') AND name = 'RowVersion' AND system_type_id = TYPE_ID('timestamp'))
    THROW 51000, 'GroupAnnotationSets.RowVersion must be SQL Server rowversion.', 1;
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.GroupChangeRequests') AND name = 'RowVersion' AND system_type_id = TYPE_ID('timestamp'))
    THROW 51000, 'GroupChangeRequests.RowVersion must be SQL Server rowversion.', 1;

SELECT 'M3/M4 schema preflight: PASS' AS Result;
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE (TABLE_NAME IN ('GroupDocumentShares','GroupNotes','GroupAnnotationSets','GroupChangeRequests')
       AND COLUMN_NAME IN ('StudyGroupId','DocumentVersionId','ContentDeltaJson','PayloadJson','ProposedPayloadJson','BaseVersionToken','RowVersion'))
   OR (TABLE_NAME = 'NoteReferenceLinks' AND COLUMN_NAME = 'CapturedText')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
