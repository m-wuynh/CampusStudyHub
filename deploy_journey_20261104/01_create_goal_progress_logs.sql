/* =====================================================================
   01 – Migration 20261104090000_AddGoalProgressLogs (áp dụng bằng SQL, KHÔNG dùng dotnet ef database update)
   ---------------------------------------------------------------------
   1. Tạo bảng dbo.GoalProgressLogs (hành trình mục tiêu) + 3 index + 3 khóa ngoại (NO ACTION).
   2. Ghi 1 dòng vào __EFMigrationsHistory (chỉ dòng của migration này).
   Không sửa / xóa dữ liệu nào; các bảng Grades & Goals được dấu kiểm bảo vệ.
   Chạy (bắt buộc truyền Apply):
     Chạy thử (ROLLBACK):  sqlcmd -S "DESKTOP-MUJA2DK\SQLEXPRESS" -d StudyHub -E -I -b -f 65001 -W -s "|" -v Apply=0 -i 01_create_goal_progress_logs.sql -o 01_Apply0.txt
     Áp dụng thật (COMMIT): sqlcmd -S "DESKTOP-MUJA2DK\SQLEXPRESS" -d StudyHub -E -I -b -f 65001 -W -s "|" -v Apply=1 -i 01_create_goal_progress_logs.sql -o 01_Apply1.txt
   Tắt StudyHub.Web trước khi chạy.
   ===================================================================== */
:on error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF DB_NAME() <> N'StudyHub' THROW 50000, N'Sai CSDL: phải chạy trên StudyHub.', 1;
IF '$(Apply)' NOT IN ('0', '1') THROW 50000, N'Apply phải là 0 (chạy thử) hoặc 1 (áp dụng).', 1;
PRINT N'=== CHẾ ĐỘ: ' + CASE WHEN '$(Apply)' = '1' THEN N'ÁP DỤNG THẬT (COMMIT)' ELSE N'CHẠY THỬ (ROLLBACK)' END + N' – ' + CONVERT(nvarchar(30), SYSDATETIME(), 121);
GO
PRINT N'--- Kiểm tra điều kiện ---';
IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261104090000_AddGoalProgressLogs')
    THROW 50001, N'Migration AddGoalProgressLogs đã có trong __EFMigrationsHistory – dừng.', 1;
IF OBJECT_ID('dbo.GoalProgressLogs') IS NOT NULL
    THROW 50002, N'Bảng dbo.GoalProgressLogs đã tồn tại – dừng.', 1;
IF OBJECT_ID('dbo.Goals') IS NULL OR OBJECT_ID('dbo.UserSubjects') IS NULL OR OBJECT_ID('dbo.Users') IS NULL
    THROW 50003, N'Thiếu bảng Goals / UserSubjects / Users – dừng.', 1;
PRINT N'OK: chưa có bảng, chưa có dòng lịch sử.';

