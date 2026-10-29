BEGIN TRANSACTION;
GO

CREATE TABLE [NotesV3Documents] (
    [DocumentId] bigint NOT NULL IDENTITY,
    [OwnerUserId] bigint NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [IsArchived] bit NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_NotesV3Documents] PRIMARY KEY ([DocumentId]),
    CONSTRAINT [FK_NotesV3Documents_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([UserId])
);
GO

CREATE TABLE [NotesV3Notebooks] (
    [NotebookId] bigint NOT NULL IDENTITY,
    [OwnerUserId] bigint NOT NULL,
    [StudyGroupId] bigint NULL,
    [Title] nvarchar(200) NOT NULL,
    [SortOrder] int NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_NotesV3Notebooks] PRIMARY KEY ([NotebookId]),
    CONSTRAINT [CK_NotesV3Notebooks_SortOrder] CHECK ([SortOrder] >= 0),
    CONSTRAINT [FK_NotesV3Notebooks_StudyGroups_StudyGroupId] FOREIGN KEY ([StudyGroupId]) REFERENCES [StudyGroups] ([StudyGroupId]),
    CONSTRAINT [FK_NotesV3Notebooks_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([UserId])
);
GO

CREATE TABLE [NotesV3DocumentShares] (
    [GroupDocumentShareId] bigint NOT NULL IDENTITY,
    [DocumentId] bigint NOT NULL,
    [StudyGroupId] bigint NOT NULL,
    [SharedByUserId] bigint NOT NULL,
    [IsActive] bit NOT NULL,
    [SharedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UnsharedAtUtc] datetime2 NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_NotesV3DocumentShares] PRIMARY KEY ([GroupDocumentShareId]),
    CONSTRAINT [CK_NotesV3DocumentShares_State] CHECK (([IsActive] = 1 AND [UnsharedAtUtc] IS NULL)
OR
([IsActive] = 0 AND [UnsharedAtUtc] IS NOT NULL)),
    CONSTRAINT [FK_NotesV3DocumentShares_NotesV3Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [NotesV3Documents] ([DocumentId]),
    CONSTRAINT [FK_NotesV3DocumentShares_StudyGroups_StudyGroupId] FOREIGN KEY ([StudyGroupId]) REFERENCES [StudyGroups] ([StudyGroupId]),
    CONSTRAINT [FK_NotesV3DocumentShares_Users_SharedByUserId] FOREIGN KEY ([SharedByUserId]) REFERENCES [Users] ([UserId])
);
GO

CREATE TABLE [NotesV3DocumentVersions] (
    [DocumentVersionId] bigint NOT NULL IDENTITY,
    [DocumentId] bigint NOT NULL,
    [StorageKey] nvarchar(200) NOT NULL,
    [OriginalFileName] nvarchar(200) NOT NULL,
    [Sha256] varchar(64) NOT NULL,
    [FileSizeBytes] bigint NOT NULL,
    [PageCount] int NULL,
    [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_NotesV3DocumentVersions] PRIMARY KEY ([DocumentVersionId]),
    CONSTRAINT [CK_NotesV3DocumentVersions_FileSize] CHECK ([FileSizeBytes] > 0 AND [FileSizeBytes] <= 26214400),
    CONSTRAINT [CK_NotesV3DocumentVersions_PageCount] CHECK ([PageCount] IS NULL OR [PageCount] >= 1),
    CONSTRAINT [FK_NotesV3DocumentVersions_NotesV3Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [NotesV3Documents] ([DocumentId])
);
GO

CREATE TABLE [NotesV3Sections] (
    [SectionId] bigint NOT NULL IDENTITY,
    [NotebookId] bigint NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [SortOrder] int NOT NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_NotesV3Sections] PRIMARY KEY ([SectionId]),
    CONSTRAINT [CK_NotesV3Sections_SortOrder] CHECK ([SortOrder] >= 0),
    CONSTRAINT [FK_NotesV3Sections_NotesV3Notebooks_NotebookId] FOREIGN KEY ([NotebookId]) REFERENCES [NotesV3Notebooks] ([NotebookId])
);
GO

CREATE TABLE [NotesV3Annotations] (
    [AnnotationId] bigint NOT NULL IDENTITY,
    [DocumentVersionId] bigint NOT NULL,
    [OwnerUserId] bigint NULL,
    [GroupDocumentShareId] bigint NULL,
    [PhysicalPageNumber] int NOT NULL,
    [PayloadJson] nvarchar(max) NOT NULL,
    [IsDeleted] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_NotesV3Annotations] PRIMARY KEY ([AnnotationId]),
    CONSTRAINT [CK_NotesV3Annotations_PayloadJson] CHECK (ISJSON([PayloadJson]) = 1),
    CONSTRAINT [CK_NotesV3Annotations_PhysicalPage] CHECK ([PhysicalPageNumber] >= 1),
    CONSTRAINT [CK_NotesV3Annotations_Scope] CHECK (([OwnerUserId] IS NOT NULL AND [GroupDocumentShareId] IS NULL)
OR
([OwnerUserId] IS NULL AND [GroupDocumentShareId] IS NOT NULL)),
    CONSTRAINT [FK_NotesV3Annotations_NotesV3DocumentShares_GroupDocumentShareId] FOREIGN KEY ([GroupDocumentShareId]) REFERENCES [NotesV3DocumentShares] ([GroupDocumentShareId]),
    CONSTRAINT [FK_NotesV3Annotations_NotesV3DocumentVersions_DocumentVersionId] FOREIGN KEY ([DocumentVersionId]) REFERENCES [NotesV3DocumentVersions] ([DocumentVersionId]),
    CONSTRAINT [FK_NotesV3Annotations_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([UserId])
);
GO

