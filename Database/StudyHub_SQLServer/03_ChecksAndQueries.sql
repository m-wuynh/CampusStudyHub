/* Read-only integrity checks and parameterized dashboard query examples.
   Run after 01_Schema.sql; 02_SeedDemo.sql is optional.
   Never pass browser-supplied UserId directly: use the authenticated principal.
*/
USE [StudyHub];
GO
SET NOCOUNT ON;
IF DB_NAME() <> N'StudyHub' THROW 51200, N'Wrong database.', 1;
IF OBJECT_ID(N'dbo.ActivityEvents', N'U') IS NULL THROW 51201, N'Schema not installed.', 1;

-- Expected: 27 tables, 4 views, 2 procedures in a fresh install.
SELECT type_desc, COUNT(*) AS ObjectCount
FROM sys.objects
WHERE is_ms_shipped = 0 AND schema_id = SCHEMA_ID(N'dbo') AND type IN ('U','V','P')
GROUP BY type_desc;

-- Expected: no rows in the following three integrity results.
SELECT name AS DisabledOrUntrustedForeignKey
FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1;
SELECT name AS DisabledOrUntrustedCheck
FROM sys.check_constraints WHERE is_disabled = 1 OR is_not_trusted = 1;
DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;

-- Additional business-integrity checks; expected empty with demo data.
SELECT g.StudyGroupId AS GroupWithoutActiveOwner
FROM dbo.StudyGroups AS g
LEFT JOIN dbo.GroupMembers AS m ON m.StudyGroupId = g.StudyGroupId AND m.UserId = g.OwnerUserId
WHERE m.UserId IS NULL OR m.Status <> 'Active';
SELECT g.StudyGroupId, g.MaxMembers, COUNT(*) AS ActiveMemberCount
FROM dbo.StudyGroups AS g
JOIN dbo.GroupMembers AS m ON m.StudyGroupId = g.StudyGroupId AND m.Status = 'Active'
GROUP BY g.StudyGroupId, g.MaxMembers HAVING COUNT(*) > g.MaxMembers;
SELECT s.UserId AS InvalidUserTimeZone, s.TimeZoneId
FROM dbo.UserSettings AS s
WHERE NOT EXISTS (SELECT 1 FROM sys.time_zone_info AS z WHERE z.name = s.TimeZoneId);
SELECT e.ScheduleEventId AS InvalidEventTimeZone, e.TimeZoneId
FROM dbo.ScheduleEvents AS e
WHERE NOT EXISTS (SELECT 1 FROM sys.time_zone_info AS z WHERE z.name = e.TimeZoneId);

-- This lookup is for the fictional sample only, NOT an authentication mechanism.
DECLARE @UserId bigint = (SELECT MIN(UserId) FROM dbo.Users WHERE Email = N'minhanh@example.invalid');
DECLARE @Now datetime2(3) = SYSUTCDATETIME();
IF @UserId IS NULL
BEGIN
    PRINT N'No demo student found. Set @UserId to your authenticated test user.';
    RETURN;
END;

-- 1. Overview cards. Each aggregate is independent to avoid join multiplication.
SELECT
    (SELECT COUNT(*) FROM dbo.UserSubjects WHERE UserId = @UserId AND IsArchived = 0) AS ActiveSubjects,
    (SELECT COUNT(*) FROM dbo.Notes WHERE OwnerUserId = @UserId AND IsDeleted = 0) AS Notes,
    (SELECT COUNT(*) FROM dbo.Documents WHERE OwnerUserId = @UserId AND IsDeleted = 0) AS Documents,
    (SELECT COUNT(*) FROM dbo.Deadlines WHERE UserId = @UserId AND IsDeleted = 0 AND Status IN ('Pending','InProgress')) AS OpenDeadlines,
    (SELECT COUNT(*) FROM dbo.Deadlines WHERE UserId = @UserId AND IsDeleted = 0 AND Status IN ('Pending','InProgress') AND DueAtUtc < @Now) AS OverdueDeadlines;
SELECT * FROM dbo.vw_StudyStreaks WHERE UserId = @UserId;
SELECT * FROM dbo.vw_TermGradeSummary WHERE UserId = @UserId;

-- 2. Deadlines for the next seven days. Overdue items are a separate card above.
SELECT DeadlineId, Title, DueAtUtc, Priority, Status
FROM dbo.Deadlines
WHERE UserId = @UserId AND IsDeleted = 0 AND Status IN ('Pending','InProgress')
  AND DueAtUtc >= @Now AND DueAtUtc < DATEADD(day, 7, @Now)