BEGIN TRANSACTION;
IF OBJECT_ID('tempdb..#GuardBefore') IS NOT NULL DROP TABLE #GuardBefore;
SELECT g.TableName, g.RowCnt, g.Chk INTO #GuardBefore
FROM (
SELECT N'GradeBooks', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.GradeBooks
UNION ALL SELECT N'GradeColumns', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.GradeColumns
UNION ALL SELECT N'GradeEntries', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.GradeEntries
UNION ALL SELECT N'UserSubjects', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.UserSubjects
UNION ALL SELECT N'Goals', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.Goals
UNION ALL SELECT N'GoalHistories', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.GoalHistories
UNION ALL SELECT N'ScheduleEvents', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.ScheduleEvents
UNION ALL SELECT N'Reminders', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.Reminders
UNION ALL SELECT N'Deadlines', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.Deadlines
UNION ALL SELECT N'StudySessions', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.StudySessions
) AS g (TableName, RowCnt, Chk);
GO
CREATE TABLE dbo.GoalProgressLogs (
    GoalProgressLogId bigint IDENTITY(1,1) NOT NULL,
    UserId            bigint NOT NULL,
    UserSubjectId     bigint NOT NULL,
    GoalId            bigint NULL,
    EventType         varchar(30) NOT NULL,
    AverageScore      decimal(5,2) NULL,
    PreviousValue     decimal(5,2) NULL,
    TargetValue       decimal(5,2) NULL,
    ProgressPercent   int NULL,
    StatusFrom        nvarchar(20) NULL,
    StatusTo          nvarchar(20) NULL,
    OccurredAtUtc     datetime2(3) NOT NULL CONSTRAINT DF_GoalProgressLogs_OccurredAtUtc DEFAULT (sysutcdatetime()),
    CONSTRAINT PK_GoalProgressLogs PRIMARY KEY (GoalProgressLogId),
    CONSTRAINT FK_GoalProgressLogs_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_GoalProgressLogs_UserSubject FOREIGN KEY (UserSubjectId) REFERENCES dbo.UserSubjects (UserSubjectId),
    CONSTRAINT FK_GoalProgressLogs_Goal FOREIGN KEY (GoalId) REFERENCES dbo.Goals (GoalId)
);
CREATE INDEX IX_GoalProgressLogs_Subject ON dbo.GoalProgressLogs (UserSubjectId, OccurredAtUtc);
CREATE INDEX IX_GoalProgressLogs_Goal ON dbo.GoalProgressLogs (GoalId);
CREATE INDEX IX_GoalProgressLogs_UserId ON dbo.GoalProgressLogs (UserId);

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
VALUES (N'20261104090000_AddGoalProgressLogs', N'8.0.31');
GO
PRINT N'--- Cấu trúc bảng mới ---';
SELECT c.name AS ColumnName, t.name AS TypeName, c.max_length, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.GoalProgressLogs') ORDER BY c.column_id;
SELECT name AS ForeignKey, delete_referential_action_desc AS OnDelete
FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.GoalProgressLogs');

IF OBJECT_ID('tempdb..#GuardAfter') IS NOT NULL DROP TABLE #GuardAfter;
SELECT g.TableName, g.RowCnt, g.Chk INTO #GuardAfter
FROM (
SELECT N'GradeBooks', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.GradeBooks
UNION ALL SELECT N'GradeColumns', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.GradeColumns
UNION ALL SELECT N'GradeEntries', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.GradeEntries
UNION ALL SELECT N'UserSubjects', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.UserSubjects
UNION ALL SELECT N'Goals', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.Goals
UNION ALL SELECT N'GoalHistories', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.GoalHistories
UNION ALL SELECT N'ScheduleEvents', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.ScheduleEvents
UNION ALL SELECT N'Reminders', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.Reminders
UNION ALL SELECT N'Deadlines', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.Deadlines
UNION ALL SELECT N'StudySessions', COUNT_BIG(*), CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.StudySessions
) AS g (TableName, RowCnt, Chk);
IF EXISTS (SELECT TableName, RowCnt, Chk FROM #GuardBefore EXCEPT SELECT TableName, RowCnt, Chk FROM #GuardAfter)
   OR EXISTS (SELECT TableName, RowCnt, Chk FROM #GuardAfter EXCEPT SELECT TableName, RowCnt, Chk FROM #GuardBefore)
BEGIN
    SELECT N'TRƯỚC' AS Phase, * FROM #GuardBefore UNION ALL SELECT N'SAU', * FROM #GuardAfter ORDER BY 2, 1;
    THROW 50090, N'Phát hiện dữ liệu ngoài phạm vi bị thay đổi – đã hủy toàn bộ.', 1;
END
PRINT N'--- Dấu kiểm: các bảng Grades & Goals KHÔNG thay đổi ---';
SELECT TableName, RowCnt, Chk FROM #GuardAfter ORDER BY TableName;

IF '$(Apply)' = '1'
BEGIN
    COMMIT TRANSACTION;
    PRINT N'>>> ĐÃ COMMIT.';
END
ELSE
BEGIN
    ROLLBACK TRANSACTION;
    PRINT N'>>> CHẠY THỬ: đã ROLLBACK, dữ liệu không thay đổi.';
END
GO
