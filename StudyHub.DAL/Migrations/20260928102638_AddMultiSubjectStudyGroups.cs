using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations;

/// <summary>
/// Adds reusable personal subject names and changes study groups from one
/// catalog subject to multiple catalog or custom subjects.
/// </summary>
public partial class AddMultiSubjectStudyGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.UserSubjects', 'CustomSubjectName') IS NULL
                ALTER TABLE dbo.UserSubjects ADD CustomSubjectName nvarchar(150) NULL;

            IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_UserSubjects_Enrollment')
                ALTER TABLE dbo.UserSubjects DROP CONSTRAINT UQ_UserSubjects_Enrollment;

            IF EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('dbo.UserSubjects')
                  AND name = 'SubjectId' AND is_nullable = 0)
                ALTER TABLE dbo.UserSubjects ALTER COLUMN SubjectId int NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UserSubjects_SubjectOrCustom')
                ALTER TABLE dbo.UserSubjects ADD CONSTRAINT CK_UserSubjects_SubjectOrCustom CHECK (
                    (SubjectId IS NOT NULL AND CustomSubjectName IS NULL)
                    OR (SubjectId IS NULL AND CustomSubjectName IS NOT NULL));

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'UQ_UserSubjects_CatalogEnrollment'
                  AND object_id = OBJECT_ID('dbo.UserSubjects'))
                CREATE UNIQUE INDEX UQ_UserSubjects_CatalogEnrollment
                    ON dbo.UserSubjects(UserId, AcademicTermId, SubjectId)
                    WHERE SubjectId IS NOT NULL AND IsArchived = 0;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'UQ_UserSubjects_CustomEnrollment'
                  AND object_id = OBJECT_ID('dbo.UserSubjects'))
                CREATE UNIQUE INDEX UQ_UserSubjects_CustomEnrollment
                    ON dbo.UserSubjects(UserId, AcademicTermId, CustomSubjectName)
                    WHERE CustomSubjectName IS NOT NULL AND IsArchived = 0;

            IF OBJECT_ID('dbo.StudyGroupSubjects', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.StudyGroupSubjects (
                    StudyGroupSubjectId bigint IDENTITY(1,1) NOT NULL
                        CONSTRAINT PK_StudyGroupSubjects PRIMARY KEY,
                    StudyGroupId bigint NOT NULL,
                    SubjectId int NULL,
                    CustomSubjectName nvarchar(150) NULL,
                    AddedByUserId bigint NOT NULL,
                    CreatedAtUtc datetime2(3) NOT NULL
                        CONSTRAINT DF_StudyGroupSubjects_Created DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_StudyGroupSubjects_Group
                        FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId) ON DELETE CASCADE,
                    CONSTRAINT FK_StudyGroupSubjects_Subject
                        FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects(SubjectId),
                    CONSTRAINT FK_StudyGroupSubjects_AddedBy
                        FOREIGN KEY (AddedByUserId) REFERENCES dbo.Users(UserId),
                    CONSTRAINT CK_StudyGroupSubjects_Choice CHECK (
                        (SubjectId IS NOT NULL AND CustomSubjectName IS NULL)
                        OR (SubjectId IS NULL AND CustomSubjectName IS NOT NULL))
                );

                CREATE INDEX IX_StudyGroupSubjects_AddedByUserId
                    ON dbo.StudyGroupSubjects(AddedByUserId);
                CREATE INDEX IX_StudyGroupSubjects_SubjectId
                    ON dbo.StudyGroupSubjects(SubjectId);
                CREATE UNIQUE INDEX UX_StudyGroupSubjects_Catalog
                    ON dbo.StudyGroupSubjects(StudyGroupId, SubjectId)
                    WHERE SubjectId IS NOT NULL;
                CREATE UNIQUE INDEX UX_StudyGroupSubjects_Custom
                    ON dbo.StudyGroupSubjects(StudyGroupId, CustomSubjectName)
                    WHERE CustomSubjectName IS NOT NULL;
            END;

            IF COL_LENGTH('dbo.StudyGroups', 'SubjectId') IS NOT NULL
            BEGIN
                EXEC(N'
                    INSERT dbo.StudyGroupSubjects(StudyGroupId, SubjectId, AddedByUserId, CreatedAtUtc)
                    SELECT StudyGroupId, SubjectId, OwnerUserId, CreatedAtUtc
                    FROM dbo.StudyGroups AS source
                    WHERE SubjectId IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.StudyGroupSubjects AS target
                          WHERE target.StudyGroupId = source.StudyGroupId
                            AND target.SubjectId = source.SubjectId);');

                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_StudyGroups_Subject')
                    ALTER TABLE dbo.StudyGroups DROP CONSTRAINT FK_StudyGroups_Subject;
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_StudyGroups_SubjectId'
                      AND object_id = OBJECT_ID('dbo.StudyGroups'))
                    DROP INDEX IX_StudyGroups_SubjectId ON dbo.StudyGroups;
                ALTER TABLE dbo.StudyGroups DROP COLUMN SubjectId;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.StudyGroups', 'SubjectId') IS NULL
                ALTER TABLE dbo.StudyGroups ADD SubjectId int NULL;

            IF OBJECT_ID('dbo.StudyGroupSubjects', 'U') IS NOT NULL
            BEGIN
                EXEC(N'
                    UPDATE groups
                    SET SubjectId = subjects.SubjectId
                    FROM dbo.StudyGroups AS groups
                    OUTER APPLY (
                        SELECT TOP (1) link.SubjectId
                        FROM dbo.StudyGroupSubjects AS link
                        WHERE link.StudyGroupId = groups.StudyGroupId
                          AND link.SubjectId IS NOT NULL
                        ORDER BY link.StudyGroupSubjectId
                    ) AS subjects;');
                DROP TABLE dbo.StudyGroupSubjects;
            END;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_StudyGroups_SubjectId'
                  AND object_id = OBJECT_ID('dbo.StudyGroups'))
                CREATE INDEX IX_StudyGroups_SubjectId ON dbo.StudyGroups(SubjectId);

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_StudyGroups_Subject')
                ALTER TABLE dbo.StudyGroups ADD CONSTRAINT FK_StudyGroups_Subject
                    FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects(SubjectId);
            """);
    }
}
