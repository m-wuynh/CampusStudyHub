/* OPTIONAL DEMO ONLY. Run once after 01_Schema.sql.
   Fictional profiles, not real Google accounts. No ExternalLogins are seeded.
   Refuses to seed when users or subjects already exist. Never clears data.
*/
USE [StudyHub];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    IF DB_NAME() <> N'StudyHub' THROW 51100, N'Wrong database.', 1;
    IF OBJECT_ID(N'dbo.ActivityEvents', N'U') IS NULL THROW 51101, N'Run 01_Schema.sql first.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Users) OR EXISTS (SELECT 1 FROM dbo.Subjects)
        THROW 51102, N'Demo requires empty Users and Subjects. No data has been removed.', 1;

    BEGIN TRANSACTION;
    DECLARE @Now datetime2(3) = SYSUTCDATETIME();
    DECLARE @Today date = CONVERT(date, @Now AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time');
    DECLARE @Student1 bigint, @Student2 bigint, @Admin bigint,
            @SqlSubject int, @FlutterSubject int, @Term1 bigint, @Term2 bigint,
            @UserSql1 bigint, @UserFlutter1 bigint, @UserSql2 bigint,
            @Group bigint, @Note bigint, @Document bigint, @Deck bigint,
            @Card bigint, @Event bigint, @Deadline bigint;

    INSERT dbo.Users(DisplayName, Email, SchoolName, ClassName, StudentCode)
    VALUES (N'Nguyễn Minh Anh (demo)', N'minhanh@example.invalid', N'Trường đại học demo', N'SE01', N'DEMO001');
    SET @Student1 = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.Users(DisplayName, Email, SchoolName, ClassName, StudentCode)
    VALUES (N'Trần Bảo An (demo)', N'baoan@example.invalid', N'Trường đại học demo', N'SE01', N'DEMO002');
    SET @Student2 = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.Users(DisplayName, Email, RoleCode)
    VALUES (N'Quản trị viên demo', N'admin@example.invalid', 'Admin');
    SET @Admin = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.UserSettings(UserId) VALUES (@Student1), (@Student2), (@Admin);
    INSERT dbo.Subjects(SubjectCode, SubjectName, Description)
    VALUES ('DB101', N'Cơ sở dữ liệu', N'Thiết kế dữ liệu và truy vấn SQL Server.');
    SET @SqlSubject = CONVERT(int, SCOPE_IDENTITY());
    INSERT dbo.Subjects(SubjectCode, SubjectName)
    VALUES ('MOB101', N'Lập trình ứng dụng di động');
    SET @FlutterSubject = CONVERT(int, SCOPE_IDENTITY());

    INSERT dbo.AcademicTerms(UserId, TermName, StartDate, EndDate)
    VALUES (@Student1, N'Học kỳ demo', DATEADD(day, -30, @Today), DATEADD(day, 90, @Today));
    SET @Term1 = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.AcademicTerms(UserId, TermName, StartDate, EndDate)
    VALUES (@Student2, N'Học kỳ demo', DATEADD(day, -30, @Today), DATEADD(day, 90, @Today));
    SET @Term2 = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.UserSubjects(UserId, AcademicTermId, SubjectId, CreditWeight, TargetScore10)
    VALUES (@Student1, @Term1, @SqlSubject, 3, 8.5);
    SET @UserSql1 = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.UserSubjects(UserId, AcademicTermId, SubjectId, CreditWeight, TargetScore10)
    VALUES (@Student1, @Term1, @FlutterSubject, 3, 8.0);
    SET @UserFlutter1 = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.UserSubjects(UserId, AcademicTermId, SubjectId, CreditWeight)
    VALUES (@Student2, @Term2, @SqlSubject, 3);
    SET @UserSql2 = CONVERT(bigint, SCOPE_IDENTITY());

    EXEC dbo.usp_CreateStudyGroup @OwnerUserId = @Student1, @GroupName = N'Nhóm ôn SQL Server',
        @SubjectId = @SqlSubject, @MaxMembers = 5, @StudyGroupId = @Group OUTPUT;
    INSERT dbo.GroupMembers(StudyGroupId, UserId, Status, JoinedAtUtc)
    VALUES (@Group, @Student2, 'Active', @Now);
    INSERT dbo.GroupPosts(StudyGroupId, AuthorUserId, Body, IsPinned)
    VALUES (@Group, @Student1, N'Tuần này cùng luyện JOIN, GROUP BY và thiết kế khóa ngoại.', 1);

    INSERT dbo.Notes(OwnerUserId, UserSubjectId, StudyGroupId, Title, Body, Visibility)
    VALUES (@Student1, @UserSql1, @Group, N'Ôn tập khóa chính và khóa ngoại',
            N'# Ghi nhớ' + NCHAR(10) + N'- Primary key: định danh duy nhất.' + NCHAR(10) + N'- Foreign key: đảm bảo quan hệ dữ liệu.', 'Group');
    SET @Note = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.Notes(OwnerUserId, UserSubjectId, Title, Body)
    VALUES (@Student1, @UserFlutter1, N'Ghi chú Flutter riêng tư', N'Widget, state và navigation.');

    INSERT dbo.Documents(OwnerUserId, UserSubjectId, StudyGroupId, Title, ResourceType, ExternalUrl, Visibility)
    VALUES (@Student1, @UserSql1, @Group, N'Tài liệu CREATE TABLE chính thức', 'Link',
            N'https://learn.microsoft.com/en-us/sql/t-sql/statements/create-table-transact-sql', 'Group');
    SET @Document = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.DocumentBookmarks(UserId, DocumentId) VALUES (@Student2, @Document);

    INSERT dbo.FlashcardDecks(OwnerUserId, UserSubjectId, StudyGroupId, Title, Visibility)
    VALUES (@Student1, @UserSql1, @Group, N'SQL cơ bản', 'Group');
    SET @Deck = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.Flashcards(DeckId, FrontText, BackText, SortOrder)
    VALUES (@Deck, N'PRIMARY KEY dùng để làm gì?', N'Định danh duy nhất mỗi bản ghi; không được NULL.', 1);
    SET @Card = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.Flashcards(DeckId, FrontText, BackText, SortOrder) VALUES
        (@Deck, N'FOREIGN KEY dùng để làm gì?', N'Đảm bảo giá trị tham chiếu tồn tại ở bảng được liên kết.', 2),
        (@Deck, N'GROUP BY dùng để làm gì?', N'Gom các dòng thành nhóm để tính tổng hợp.', 3);
    INSERT dbo.FlashcardProgress(UserId, FlashcardId, NextReviewAtUtc, LastReviewedAtUtc, RepetitionCount, IntervalDays)
    VALUES (@Student1, @Card, DATEADD(day, 1, @Now), @Now, 1, 1);
    INSERT dbo.FlashcardReviews(RequestId, UserId, FlashcardId, Rating, ResponseMilliseconds, ReviewedAtUtc)
    VALUES (NEWID(), @Student1, @Card, 3, 4500, @Now);

    DECLARE @ClassStart datetime2(0) = DATEADD(hour, 9, CONVERT(datetime2(0), DATEADD(day, 1, @Today)));
    INSERT dbo.ScheduleEvents(UserId, UserSubjectId, Title, StartAtLocal, EndAtLocal, RepeatMode, RepeatUntilDate, Location)
    VALUES (@Student1, @UserSql1, N'Học Cơ sở dữ liệu', @ClassStart, DATEADD(hour, 2, @ClassStart),
            'Weekly', DATEADD(day, 60, @Today), N'Phòng A302');
    SET @Event = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.ScheduleExceptions(ScheduleEventId, OccurrenceDate, IsCancelled)
    VALUES (@Event, DATEADD(day, 8, @Today), 1);

    INSERT dbo.Deadlines(UserId, UserSubjectId, Title, DueAtUtc, Priority)
    VALUES (@Student1, @UserSql1, N'Nộp ERD và script database Study Hub', DATEADD(day, 2, @Now), 3);
    SET @Deadline = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.Reminders(UserId, DeadlineId, RemindAtUtc)
    VALUES (@Student1, @Deadline, DATEADD(hour, -1, DATEADD(day, 2, @Now)));
    INSERT dbo.Reminders(UserId, ScheduleEventId, OccurrenceDate, RemindAtUtc)
    VALUES (@Student1, @Event, CAST(@ClassStart AS date),
        CONVERT(datetime2(3), DATEADD(minute, -15, @ClassStart) AT TIME ZONE 'SE Asia Standard Time' AT TIME ZONE 'UTC'));

    -- Current weighted average = (8*1 + 8.5*2 + 8.5*2)/5 = 8.40 / 10.
    INSERT dbo.GradeEntries(UserSubjectId, Title, AssessmentType, Score, MaxScore, Weight, AssessedOn) VALUES
        (@UserSql1, N'Quiz 1', 'Quiz', 8, 10, 1, @Today),
        (@UserSql1, N'Bài thực hành', 'Assignment', 85, 100, 2, @Today),
        (@UserSql1, N'Giữa kỳ', 'Midterm', 8.5, 10, 2, @Today);
    INSERT dbo.Goals(UserId, UserSubjectId, Title, UnitCode, TargetValue, CurrentValue, StartDate, EndDate)
    VALUES (@Student1, @UserSql1, N'Học SQL 150 phút trong tuần', 'Minutes', 150, 25, @Today, DATEADD(day, 6, @Today));
    INSERT dbo.StudySessions(UserId, UserSubjectId, StartedAtUtc, EndedAtUtc, FocusSeconds, Status)
    VALUES (@Student1, @UserSql1, DATEADD(minute, -25, @Now), @Now, 1500, 'Completed');

    -- Five consecutive user-local study dates, plus another event today.
    -- Direct historical inserts are DEMO ONLY. Production uses usp_RecordActivity.
    INSERT dbo.ActivityEvents(RequestId, UserId, EventType, OccurredAtUtc, ActivityDate, TimeZoneId)
    SELECT NEWID(), @Student1, 'NoteSaved', DATEADD(day, -v.OffsetDays, @Now),
           DATEADD(day, -v.OffsetDays, @Today), N'SE Asia Standard Time'
    FROM (VALUES (0),(1),(2),(3),(4)) AS v(OffsetDays);
    DECLARE @ActivityRequest uniqueidentifier = NEWID();
    EXEC dbo.usp_RecordActivity @UserId = @Student1, @EventType = 'FocusCompleted', @RequestId = @ActivityRequest;
    SET @ActivityRequest = NEWID();
    EXEC dbo.usp_RecordActivity @UserId = @Student2, @EventType = 'Login', @RequestId = @ActivityRequest;

    INSERT dbo.ContentReports(ReporterUserId, NoteId, Reason)
    VALUES (@Student2, @Note, N'Báo cáo mẫu để demo trang quản trị; không phải vi phạm thật.');
    INSERT dbo.AuditLogs(ActorUserId, ActionCode, EntityType, EntityKey, Summary)
    VALUES (@Admin, 'DemoSeeded', 'System', N'Demo', N'Khởi tạo dữ liệu giả phục vụ kiểm thử.');

    COMMIT TRANSACTION;
    SELECT @Student1 AS DemoStudent1Id, @Student2 AS DemoStudent2Id,
           @Admin AS DemoAdminId, @UserSql1 AS DemoUserSubjectId;
    SELECT * FROM dbo.vw_SubjectGradeSummary WHERE UserId = @Student1;
    SELECT * FROM dbo.vw_StudyStreaks WHERE UserId IN (@Student1, @Student2);
    PRINT N'Demo created. Student 1: average 8.40; streak 5 on the day the seed runs.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
