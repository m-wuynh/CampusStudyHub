using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations;

/// <summary>
/// Adds the Discord-style access, posts and invitations to an existing
/// Database First StudyHub schema. Every operation is idempotent so databases
/// previously upgraded with the legacy SQL script can adopt EF migrations.
/// </summary>
public partial class AddDiscordStudyGroupFlow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.StudyGroups', 'Goal') IS NULL
                ALTER TABLE dbo.StudyGroups ADD Goal nvarchar(500) NULL;

            IF COL_LENGTH('dbo.StudyGroups', 'MeetingFormat') IS NULL
                ALTER TABLE dbo.StudyGroups ADD MeetingFormat varchar(10) NOT NULL
                    CONSTRAINT DF_StudyGroups_MeetingFormat DEFAULT ('Online');

            IF COL_LENGTH('dbo.StudyGroups', 'MeetingSchedule') IS NULL
                ALTER TABLE dbo.StudyGroups ADD MeetingSchedule nvarchar(250) NULL;

            IF COL_LENGTH('dbo.StudyGroups', 'ContactUrl') IS NULL
                ALTER TABLE dbo.StudyGroups ADD ContactUrl nvarchar(2048) NULL;

            IF COL_LENGTH('dbo.StudyGroups', 'Rules') IS NULL
                ALTER TABLE dbo.StudyGroups ADD Rules nvarchar(2000) NULL;

            DECLARE @JoinModeAdded bit = 0;
            IF COL_LENGTH('dbo.StudyGroups', 'JoinMode') IS NULL
            BEGIN
                ALTER TABLE dbo.StudyGroups ADD JoinMode varchar(15) NOT NULL
                    CONSTRAINT DF_StudyGroups_JoinMode DEFAULT ('Approval');
                SET @JoinModeAdded = 1;
            END;

            IF COL_LENGTH('dbo.StudyGroups', 'UpdatedAtUtc') IS NULL
                ALTER TABLE dbo.StudyGroups ADD UpdatedAtUtc datetime2(3) NOT NULL
                    CONSTRAINT DF_StudyGroups_Updated DEFAULT (SYSUTCDATETIME());

            IF NOT EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = 'CK_StudyGroups_JoinMode'
                  AND parent_object_id = OBJECT_ID('dbo.StudyGroups'))
                EXEC(N'ALTER TABLE dbo.StudyGroups ADD CONSTRAINT CK_StudyGroups_JoinMode
                    CHECK (JoinMode IN (''Open'', ''Approval'', ''InviteOnly''));');

            IF NOT EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = 'CK_StudyGroups_MeetingFormat'
                  AND parent_object_id = OBJECT_ID('dbo.StudyGroups'))
                EXEC(N'ALTER TABLE dbo.StudyGroups ADD CONSTRAINT CK_StudyGroups_MeetingFormat
                    CHECK (MeetingFormat IN (''Online'', ''Offline'', ''Hybrid''));');

            IF @JoinModeAdded = 1
                EXEC(N'UPDATE dbo.StudyGroups
                    SET JoinMode = CASE
                        WHEN Visibility = ''Private'' THEN ''InviteOnly''
                        ELSE ''Approval''
                    END;');

            IF COL_LENGTH('dbo.GroupPosts', 'PostType') IS NULL
                ALTER TABLE dbo.GroupPosts ADD PostType varchar(15) NOT NULL
                    CONSTRAINT DF_GroupPosts_PostType DEFAULT ('Message');

            IF NOT EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = 'CK_GroupPosts_Type'
                  AND parent_object_id = OBJECT_ID('dbo.GroupPosts'))
                EXEC(N'ALTER TABLE dbo.GroupPosts ADD CONSTRAINT CK_GroupPosts_Type
                    CHECK (PostType IN (''Message'', ''Announcement''));');

            IF OBJECT_ID('dbo.GroupInvites', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.GroupInvites (
                    InviteCode varchar(64) NOT NULL
                        CONSTRAINT PK_GroupInvites PRIMARY KEY,
                    StudyGroupId bigint NOT NULL,
                    CreatedByUserId bigint NOT NULL,
                    ExpiresAtUtc datetime2(3) NOT NULL,
                    MaxUses int NOT NULL
                        CONSTRAINT DF_GroupInvites_MaxUses DEFAULT (20),
                    UseCount int NOT NULL
                        CONSTRAINT DF_GroupInvites_UseCount DEFAULT (0),
                    IsRevoked bit NOT NULL
                        CONSTRAINT DF_GroupInvites_Revoked DEFAULT (0),
                    CreatedAtUtc datetime2(3) NOT NULL
                        CONSTRAINT DF_GroupInvites_Created DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_GroupInvites_Group
                        FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId),
                    CONSTRAINT FK_GroupInvites_Creator
                        FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId),
                    CONSTRAINT CK_GroupInvites_Uses
                        CHECK (MaxUses BETWEEN 1 AND 1000 AND UseCount BETWEEN 0 AND MaxUses)
                );

                CREATE INDEX IX_GroupInvites_Group
                    ON dbo.GroupInvites(StudyGroupId, IsRevoked, ExpiresAtUtc);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID('dbo.GroupInvites', 'U') IS NOT NULL
                DROP TABLE dbo.GroupInvites;

            IF EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = 'CK_GroupPosts_Type'
                  AND parent_object_id = OBJECT_ID('dbo.GroupPosts'))
                ALTER TABLE dbo.GroupPosts DROP CONSTRAINT CK_GroupPosts_Type;

            IF EXISTS (
                SELECT 1 FROM sys.default_constraints
                WHERE name = 'DF_GroupPosts_PostType'
                  AND parent_object_id = OBJECT_ID('dbo.GroupPosts'))
                ALTER TABLE dbo.GroupPosts DROP CONSTRAINT DF_GroupPosts_PostType;

            IF COL_LENGTH('dbo.GroupPosts', 'PostType') IS NOT NULL
                ALTER TABLE dbo.GroupPosts DROP COLUMN PostType;

            IF EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = 'CK_StudyGroups_JoinMode'
                  AND parent_object_id = OBJECT_ID('dbo.StudyGroups'))
                ALTER TABLE dbo.StudyGroups DROP CONSTRAINT CK_StudyGroups_JoinMode;

            IF EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = 'CK_StudyGroups_MeetingFormat'
                  AND parent_object_id = OBJECT_ID('dbo.StudyGroups'))
                ALTER TABLE dbo.StudyGroups DROP CONSTRAINT CK_StudyGroups_MeetingFormat;

            IF EXISTS (
                SELECT 1 FROM sys.default_constraints
                WHERE name = 'DF_StudyGroups_JoinMode'
                  AND parent_object_id = OBJECT_ID('dbo.StudyGroups'))
                ALTER TABLE dbo.StudyGroups DROP CONSTRAINT DF_StudyGroups_JoinMode;

            IF EXISTS (
                SELECT 1 FROM sys.default_constraints
                WHERE name = 'DF_StudyGroups_MeetingFormat'
                  AND parent_object_id = OBJECT_ID('dbo.StudyGroups'))
                ALTER TABLE dbo.StudyGroups DROP CONSTRAINT DF_StudyGroups_MeetingFormat;

            IF EXISTS (
                SELECT 1 FROM sys.default_constraints
                WHERE name = 'DF_StudyGroups_Updated'
                  AND parent_object_id = OBJECT_ID('dbo.StudyGroups'))
                ALTER TABLE dbo.StudyGroups DROP CONSTRAINT DF_StudyGroups_Updated;

            IF COL_LENGTH('dbo.StudyGroups', 'Goal') IS NOT NULL
                ALTER TABLE dbo.StudyGroups DROP COLUMN Goal;
            IF COL_LENGTH('dbo.StudyGroups', 'MeetingFormat') IS NOT NULL
                ALTER TABLE dbo.StudyGroups DROP COLUMN MeetingFormat;
            IF COL_LENGTH('dbo.StudyGroups', 'MeetingSchedule') IS NOT NULL
                ALTER TABLE dbo.StudyGroups DROP COLUMN MeetingSchedule;
            IF COL_LENGTH('dbo.StudyGroups', 'ContactUrl') IS NOT NULL
                ALTER TABLE dbo.StudyGroups DROP COLUMN ContactUrl;
            IF COL_LENGTH('dbo.StudyGroups', 'Rules') IS NOT NULL
                ALTER TABLE dbo.StudyGroups DROP COLUMN Rules;
            IF COL_LENGTH('dbo.StudyGroups', 'JoinMode') IS NOT NULL
                ALTER TABLE dbo.StudyGroups DROP COLUMN JoinMode;
            IF COL_LENGTH('dbo.StudyGroups', 'UpdatedAtUtc') IS NOT NULL
                ALTER TABLE dbo.StudyGroups DROP COLUMN UpdatedAtUtc;
            """);
    }
}
