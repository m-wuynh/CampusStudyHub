using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeBookAndColumnRefactoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ══════════════════════════════════════════════════════════════════
            // STEP 1: Create GradeBooks table
            // ══════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                CREATE TABLE dbo.GradeBooks (
                    GradeBookId     BIGINT IDENTITY(1,1) NOT NULL,
                    UserId          BIGINT NOT NULL,
                    AcademicTermId  BIGINT NOT NULL,
                    Name            NVARCHAR(200) NOT NULL,
                    CreatedAtUtc    DATETIME2(3) NOT NULL DEFAULT sysutcdatetime(),
                    UpdatedAtUtc    DATETIME2(3) NOT NULL DEFAULT sysutcdatetime(),
                    CONSTRAINT PK_GradeBooks PRIMARY KEY (GradeBookId),
                    CONSTRAINT FK_GradeBooks_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
                    CONSTRAINT FK_GradeBooks_TermOwner FOREIGN KEY (AcademicTermId, UserId)
                        REFERENCES dbo.AcademicTerms(AcademicTermId, UserId)
                );
                CREATE UNIQUE INDEX UQ_GradeBooks_UserTerm ON dbo.GradeBooks (UserId, AcademicTermId);
            ");

            // ══════════════════════════════════════════════════════════════════
            // STEP 2: Create GradeColumns table
            // ══════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                CREATE TABLE dbo.GradeColumns (
                    GradeColumnId   BIGINT IDENTITY(1,1) NOT NULL,
                    GradeBookId     BIGINT NOT NULL,
                    Name            NVARCHAR(100) NOT NULL,
                    Weight          DECIMAL(6,2) NOT NULL DEFAULT 1,
                    DisplayOrder    INT NOT NULL DEFAULT 0,
                    IsArchived      BIT NOT NULL DEFAULT 0,
                    CreatedAtUtc    DATETIME2(3) NOT NULL DEFAULT sysutcdatetime(),
                    UpdatedAtUtc    DATETIME2(3) NOT NULL DEFAULT sysutcdatetime(),
                    CONSTRAINT PK_GradeColumns PRIMARY KEY (GradeColumnId),
                    CONSTRAINT FK_GradeColumns_GradeBook FOREIGN KEY (GradeBookId)
                        REFERENCES dbo.GradeBooks(GradeBookId)
                );
                CREATE UNIQUE INDEX UQ_GradeColumns_Name ON dbo.GradeColumns (GradeBookId, Name)
                    WHERE IsArchived = 0;
                CREATE INDEX IX_GradeColumns_Order ON dbo.GradeColumns (GradeBookId, IsArchived, DisplayOrder);
            ");

            // ══════════════════════════════════════════════════════════════════
            // STEP 3: Add GradeColumnId (nullable) to GradeEntries
            // ══════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                ALTER TABLE dbo.GradeEntries ADD GradeColumnId BIGINT NULL;
            ");

            // ══════════════════════════════════════════════════════════════════
            // STEP 4: DATA MIGRATION
            // For each unique (UserId, AcademicTermId) that has GradeEntries:
            //   a) Create a GradeBook
            //   b) Create GradeColumns from distinct AssessmentType values
            //   c) Map GradeEntries to their GradeColumn
            //   d) Normalize Score from (Score/MaxScore*10) if MaxScore != 10
            // ══════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                -- 4a: Create GradeBooks for every (UserId, AcademicTermId) with data
                INSERT INTO dbo.GradeBooks (UserId, AcademicTermId, Name)
                SELECT DISTINCT us.UserId, us.AcademicTermId,
                       N'Bảng điểm ' + at.TermName
                FROM dbo.UserSubjects us
                INNER JOIN dbo.AcademicTerms at ON at.AcademicTermId = us.AcademicTermId AND at.UserId = us.UserId
                INNER JOIN dbo.GradeEntries ge ON ge.UserSubjectId = us.UserSubjectId
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.GradeBooks gb
                    WHERE gb.UserId = us.UserId AND gb.AcademicTermId = us.AcademicTermId
                );
            ");

            migrationBuilder.Sql(@"
                -- 4b: Create GradeColumns from distinct AssessmentType values per GradeBook
                -- Map: Other->Miệng(w1), Quiz->15 Phút(w1), Assignment->1 Tiết(w1),
                --      Midterm->Giữa kỳ(w2), Final->Cuối kỳ(w3)
                ;WITH DistinctTypes AS (
                    SELECT DISTINCT
                        gb.GradeBookId,
                        ge.AssessmentType,
                        CASE ge.AssessmentType
                            WHEN N'Other'      THEN N'Miệng'
                            WHEN N'Quiz'       THEN N'15 Phút'
                            WHEN N'Assignment' THEN N'1 Tiết'
                            WHEN N'Midterm'    THEN N'Giữa kỳ'
                            WHEN N'Final'      THEN N'Cuối kỳ'
                            ELSE ge.AssessmentType
                        END AS ColName,
                        CASE ge.AssessmentType
                            WHEN N'Midterm' THEN 2
                            WHEN N'Final'   THEN 3
                            ELSE 1
                        END AS ColWeight,
                        CASE ge.AssessmentType
                            WHEN N'Other'      THEN 1
                            WHEN N'Quiz'       THEN 2
                            WHEN N'Assignment' THEN 3
                            WHEN N'Midterm'    THEN 4
                            WHEN N'Final'      THEN 5
                            ELSE 6
                        END AS ColOrder
                    FROM dbo.GradeEntries ge
                    INNER JOIN dbo.UserSubjects us ON us.UserSubjectId = ge.UserSubjectId
                    INNER JOIN dbo.GradeBooks gb ON gb.UserId = us.UserId AND gb.AcademicTermId = us.AcademicTermId
                )
                INSERT INTO dbo.GradeColumns (GradeBookId, Name, Weight, DisplayOrder)
                SELECT GradeBookId, ColName, ColWeight, ColOrder
                FROM DistinctTypes
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.GradeColumns gc
                    WHERE gc.GradeBookId = DistinctTypes.GradeBookId AND gc.Name = DistinctTypes.ColName
                );
            ");

            migrationBuilder.Sql(@"
                -- 4c: Map GradeEntry.GradeColumnId by matching AssessmentType -> column name -> GradeColumn
                UPDATE ge
                SET ge.GradeColumnId = gc.GradeColumnId
                FROM dbo.GradeEntries ge
                INNER JOIN dbo.UserSubjects us ON us.UserSubjectId = ge.UserSubjectId
                INNER JOIN dbo.GradeBooks gb ON gb.UserId = us.UserId AND gb.AcademicTermId = us.AcademicTermId
                INNER JOIN dbo.GradeColumns gc ON gc.GradeBookId = gb.GradeBookId
                    AND gc.Name = CASE ge.AssessmentType
                                    WHEN N'Other'      THEN N'Miệng'
                                    WHEN N'Quiz'       THEN N'15 Phút'
                                    WHEN N'Assignment' THEN N'1 Tiết'
                                    WHEN N'Midterm'    THEN N'Giữa kỳ'
                                    WHEN N'Final'      THEN N'Cuối kỳ'
                                    ELSE ge.AssessmentType
                                  END
                WHERE ge.GradeColumnId IS NULL;
            ");

            migrationBuilder.Sql(@"
                -- 4d: Normalize Score to scale 0-10
                -- Only update entries where MaxScore is not 10 (avoid re-normalizing)
                UPDATE dbo.GradeEntries
                SET Score = CASE
                    WHEN MaxScore IS NULL OR MaxScore = 0 THEN Score
                    WHEN MaxScore = 10 THEN Score   -- already scale 10
                    ELSE CAST(Score / MaxScore * 10.0 AS DECIMAL(5,2))
                END
                WHERE MaxScore IS NOT NULL AND MaxScore != 10 AND MaxScore > 0;

                -- Cap at 10 (safety)
                UPDATE dbo.GradeEntries SET Score = 10 WHERE Score > 10;
                -- Floor at 0 (safety)
                UPDATE dbo.GradeEntries SET Score = 0 WHERE Score < 0;
            ");

            // ══════════════════════════════════════════════════════════════════
            // STEP 5: Make GradeColumnId NOT NULL, add FK and check constraint
            // ══════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                -- Delete any orphan entries that could not be mapped (shouldn't exist, but safety first)
                DELETE FROM dbo.GradeEntries WHERE GradeColumnId IS NULL;

                ALTER TABLE dbo.GradeEntries ALTER COLUMN GradeColumnId BIGINT NOT NULL;

                ALTER TABLE dbo.GradeEntries ADD CONSTRAINT FK_GradeEntries_Column
                    FOREIGN KEY (GradeColumnId) REFERENCES dbo.GradeColumns(GradeColumnId);

                CREATE INDEX IX_GradeEntries_Column
                    ON dbo.GradeEntries (GradeColumnId, UserSubjectId, IsDeleted);
            ");

            // ══════════════════════════════════════════════════════════════════
            // STEP 6: Remove deprecated columns from GradeEntries
            // (AssessmentType, MaxScore, Title, Weight - Weight moves to GradeColumn)
            // ══════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                -- Drop check constraints on AssessmentType, Weight, Score
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_GradeEntries_Type')
                    ALTER TABLE dbo.GradeEntries DROP CONSTRAINT CK_GradeEntries_Type;
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_GradeEntries_Weight')
                    ALTER TABLE dbo.GradeEntries DROP CONSTRAINT CK_GradeEntries_Weight;
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_GradeEntries_Score')
                    ALTER TABLE dbo.GradeEntries DROP CONSTRAINT CK_GradeEntries_Score;

                -- Drop index that includes MaxScore
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GradeEntries_Subject' AND object_id = OBJECT_ID('dbo.GradeEntries'))
                    DROP INDEX IX_GradeEntries_Subject ON dbo.GradeEntries;

                -- Drop the default constraint on AssessmentType before dropping the column
                DECLARE @constraintName NVARCHAR(200);
                SELECT @constraintName = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.GradeEntries') AND c.name = 'AssessmentType';
                IF @constraintName IS NOT NULL
                    EXEC('ALTER TABLE dbo.GradeEntries DROP CONSTRAINT ' + @constraintName);

                DECLARE @maxScoreConstraint NVARCHAR(200);
                SELECT @maxScoreConstraint = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.GradeEntries') AND c.name = 'MaxScore';
                IF @maxScoreConstraint IS NOT NULL
                    EXEC('ALTER TABLE dbo.GradeEntries DROP CONSTRAINT ' + @maxScoreConstraint);

                DECLARE @weightConstraint NVARCHAR(200);
                SELECT @weightConstraint = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.GradeEntries') AND c.name = 'Weight';
                IF @weightConstraint IS NOT NULL
                    EXEC('ALTER TABLE dbo.GradeEntries DROP CONSTRAINT ' + @weightConstraint);

                ALTER TABLE dbo.GradeEntries DROP COLUMN AssessmentType;
                ALTER TABLE dbo.GradeEntries DROP COLUMN MaxScore;
                ALTER TABLE dbo.GradeEntries DROP COLUMN Title;
                ALTER TABLE dbo.GradeEntries DROP COLUMN Weight;

                -- Recreate the index without MaxScore
                CREATE INDEX IX_GradeEntries_Subject
                    ON dbo.GradeEntries (UserSubjectId, IsDeleted);

                ALTER TABLE dbo.GradeEntries ADD CONSTRAINT CK_GradeEntries_Score
                    CHECK (Score >= 0 AND Score <= 10);
            ");

            // ══════════════════════════════════════════════════════════════════
            // STEP 7: Update Goal table
            // - Drop CurrentValue, UnitCode, StartDate columns
            // - Make UserSubjectId NOT NULL
            // - Add unique filtered index for Goal Ladder
            // ══════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                -- Drop IX_Goals_User index first (it includes EndDate which we keep as nullable)
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Goals_User' AND object_id = OBJECT_ID('dbo.Goals'))
                    DROP INDEX IX_Goals_User ON dbo.Goals;

                -- Drop dependent check constraints
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Goals_Dates')
                    ALTER TABLE dbo.Goals DROP CONSTRAINT CK_Goals_Dates;
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Goals_Score')
                    ALTER TABLE dbo.Goals DROP CONSTRAINT CK_Goals_Score;
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Goals_Unit')
                    ALTER TABLE dbo.Goals DROP CONSTRAINT CK_Goals_Unit;
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Goals_Values')
                    ALTER TABLE dbo.Goals DROP CONSTRAINT CK_Goals_Values;

                -- Remove CurrentValue column (computed on-the-fly from average)
                DECLARE @currentValueDefault NVARCHAR(200);
                SELECT @currentValueDefault = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.Goals') AND c.name = 'CurrentValue';
                IF @currentValueDefault IS NOT NULL
                    EXEC('ALTER TABLE dbo.Goals DROP CONSTRAINT ' + @currentValueDefault);
                ALTER TABLE dbo.Goals DROP COLUMN CurrentValue;

                -- Remove UnitCode column (no longer needed)
                DECLARE @unitCodeDefault NVARCHAR(200);
                SELECT @unitCodeDefault = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.Goals') AND c.name = 'UnitCode';
                IF @unitCodeDefault IS NOT NULL
                    EXEC('ALTER TABLE dbo.Goals DROP CONSTRAINT ' + @unitCodeDefault);
                ALTER TABLE dbo.Goals DROP COLUMN UnitCode;

                -- Remove StartDate column
                ALTER TABLE dbo.Goals DROP COLUMN StartDate;

                -- Make EndDate nullable (it was NOT NULL before, now optional)
                -- Need to handle existing non-null data first
                -- (EndDate is now optional, so just change the type)
                ALTER TABLE dbo.Goals ALTER COLUMN EndDate DATE NULL;

                -- Make UserSubjectId NOT NULL (all goals must be for a subject now)
                -- First: cancel any orphan goals with NULL UserSubjectId
                UPDATE dbo.Goals SET IsCancelled = 1 WHERE UserSubjectId IS NULL;
                -- Then remove them or leave them cancelled; we'll delete to enforce NOT NULL
                DELETE FROM dbo.Goals WHERE UserSubjectId IS NULL;
                ALTER TABLE dbo.Goals ALTER COLUMN UserSubjectId BIGINT NOT NULL;

                -- Recreate the index without EndDate in the key
                CREATE INDEX IX_Goals_User ON dbo.Goals (UserId, IsCancelled);

                -- Add unique index for Goal Ladder (no two active goals with same TargetValue per subject)
                CREATE UNIQUE INDEX UQ_Goals_SubjectTargetValue ON dbo.Goals (UserSubjectId, TargetValue)
                    WHERE IsCancelled = 0;
            ");

            // ══════════════════════════════════════════════════════════════════
            // STEP 8: Remove TargetScore10 from UserSubjects
            // ══════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UserSubjects_Target')
                    ALTER TABLE dbo.UserSubjects DROP CONSTRAINT CK_UserSubjects_Target;

                ALTER TABLE dbo.UserSubjects DROP COLUMN TargetScore10;
            ");

            // ══════════════════════════════════════════════════════════════════
            // STEP 9: Update vw_SubjectGradeSummary
            // New formula: SUM(Score * Weight) / SUM(Weight) using GradeColumn weights
            // Only active columns (IsArchived = 0), non-deleted entries
            // ══════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                ALTER VIEW dbo.vw_SubjectGradeSummary AS
                SELECT
                    us.UserSubjectId,
                    us.UserId,
                    us.AcademicTermId,
                    us.SubjectId,
                    COALESCE(us.CustomSubjectName, s.SubjectName) AS SubjectName,
                    us.CreditWeight,
                    COUNT(ge.GradeEntryId) AS EnteredAssessmentCount,
                    CAST(
                        SUM(ge.Score * gc.Weight) / NULLIF(SUM(gc.Weight), 0)
                    AS decimal(5,2)) AS CurrentAverage10
                FROM dbo.UserSubjects AS us
                LEFT JOIN dbo.Subjects AS s ON s.SubjectId = us.SubjectId
                LEFT JOIN dbo.GradeEntries AS ge
                    ON ge.UserSubjectId = us.UserSubjectId AND ge.IsDeleted = 0
                LEFT JOIN dbo.GradeColumns AS gc
                    ON gc.GradeColumnId = ge.GradeColumnId AND gc.IsArchived = 0
                WHERE us.IsArchived = 0
                GROUP BY
                    us.UserSubjectId, us.UserId, us.AcademicTermId, us.SubjectId,
                    us.CustomSubjectName, s.SubjectName, us.CreditWeight;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Revert vw_SubjectGradeSummary ──────────────────────────────
            migrationBuilder.Sql(@"
                ALTER VIEW dbo.vw_SubjectGradeSummary AS
                SELECT us.UserSubjectId, us.UserId, us.AcademicTermId, us.SubjectId,
                       COALESCE(us.CustomSubjectName, s.SubjectName) AS SubjectName,
                       us.CreditWeight, us.TargetScore10,
                       COUNT(g.GradeEntryId) AS EnteredAssessmentCount,
                       CAST(SUM((g.Score / NULLIF(g.MaxScore, 0)) * 10.0 * g.Weight)
                            / NULLIF(SUM(g.Weight), 0) AS decimal(5,2)) AS CurrentAverage10
                FROM dbo.UserSubjects AS us
                LEFT JOIN dbo.Subjects AS s ON s.SubjectId = us.SubjectId
                LEFT JOIN dbo.GradeEntries AS g ON g.UserSubjectId = us.UserSubjectId AND g.IsDeleted = 0
                GROUP BY us.UserSubjectId, us.UserId, us.AcademicTermId, us.SubjectId,
                         us.CustomSubjectName, s.SubjectName, us.CreditWeight, us.TargetScore10;
            ");

            // NOTE: Down migration does NOT fully restore data (irreversible data transformation).
            // The structural DDL can be reversed but Score normalization and column mapping cannot.
            // This Down() is provided for structural rollback only.

            migrationBuilder.Sql(@"
                ALTER TABLE dbo.UserSubjects ADD TargetScore10 DECIMAL(4,2) NULL;
            ");

            migrationBuilder.Sql(@"
                -- Restore Goal columns
                DROP INDEX IF EXISTS UQ_Goals_SubjectTargetValue ON dbo.Goals;
                DROP INDEX IF EXISTS IX_Goals_User ON dbo.Goals;
                ALTER TABLE dbo.Goals ALTER COLUMN UserSubjectId BIGINT NULL;
                ALTER TABLE dbo.Goals ALTER COLUMN EndDate DATE NOT NULL;
                ALTER TABLE dbo.Goals ADD StartDate DATE NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE);
                ALTER TABLE dbo.Goals ADD UnitCode NVARCHAR(15) NULL;
                ALTER TABLE dbo.Goals ADD CurrentValue DECIMAL(12,2) NOT NULL DEFAULT 0;
                CREATE INDEX IX_Goals_User ON dbo.Goals (UserId, IsCancelled, EndDate);
            ");

            migrationBuilder.Sql(@"
                -- Restore GradeEntries columns
                DROP INDEX IF EXISTS IX_GradeEntries_Column ON dbo.GradeEntries;
                ALTER TABLE dbo.GradeEntries DROP CONSTRAINT IF EXISTS CK_GradeEntries_Score;
                ALTER TABLE dbo.GradeEntries DROP CONSTRAINT IF EXISTS FK_GradeEntries_Column;
                ALTER TABLE dbo.GradeEntries DROP COLUMN GradeColumnId;
                ALTER TABLE dbo.GradeEntries ADD AssessmentType NVARCHAR(20) NOT NULL DEFAULT 'Other';
                ALTER TABLE dbo.GradeEntries ADD MaxScore DECIMAL(8,2) NOT NULL DEFAULT 10;
                ALTER TABLE dbo.GradeEntries ADD Title NVARCHAR(150) NOT NULL DEFAULT '';
                ALTER TABLE dbo.GradeEntries ADD Weight DECIMAL(6,2) NOT NULL DEFAULT 1;
            ");

            migrationBuilder.Sql(@"DROP TABLE IF EXISTS dbo.GradeColumns;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS dbo.GradeBooks;");
        }
    }
}
