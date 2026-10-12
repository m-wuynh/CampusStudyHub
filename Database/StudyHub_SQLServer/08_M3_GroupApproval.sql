/* M3/M4 Notes group sharing + controlled approval. Apply after 07_M2_NotesReferences.sql. */
IF OBJECT_ID('dbo.GroupDocumentShares', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GroupDocumentShares (
        GroupDocumentShareId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GroupDocumentShares PRIMARY KEY,
        DocumentId bigint NOT NULL,
        StudyGroupId bigint NOT NULL,
        SharedByUserId bigint NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_GroupDocumentShares_IsActive DEFAULT (1),
        SharedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GroupDocumentShares_Shared DEFAULT (SYSUTCDATETIME()),
        UnsharedAtUtc datetime2(3) NULL,
        CONSTRAINT FK_GroupDocumentShares_Document FOREIGN KEY (DocumentId) REFERENCES dbo.Documents(DocumentId),
        CONSTRAINT FK_GroupDocumentShares_Group FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId),
        CONSTRAINT FK_GroupDocumentShares_User FOREIGN KEY (SharedByUserId) REFERENCES dbo.Users(UserId)
    );
    CREATE UNIQUE INDEX UX_GroupDocumentShares_Active ON dbo.GroupDocumentShares(DocumentId, StudyGroupId) WHERE IsActive = 1;
    CREATE INDEX IX_GroupDocumentShares_Group_Active ON dbo.GroupDocumentShares(StudyGroupId, IsActive, SharedAtUtc DESC);
END;
GO

IF OBJECT_ID('dbo.GroupNotes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GroupNotes (
        GroupNoteId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GroupNotes PRIMARY KEY,
        StudyGroupId bigint NOT NULL,
        OwnerUserId bigint NOT NULL,
        SourceNoteId bigint NULL,
        Title nvarchar(200) NOT NULL,
        ContentDeltaJson nvarchar(max) NOT NULL,
        IsArchived bit NOT NULL CONSTRAINT DF_GroupNotes_IsArchived DEFAULT (0),
        IsDeleted bit NOT NULL CONSTRAINT DF_GroupNotes_IsDeleted DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GroupNotes_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GroupNotes_Updated DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_GroupNotes_Group FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId),
        CONSTRAINT FK_GroupNotes_Owner FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_GroupNotes_SourceNote FOREIGN KEY (SourceNoteId) REFERENCES dbo.Notes(NoteId) ON DELETE SET NULL,
        CONSTRAINT CK_GroupNotes_ContentDeltaJson CHECK (ISJSON(ContentDeltaJson) = 1)
    );
    CREATE INDEX IX_GroupNotes_Group_Active ON dbo.GroupNotes(StudyGroupId, IsDeleted, UpdatedAtUtc DESC);
    CREATE UNIQUE INDEX UX_GroupNotes_Source_Group_Active ON dbo.GroupNotes(SourceNoteId, StudyGroupId)
        WHERE SourceNoteId IS NOT NULL AND IsDeleted = 0;
END;
GO

IF OBJECT_ID('dbo.GroupAnnotationSets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GroupAnnotationSets (
        StudyGroupId bigint NOT NULL,
        DocumentVersionId bigint NOT NULL,
        PayloadJson nvarchar(max) NOT NULL CONSTRAINT DF_GroupAnnotationSets_Payload DEFAULT ('[]'),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GroupAnnotationSets_Updated DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_GroupAnnotationSets PRIMARY KEY (StudyGroupId, DocumentVersionId),
        CONSTRAINT FK_GroupAnnotationSets_Group FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId),
        CONSTRAINT FK_GroupAnnotationSets_Version FOREIGN KEY (DocumentVersionId) REFERENCES dbo.DocumentVersions(DocumentVersionId),
        CONSTRAINT CK_GroupAnnotationSets_PayloadJson CHECK (ISJSON(PayloadJson) = 1)
    );
END;
GO

IF OBJECT_ID('dbo.GroupChangeRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GroupChangeRequests (
        GroupChangeRequestId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GroupChangeRequests PRIMARY KEY,
        StudyGroupId bigint NOT NULL,
        RequesterUserId bigint NOT NULL,
        ReviewerUserId bigint NULL,
        TargetGroupNoteId bigint NULL,
        DocumentVersionId bigint NULL,
        TargetType varchar(20) NOT NULL,
        Operation varchar(10) NOT NULL,
        ProposedTitle nvarchar(200) NULL,
        ProposedPayloadJson nvarchar(max) NULL,
        BaseVersionToken varchar(24) NULL,
        Status varchar(12) NOT NULL CONSTRAINT DF_GroupChangeRequests_Status DEFAULT ('Pending'),
        ResolutionNote nvarchar(1000) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GroupChangeRequests_Created DEFAULT (SYSUTCDATETIME()),
        ResolvedAtUtc datetime2(3) NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_GroupChangeRequests_Group FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId),
        CONSTRAINT FK_GroupChangeRequests_Requester FOREIGN KEY (RequesterUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_GroupChangeRequests_Reviewer FOREIGN KEY (ReviewerUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_GroupChangeRequests_GroupNote FOREIGN KEY (TargetGroupNoteId) REFERENCES dbo.GroupNotes(GroupNoteId),
        CONSTRAINT FK_GroupChangeRequests_Version FOREIGN KEY (DocumentVersionId) REFERENCES dbo.DocumentVersions(DocumentVersionId),
        CONSTRAINT CK_GroupChangeRequests_Target CHECK
            ((TargetType = 'GroupNote' AND DocumentVersionId IS NULL AND
                ((Operation = 'CREATE' AND TargetGroupNoteId IS NULL) OR
                 (Operation IN ('UPDATE','DELETE') AND TargetGroupNoteId IS NOT NULL))) OR
             (TargetType = 'GroupAnnotation' AND TargetGroupNoteId IS NULL AND DocumentVersionId IS NOT NULL)),
        CONSTRAINT CK_GroupChangeRequests_Operation CHECK (Operation IN ('CREATE','UPDATE','DELETE')),
        CONSTRAINT CK_GroupChangeRequests_Status CHECK (Status IN ('Pending','Approved','Rejected','Conflict','Cancelled')),
        CONSTRAINT CK_GroupChangeRequests_ProposedJson CHECK (ProposedPayloadJson IS NULL OR ISJSON(ProposedPayloadJson) = 1),
        CONSTRAINT CK_GroupChangeRequests_ProposedContent CHECK
            ((TargetType = 'GroupNote' AND (Operation = 'DELETE' OR (ProposedTitle IS NOT NULL AND ProposedPayloadJson IS NOT NULL))) OR
             (TargetType = 'GroupAnnotation' AND ProposedPayloadJson IS NOT NULL)),
        CONSTRAINT CK_GroupChangeRequests_BaseVersion CHECK
            ((TargetType = 'GroupNote' AND Operation = 'CREATE' AND BaseVersionToken IS NULL) OR
             ((TargetType = 'GroupNote' AND Operation IN ('UPDATE','DELETE') OR TargetType = 'GroupAnnotation')
                AND BaseVersionToken IS NOT NULL))
    );
    CREATE INDEX IX_GroupChangeRequests_Group_Status_Created
        ON dbo.GroupChangeRequests(StudyGroupId, Status, CreatedAtUtc DESC);
END;
GO