ORDER BY DueAtUtc, Priority DESC;

-- 3. Documents currently accessible to a student, including bookmarked files.
-- A bookmark does not preserve access after a file becomes private.
SELECT d.DocumentId, d.Title, d.ResourceType, d.UserSubjectId, d.Visibility,
       CONVERT(bit, CASE WHEN b.DocumentId IS NULL THEN 0 ELSE 1 END) AS IsBookmarked
FROM dbo.Documents AS d
LEFT JOIN dbo.DocumentBookmarks AS b ON b.DocumentId = d.DocumentId AND b.UserId = @UserId
WHERE d.IsDeleted = 0
  AND (d.OwnerUserId = @UserId OR d.Visibility = 'Public'
       OR (d.Visibility = 'Group' AND EXISTS (
           SELECT 1 FROM dbo.GroupMembers AS gm
           JOIN dbo.StudyGroups AS sg ON sg.StudyGroupId = gm.StudyGroupId
           WHERE gm.StudyGroupId = d.StudyGroupId AND gm.UserId = @UserId
             AND gm.Status = 'Active' AND sg.IsArchived = 0)))
ORDER BY d.CreatedAtUtc DESC;

-- 4. Flashcards ready for this learner: new cards OR existing due cards.
-- Example limited to decks the learner owns or can currently access.
SELECT TOP (50) c.FlashcardId, c.DeckId, c.FrontText, c.BackText,
       p.NextReviewAtUtc, p.RepetitionCount
FROM dbo.Flashcards AS c
JOIN dbo.FlashcardDecks AS d ON d.DeckId = c.DeckId
LEFT JOIN dbo.FlashcardProgress AS p ON p.FlashcardId = c.FlashcardId AND p.UserId = @UserId
WHERE c.IsDeleted = 0 AND d.IsDeleted = 0
  AND (p.FlashcardId IS NULL OR p.NextReviewAtUtc <= @Now)
  AND (d.OwnerUserId = @UserId OR d.Visibility = 'Public'
       OR (d.Visibility = 'Group' AND EXISTS (
           SELECT 1 FROM dbo.GroupMembers AS gm
           JOIN dbo.StudyGroups AS sg ON sg.StudyGroupId = gm.StudyGroupId
           WHERE gm.StudyGroupId = d.StudyGroupId AND gm.UserId = @UserId
             AND gm.Status = 'Active' AND sg.IsArchived = 0)))
ORDER BY p.NextReviewAtUtc, c.DeckId, c.SortOrder, c.FlashcardId;

-- 5. Calendar definitions, not expanded weekly occurrences.
-- Backend expands weekly dates, applies exceptions, then converts each to UTC.
SELECT * FROM dbo.ScheduleEvents WHERE UserId = @UserId AND IsDeleted = 0 ORDER BY StartAtLocal;
SELECT x.* FROM dbo.ScheduleExceptions AS x
JOIN dbo.ScheduleEvents AS e ON e.ScheduleEventId = x.ScheduleEventId
WHERE e.UserId = @UserId AND e.IsDeleted = 0;

-- 6. Grades and goals. Average is provisional, not a certified final GPA.
SELECT * FROM dbo.vw_SubjectGradeSummary WHERE UserId = @UserId;
SELECT GoalId, Title, UnitCode, CurrentValue, TargetValue, EndDate,
       CAST(CASE WHEN CurrentValue >= TargetValue THEN 100.0
            ELSE 100.0 * CurrentValue / TargetValue END AS decimal(5,2)) AS ProgressPercent,
       CASE WHEN IsCancelled = 1 THEN 'Cancelled'
            WHEN CurrentValue >= TargetValue THEN 'Reached' ELSE 'Active' END AS GoalStatus
FROM dbo.Goals WHERE UserId = @UserId ORDER BY EndDate;

-- 7. ADMIN-ONLY query examples: enforce Admin policy before exposing them.
SELECT * FROM dbo.vw_DailyUsageUtc
WHERE ActivityDateUtc >= DATEADD(day, -29, CAST(@Now AS date)) ORDER BY ActivityDateUtc;
SELECT COUNT(DISTINCT UserId) AS ActiveUsersLast30Days
FROM dbo.ActivityEvents WHERE OccurredAtUtc >= DATEADD(day, -30, @Now) AND OccurredAtUtc <= @Now;
SELECT ReportId, Reason, CreatedAtUtc FROM dbo.ContentReports WHERE Status = 'Open' ORDER BY CreatedAtUtc;
GO
