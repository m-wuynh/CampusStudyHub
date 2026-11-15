/* =====================================================================
   R01 – Gỡ bảng GoalProgressLogs (chỉ dùng khi cần quay lại trước bước 01).
   Xóa luôn mọi mốc hành trình đã ghi. Chạy:
     sqlcmd -S "DESKTOP-MUJA2DK\SQLEXPRESS" -d StudyHub -E -I -b -f 65001 -W -s "|" -v Apply=1 -i R01_drop_goal_progress_logs.sql -o R01_Apply1.txt
   ===================================================================== */
:on error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF DB_NAME() <> N'StudyHub' THROW 50000, N'Sai CSDL: phải chạy trên StudyHub.', 1;
IF '$(Apply)' NOT IN ('0', '1') THROW 50000, N'Apply phải là 0 (chạy thử) hoặc 1 (áp dụng).', 1;
BEGIN TRANSACTION;
SELECT COUNT_BIG(*) AS LogRowsToDrop FROM dbo.GoalProgressLogs;
DROP TABLE dbo.GoalProgressLogs;
DELETE FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261104090000_AddGoalProgressLogs';
IF '$(Apply)' = '1' BEGIN COMMIT TRANSACTION; PRINT N'>>> ĐÃ COMMIT.'; END
ELSE BEGIN ROLLBACK TRANSACTION; PRINT N'>>> CHẠY THỬ: đã ROLLBACK.'; END
GO
