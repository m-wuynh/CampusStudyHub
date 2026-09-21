/* Study Hub | SQL Server 2019+ / Express / LocalDB
   Run in SSMS. Creates a database only when absent; never drops existing data.
   If you change the database name, change USE/DB_NAME guards in every script.
*/
USE [master];
GO
IF DB_ID(N'StudyHub') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [StudyHub] COLLATE Latin1_General_100_CI_AS_SC;');
    PRINT N'Created StudyHub. Next: 01_Schema.sql';
END
ELSE
    PRINT N'StudyHub already exists. 01_Schema.sql requires an empty database.';
GO
