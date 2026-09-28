/* OPTIONAL local integration tests for a DEVELOPMENT database only.
   Requires 01_Schema.sql, NOT demo data. All test rows are rolled back.
   Identity counters can still advance after rollback: gaps are normal.
   Run this file by itself, outside any existing transaction.
*/
USE [StudyHub];
GO
SET NOCOUNT ON;
SET XACT_ABORT OFF; -- Expected constraint violations should not doom the test transaction.
IF DB_NAME() <> N'StudyHub' THROW 51300, N'Wrong database.', 1;
IF @@TRANCOUNT <> 0 THROW 51301, N'Run tests outside an existing transaction.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @U1 bigint, @U2 bigint, @Term1 bigint, @Term2 bigint, @Subject int,
            @US1 bigint, @Group bigint, @Deck bigint, @Card bigint;
    DECLARE @Now datetime2(3) = SYSUTCDATETIME();
    DECLARE @Today date = CONVERT(date, @Now AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time');

    INSERT dbo.Users(DisplayName) VALUES (N'Smoke test user 1');
    SET @U1 = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.Users(DisplayName) VALUES (N'Smoke test user 2');
    SET @U2 = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.UserSettings(UserId) VALUES (@U1), (@U2);
    INSERT dbo.Subjects(SubjectCode, SubjectName)
    VALUES ('TST_' + LEFT(CONVERT(varchar(36), NEWID()), 26), N'Test subject');
    SET @Subject = CONVERT(int, SCOPE_IDENTITY());
    INSERT dbo.AcademicTerms(UserId, TermName, StartDate, EndDate)
    VALUES (@U1, N'Test term', @Today, DATEADD(day, 30, @Today));
    SET @Term1 = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.AcademicTerms(UserId, TermName, StartDate, EndDate)
    VALUES (@U2, N'Test term', @Today, DATEADD(day, 30, @Today));
    SET @Term2 = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.UserSubjects(UserId, AcademicTermId, SubjectId) VALUES (@U1, @Term1, @Subject);
    SET @US1 = CONVERT(bigint, SCOPE_IDENTITY());

    -- A: Cannot put a subject into someone else's term (composite FK).
    BEGIN TRY
        INSERT dbo.UserSubjects(UserId, AcademicTermId, SubjectId) VALUES (@U1, @Term2, @Subject);
        THROW 51390, N'FAIL A: cross-owner academic term was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;
    PRINT N'PASS A: term ownership constraint';

    -- B: Cannot attach a private note to another user's enrollment.
    BEGIN TRY
        INSERT dbo.Notes(OwnerUserId, UserSubjectId, Title) VALUES (@U2, @US1, N'Invalid ownership');
        THROW 51390, N'FAIL B: cross-owner subject was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;
    PRINT N'PASS B: note ownership constraint';

    -- C: Group visibility must have a group reference.
    BEGIN TRY
        INSERT dbo.Notes(OwnerUserId, Title, Visibility) VALUES (@U1, N'Invalid group sharing', 'Group');
        THROW 51390, N'FAIL C: missing sharing target was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;
    PRINT N'PASS C: sharing constraint';

    -- D: Score must be within its grading scale.
    BEGIN TRY
        INSERT dbo.GradeEntries(UserSubjectId, Title, Score, MaxScore, AssessedOn)
        VALUES (@US1, N'Invalid score', 11, 10, @Today);
        THROW 51390, N'FAIL D: out-of-range score was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;
    PRINT N'PASS D: score constraint';

    -- E: Empty grades yield NULL; mixed score scales normalize to /10.
    IF EXISTS (SELECT 1 FROM dbo.vw_SubjectGradeSummary WHERE UserSubjectId = @US1 AND CurrentAverage10 IS NOT NULL)
        THROW 51390, N'FAIL E: no grades should mean NULL, not zero.', 1;
    INSERT dbo.GradeEntries(UserSubjectId, Title, Score, MaxScore, Weight, AssessedOn) VALUES
        (@US1, N'Scale 100', 80, 100, 1, @Today),
        (@US1, N'Scale 10', 9, 10, 1, @Today);
    IF NOT EXISTS (SELECT 1 FROM dbo.vw_SubjectGradeSummary WHERE UserSubjectId = @US1 AND CurrentAverage10 = 8.50)
        THROW 51390, N'FAIL E: normalized average must be 8.50.', 1;
    PRINT N'PASS E: grade summary';

    -- F: Progress is per learner, not a single shared schedule on the card.
    INSERT dbo.FlashcardDecks(OwnerUserId, Title) VALUES (@U1, N'Test deck');
    SET @Deck = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.Flashcards(DeckId, FrontText, BackText) VALUES (@Deck, N'Q', N'A');
    SET @Card = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.FlashcardProgress(UserId, FlashcardId) VALUES (@U1, @Card);
    BEGIN TRY
        INSERT dbo.FlashcardProgress(UserId, FlashcardId) VALUES (@U1, @Card);
        THROW 51390, N'FAIL F: duplicate learner/card progress was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
    END CATCH;
    PRINT N'PASS F: unique learner/card progress';

    -- G: Multiple actions on one day count once. Login does not extend streak.
    INSERT dbo.ActivityEvents(RequestId, UserId, EventType, OccurredAtUtc, ActivityDate, TimeZoneId)
    SELECT NEWID(), @U1, 'NoteSaved', DATEADD(day, -v.OffsetDays, @Now),
           DATEADD(day, -v.OffsetDays, @Today), N'SE Asia Standard Time'
    FROM (VALUES (0),(1),(2),(3),(4),(0)) AS v(OffsetDays);
    INSERT dbo.ActivityEvents(RequestId, UserId, EventType, OccurredAtUtc, ActivityDate, TimeZoneId)
    VALUES (NEWID(), @U2, 'Login', @Now, @Today, N'SE Asia Standard Time');
    IF NOT EXISTS (SELECT 1 FROM dbo.vw_StudyStreaks WHERE UserId = @U1 AND CurrentStreakDays = 5 AND LongestStreakDays = 5)
        THROW 51390, N'FAIL G: study streak must be 5.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.vw_StudyStreaks WHERE UserId = @U2 AND CurrentStreakDays = 0)
        THROW 51390, N'FAIL G: login-only streak must be 0.', 1;
    PRINT N'PASS G: distinct study days and login exclusion';

    -- H: Stored procedure creates the owner membership atomically.
    EXEC dbo.usp_CreateStudyGroup @OwnerUserId = @U1, @GroupName = N'Smoke test group', @StudyGroupId = @Group OUTPUT;
    IF NOT EXISTS (SELECT 1 FROM dbo.GroupMembers WHERE StudyGroupId = @Group AND UserId = @U1 AND Status = 'Active')
        THROW 51390, N'FAIL H: owner membership missing.', 1;
    PRINT N'PASS H: group creation';

    -- I: Same event retried twice creates just one record.
    DECLARE @Request uniqueidentifier = NEWID();
    EXEC dbo.usp_RecordActivity @UserId = @U1, @EventType = 'FocusCompleted', @RequestId = @Request;
    EXEC dbo.usp_RecordActivity @UserId = @U1, @EventType = 'FocusCompleted', @RequestId = @Request;
    IF (SELECT COUNT(*) FROM dbo.ActivityEvents WHERE RequestId = @Request) <> 1
        THROW 51390, N'FAIL I: activity retry was not idempotent.', 1;
    PRINT N'PASS I: activity retry';

    ROLLBACK TRANSACTION;
    SET XACT_ABORT ON;
    PRINT N'ALL 9 TESTS PASSED. Test rows were rolled back; identity gaps may remain.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    SET XACT_ABORT ON;
    THROW;
END CATCH;
GO
