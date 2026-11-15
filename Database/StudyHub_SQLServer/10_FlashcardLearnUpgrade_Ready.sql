/*
 StudyHub - Flashcard Learn DB-first upgrade
 Run AFTER 01_Schema.sql and the project's existing database migrations.
 Run on a BACKED-UP TEST database first. Safe to rerun on a compatible schema.
 This script does not delete existing data or modify scheduler/progress columns.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
       OR OBJECT_ID(N'dbo.FlashcardDecks', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Flashcards', N'U') IS NULL
       OR OBJECT_ID(N'dbo.FlashcardReviews', N'U') IS NULL
        THROW 51000, 'Missing base StudyHub tables. Apply the base schema first.', 1;

    -- 1. Session table (only absent from older DB-first schemas).
    IF OBJECT_ID(N'dbo.FlashcardLearnSessions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FlashcardLearnSessions
        (
            SessionId uniqueidentifier NOT NULL
                CONSTRAINT PK_FlashcardLearnSessions PRIMARY KEY,
            UserId bigint NOT NULL,
            DeckId bigint NOT NULL,
            StartedAtUtc datetime2(3) NOT NULL
                CONSTRAINT DF_FlashcardLearnSessions_Started DEFAULT (SYSUTCDATETIME()),
            CompletedAtUtc datetime2(3) NULL,
            CompletedTotalCards int NULL,
            CurrentCardId bigint NULL,
            QuestionToken uniqueidentifier NULL,
            QuestionOptionsJson nvarchar(max) NULL,
            RowVersion rowversion NOT NULL,
            InvalidatedAtUtc datetime2(3) NULL,
            ShuffleEnabled bit NOT NULL
                CONSTRAINT DF_FlashcardLearnSessions_Shuffle DEFAULT (0)
        );
    END;

    -- 2. Fill safely addable columns on partially upgraded databases.
    IF COL_LENGTH('dbo.FlashcardLearnSessions', 'InvalidatedAtUtc') IS NULL
        ALTER TABLE dbo.FlashcardLearnSessions ADD InvalidatedAtUtc datetime2(3) NULL;

    IF COL_LENGTH('dbo.FlashcardLearnSessions', 'ShuffleEnabled') IS NULL
        ALTER TABLE dbo.FlashcardLearnSessions ADD ShuffleEnabled bit NOT NULL
            CONSTRAINT DF_FlashcardLearnSessions_Shuffle DEFAULT (0) WITH VALUES;

    IF COL_LENGTH('dbo.FlashcardLearnSessions', 'CompletedTotalCards') IS NULL
        ALTER TABLE dbo.FlashcardLearnSessions ADD CompletedTotalCards int NULL;

    -- Fail safely rather than inventing required values on a nonempty, incompatible table.
    IF EXISTS
    (
        SELECT 1 FROM (VALUES
            ('SessionId'), ('UserId'), ('DeckId'), ('StartedAtUtc'),
            ('CompletedAtUtc'), ('CurrentCardId'), ('QuestionToken'),
            ('QuestionOptionsJson'), ('RowVersion')
        ) AS required(ColumnName)
        WHERE COL_LENGTH('dbo.FlashcardLearnSessions', required.ColumnName) IS NULL
    )
        THROW 51001, 'Learn Session table has missing core columns; inspect schema before upgrading.', 1;

    IF COL_LENGTH('dbo.FlashcardReviews', 'SessionId') IS NULL
        ALTER TABLE dbo.FlashcardReviews ADD SessionId uniqueidentifier NULL;

    -- 3. Foreign keys (NO ACTION, matching StudyHubDbContext).
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_LearnSession_User'
                   AND parent_object_id = OBJECT_ID(N'dbo.FlashcardLearnSessions'))
        ALTER TABLE dbo.FlashcardLearnSessions WITH CHECK ADD CONSTRAINT FK_LearnSession_User
            FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE NO ACTION;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_LearnSession_Deck'
                   AND parent_object_id = OBJECT_ID(N'dbo.FlashcardLearnSessions'))
        ALTER TABLE dbo.FlashcardLearnSessions WITH CHECK ADD CONSTRAINT FK_LearnSession_Deck
            FOREIGN KEY (DeckId) REFERENCES dbo.FlashcardDecks(DeckId) ON DELETE NO ACTION;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_LearnSession_CurrentCard'
                   AND parent_object_id = OBJECT_ID(N'dbo.FlashcardLearnSessions'))
        ALTER TABLE dbo.FlashcardLearnSessions WITH CHECK ADD CONSTRAINT FK_LearnSession_CurrentCard
            FOREIGN KEY (CurrentCardId) REFERENCES dbo.Flashcards(FlashcardId) ON DELETE NO ACTION;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Reviews_LearnSession'
                   AND parent_object_id = OBJECT_ID(N'dbo.FlashcardReviews'))
        ALTER TABLE dbo.FlashcardReviews WITH CHECK ADD CONSTRAINT FK_Reviews_LearnSession
            FOREIGN KEY (SessionId) REFERENCES dbo.FlashcardLearnSessions(SessionId) ON DELETE NO ACTION;

    -- 4. Indexes used by Resume, session history and replay protection.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FlashcardLearnSessions')
                   AND name = N'IX_LearnSession_Resume')
        EXEC(N'CREATE INDEX IX_LearnSession_Resume
               ON dbo.FlashcardLearnSessions(UserId, DeckId, CompletedAtUtc)');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FlashcardReviews')
                   AND name = N'IX_Reviews_LearnSession')
        EXEC(N'CREATE INDEX IX_Reviews_LearnSession
               ON dbo.FlashcardReviews(SessionId, FlashcardId)');

    -- Present as a UNIQUE constraint in 01_Schema.sql; never recreate if already present.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FlashcardReviews')
                   AND name = N'UQ_FlashcardReviews_Request')
        EXEC(N'CREATE UNIQUE INDEX UQ_FlashcardReviews_Request
               ON dbo.FlashcardReviews(RequestId)');

    -- 5. Only one active Learn Session per user and deck.
    -- Fail with a clear message if legacy data already contains duplicates.
    IF EXISTS (
        SELECT 1
        FROM dbo.FlashcardLearnSessions
        WHERE CompletedAtUtc IS NULL AND InvalidatedAtUtc IS NULL
        GROUP BY UserId, DeckId
        HAVING COUNT(*) > 1
    )
        THROW 51002, 'Duplicate active Learn sessions found. Resolve them before creating UX_LearnSession_Active.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.FlashcardLearnSessions')
          AND name = N'UX_LearnSession_Active'
    )
        CREATE UNIQUE INDEX UX_LearnSession_Active
            ON dbo.FlashcardLearnSessions(UserId, DeckId)
            WHERE CompletedAtUtc IS NULL AND InvalidatedAtUtc IS NULL;


    COMMIT TRANSACTION;
    PRINT CONCAT('Flashcard Learn schema ready in database: ', DB_NAME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