CREATE TABLE [NotesV3Pages] (
    [PageId] bigint NOT NULL IDENTITY,
    [SectionId] bigint NULL,
    [Title] nvarchar(200) NOT NULL,
    [SortOrder] int NOT NULL,
    [ContentDeltaJson] nvarchar(max) NOT NULL,
    [IsDeleted] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_NotesV3Pages] PRIMARY KEY ([PageId]),
    CONSTRAINT [CK_NotesV3Pages_DeltaJson] CHECK (ISJSON([ContentDeltaJson]) = 1),
    CONSTRAINT [CK_NotesV3Pages_SortOrder] CHECK ([SortOrder] >= 0),
    CONSTRAINT [FK_NotesV3Pages_NotesV3Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [NotesV3Sections] ([SectionId])
);
GO

CREATE TABLE [NotesV3CollabUpdates] (
    [UpdateId] bigint NOT NULL IDENTITY,
    [PageId] bigint NOT NULL,
    [OperationId] uniqueidentifier NOT NULL,
    [ActorUserId] bigint NOT NULL,
    [UpdateBytes] varbinary(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_NotesV3CollabUpdates] PRIMARY KEY ([UpdateId]),
    CONSTRAINT [CK_NotesV3CollabUpdates_Bytes] CHECK (DATALENGTH([UpdateBytes]) BETWEEN 1 AND 262144),
    CONSTRAINT [CK_NotesV3CollabUpdates_Operation] CHECK ([OperationId] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [FK_NotesV3CollabUpdates_NotesV3Pages_PageId] FOREIGN KEY ([PageId]) REFERENCES [NotesV3Pages] ([PageId]),
    CONSTRAINT [FK_NotesV3CollabUpdates_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([UserId])
);
GO

CREATE TABLE [NotesV3PdfPageLinks] (
    [PageId] bigint NOT NULL,
    [DocumentVersionId] bigint NOT NULL,
    [OwnerUserId] bigint NULL,
    [GroupDocumentShareId] bigint NULL,
    [PhysicalPageNumber] int NOT NULL,
    CONSTRAINT [PK_NotesV3PdfPageLinks] PRIMARY KEY ([PageId]),
    CONSTRAINT [CK_NotesV3PdfPageLinks_PhysicalPage] CHECK ([PhysicalPageNumber] >= 1),
    CONSTRAINT [CK_NotesV3PdfPageLinks_Scope] CHECK (([OwnerUserId] IS NOT NULL AND [GroupDocumentShareId] IS NULL)
OR
([OwnerUserId] IS NULL AND [GroupDocumentShareId] IS NOT NULL)),
    CONSTRAINT [FK_NotesV3PdfPageLinks_NotesV3DocumentShares_GroupDocumentShareId] FOREIGN KEY ([GroupDocumentShareId]) REFERENCES [NotesV3DocumentShares] ([GroupDocumentShareId]),
    CONSTRAINT [FK_NotesV3PdfPageLinks_NotesV3DocumentVersions_DocumentVersionId] FOREIGN KEY ([DocumentVersionId]) REFERENCES [NotesV3DocumentVersions] ([DocumentVersionId]),
    CONSTRAINT [FK_NotesV3PdfPageLinks_NotesV3Pages_PageId] FOREIGN KEY ([PageId]) REFERENCES [NotesV3Pages] ([PageId]),
    CONSTRAINT [FK_NotesV3PdfPageLinks_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([UserId])
);
GO

CREATE TABLE [NotesV3References] (
    [ReferenceId] bigint NOT NULL IDENTITY,
    [PageId] bigint NOT NULL,
    [DocumentVersionId] bigint NOT NULL,
    [GroupDocumentShareId] bigint NULL,
    [PhysicalPageNumber] int NOT NULL,
    [AnchorJson] nvarchar(max) NULL,
    [CapturedText] nvarchar(4000) NOT NULL,
    CONSTRAINT [PK_NotesV3References] PRIMARY KEY ([ReferenceId]),
    CONSTRAINT [CK_NotesV3References_AnchorJson] CHECK ([AnchorJson] IS NULL OR ISJSON([AnchorJson]) = 1),
    CONSTRAINT [CK_NotesV3References_PhysicalPage] CHECK ([PhysicalPageNumber] >= 1),
    CONSTRAINT [FK_NotesV3References_NotesV3DocumentShares_GroupDocumentShareId] FOREIGN KEY ([GroupDocumentShareId]) REFERENCES [NotesV3DocumentShares] ([GroupDocumentShareId]),
    CONSTRAINT [FK_NotesV3References_NotesV3DocumentVersions_DocumentVersionId] FOREIGN KEY ([DocumentVersionId]) REFERENCES [NotesV3DocumentVersions] ([DocumentVersionId]),
    CONSTRAINT [FK_NotesV3References_NotesV3Pages_PageId] FOREIGN KEY ([PageId]) REFERENCES [NotesV3Pages] ([PageId])
);
GO

CREATE INDEX [IX_NotesV3Annotations_DocumentVersionId] ON [NotesV3Annotations] ([DocumentVersionId]);
GO

CREATE INDEX [IX_NotesV3Annotations_GroupDocumentShareId_DocumentVersionId_PhysicalPageNumber_IsDeleted] ON [NotesV3Annotations] ([GroupDocumentShareId], [DocumentVersionId], [PhysicalPageNumber], [IsDeleted]);
GO

CREATE INDEX [IX_NotesV3Annotations_OwnerUserId_DocumentVersionId_PhysicalPageNumber_IsDeleted] ON [NotesV3Annotations] ([OwnerUserId], [DocumentVersionId], [PhysicalPageNumber], [IsDeleted]);
GO

CREATE INDEX [IX_NotesV3CollabUpdates_ActorUserId] ON [NotesV3CollabUpdates] ([ActorUserId]);
GO

CREATE UNIQUE INDEX [IX_NotesV3CollabUpdates_PageId_OperationId] ON [NotesV3CollabUpdates] ([PageId], [OperationId]);
GO

CREATE INDEX [IX_NotesV3CollabUpdates_PageId_UpdateId] ON [NotesV3CollabUpdates] ([PageId], [UpdateId]);
GO

CREATE INDEX [IX_NotesV3Documents_OwnerUserId_IsDeleted_IsArchived_UpdatedAtUtc] ON [NotesV3Documents] ([OwnerUserId], [IsDeleted], [IsArchived], [UpdatedAtUtc]);
GO

CREATE UNIQUE INDEX [IX_NotesV3DocumentShares_DocumentId_StudyGroupId] ON [NotesV3DocumentShares] ([DocumentId], [StudyGroupId]) WHERE [IsActive] = 1;
GO

CREATE INDEX [IX_NotesV3DocumentShares_SharedByUserId] ON [NotesV3DocumentShares] ([SharedByUserId]);
GO

CREATE INDEX [IX_NotesV3DocumentShares_StudyGroupId_IsActive] ON [NotesV3DocumentShares] ([StudyGroupId], [IsActive]);
GO

CREATE INDEX [IX_NotesV3DocumentVersions_DocumentId_DocumentVersionId] ON [NotesV3DocumentVersions] ([DocumentId], [DocumentVersionId]);
GO

CREATE UNIQUE INDEX [IX_NotesV3DocumentVersions_StorageKey] ON [NotesV3DocumentVersions] ([StorageKey]);
GO

CREATE INDEX [IX_NotesV3Notebooks_OwnerUserId_IsDeleted_SortOrder] ON [NotesV3Notebooks] ([OwnerUserId], [IsDeleted], [SortOrder]);
GO

CREATE INDEX [IX_NotesV3Notebooks_StudyGroupId_IsDeleted_SortOrder] ON [NotesV3Notebooks] ([StudyGroupId], [IsDeleted], [SortOrder]);
GO

CREATE INDEX [IX_NotesV3Pages_SectionId_IsDeleted_SortOrder] ON [NotesV3Pages] ([SectionId], [IsDeleted], [SortOrder]);
GO

CREATE INDEX [IX_NotesV3PdfPageLinks_DocumentVersionId] ON [NotesV3PdfPageLinks] ([DocumentVersionId]);
GO

CREATE UNIQUE INDEX [IX_NotesV3PdfPageLinks_GroupDocumentShareId_DocumentVersionId_PhysicalPageNumber] ON [NotesV3PdfPageLinks] ([GroupDocumentShareId], [DocumentVersionId], [PhysicalPageNumber]) WHERE [GroupDocumentShareId] IS NOT NULL;
GO

CREATE UNIQUE INDEX [IX_NotesV3PdfPageLinks_OwnerUserId_DocumentVersionId_PhysicalPageNumber] ON [NotesV3PdfPageLinks] ([OwnerUserId], [DocumentVersionId], [PhysicalPageNumber]) WHERE [OwnerUserId] IS NOT NULL;
GO

CREATE INDEX [IX_NotesV3References_DocumentVersionId] ON [NotesV3References] ([DocumentVersionId]);
GO

CREATE INDEX [IX_NotesV3References_GroupDocumentShareId] ON [NotesV3References] ([GroupDocumentShareId]);
GO

CREATE INDEX [IX_NotesV3References_PageId] ON [NotesV3References] ([PageId]);
GO

CREATE INDEX [IX_NotesV3Sections_NotebookId_IsDeleted_SortOrder] ON [NotesV3Sections] ([NotebookId], [IsDeleted], [SortOrder]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261028195525_NotesV3IndependentDal', N'8.0.31');
GO

COMMIT;
GO

