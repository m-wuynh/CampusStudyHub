/*
 STUDY HUB - schema v1, SQL Server 2019+ (including Express/LocalDB).
 Scope: Google login, notes/documents, flashcards, calendar/deadlines,
 study groups, grades/goals, streak, settings and basic administration.
 Run once, after 00_CreateDatabase.sql, on an EMPTY StudyHub database.
 No DROP, no CASCADE DELETE, no real accounts, no passwords or OAuth secrets.
 All *Utc timestamps are UTC. *Local timestamps are wall-clock calendar times.
 UpdatedAtUtc must be updated by the application; ROWVERSION is not a date.
 Database integrity is NOT API authorization: enforce access in the backend.
*/
USE [StudyHub];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    IF DB_NAME() <> N'StudyHub'
        THROW 51000, N'Wrong database. Expected StudyHub.', 1;
    IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
        THROW 51001, N'Database is not empty. Stop: use a new database or write a migration.', 1;

    BEGIN TRANSACTION;

    -- 1. ACCOUNT / PROFILE. Email is contact data, not a Google identity key.
    CREATE TABLE dbo.Users (
        UserId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        DisplayName nvarchar(100) NOT NULL,
        Email nvarchar(320) NULL,
        AvatarUrl nvarchar(2048) NULL,
        EducationLevel varchar(20) NOT NULL CONSTRAINT DF_Users_Education DEFAULT ('University'),
        SchoolName nvarchar(200) NULL,
        ClassName nvarchar(100) NULL,
        StudentCode nvarchar(50) NULL,
        RoleCode varchar(20) NOT NULL CONSTRAINT DF_Users_Role DEFAULT ('Student'),
        Status varchar(20) NOT NULL CONSTRAINT DF_Users_Status DEFAULT ('Active'),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Users_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Users_Updated DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT CK_Users_Name CHECK (LEN(LTRIM(RTRIM(DisplayName))) > 0),
        CONSTRAINT CK_Users_Education CHECK (EducationLevel IN ('HighSchool','University','Other')),
        CONSTRAINT CK_Users_Role CHECK (RoleCode IN ('Student','Admin')),
        CONSTRAINT CK_Users_Status CHECK (Status IN ('Active','Suspended','Deleted'))
    );
    CREATE INDEX IX_Users_Email ON dbo.Users(Email);

    CREATE TABLE dbo.ExternalLogins (
        Provider varchar(20) COLLATE Latin1_General_100_BIN2 NOT NULL,
        ProviderSubject varchar(255) COLLATE Latin1_General_100_BIN2 NOT NULL,
        UserId bigint NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ExternalLogins_Created DEFAULT (SYSUTCDATETIME()),
        LastLoginAtUtc datetime2(3) NULL,
        CONSTRAINT PK_ExternalLogins PRIMARY KEY (Provider, ProviderSubject),
        CONSTRAINT UQ_ExternalLogins_UserProvider UNIQUE (UserId, Provider),
        CONSTRAINT FK_ExternalLogins_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_ExternalLogins_Provider CHECK (Provider = 'Google'),
        CONSTRAINT CK_ExternalLogins_Subject CHECK (LEN(ProviderSubject) > 0)
    );

    CREATE TABLE dbo.UserSettings (
        UserId bigint NOT NULL CONSTRAINT PK_UserSettings PRIMARY KEY,
        Theme varchar(10) NOT NULL CONSTRAINT DF_UserSettings_Theme DEFAULT ('System'),
        LanguageCode varchar(10) NOT NULL CONSTRAINT DF_UserSettings_Language DEFAULT ('vi'),
        TimeZoneId nvarchar(100) NOT NULL CONSTRAINT DF_UserSettings_TimeZone DEFAULT (N'SE Asia Standard Time'),
        EmailRemindersEnabled bit NOT NULL CONSTRAINT DF_UserSettings_Email DEFAULT (0),
        InAppRemindersEnabled bit NOT NULL CONSTRAINT DF_UserSettings_InApp DEFAULT (1),
        DefaultReminderMinutes smallint NOT NULL CONSTRAINT DF_UserSettings_Minutes DEFAULT (15),
        DailyStudyGoalMinutes smallint NOT NULL CONSTRAINT DF_UserSettings_DailyGoal DEFAULT (30),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_UserSettings_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_UserSettings_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_UserSettings_Theme CHECK (Theme IN ('Light','Dark','System')),
        CONSTRAINT CK_UserSettings_Minutes CHECK (DefaultReminderMinutes BETWEEN 0 AND 10080),
        CONSTRAINT CK_UserSettings_DailyGoal CHECK (DailyStudyGoalMinutes BETWEEN 1 AND 1440)
    );

    -- 2. SUBJECT CATALOG AND EACH USER'S TERMS / SUBJECT ENROLLMENTS.
    CREATE TABLE dbo.Subjects (
        SubjectId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Subjects PRIMARY KEY,
        SubjectCode varchar(30) NOT NULL CONSTRAINT UQ_Subjects_Code UNIQUE,
        SubjectName nvarchar(150) NOT NULL,
        Description nvarchar(1000) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Subjects_Active DEFAULT (1)
    );

    CREATE TABLE dbo.AcademicTerms (
        AcademicTermId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AcademicTerms PRIMARY KEY,
        UserId bigint NOT NULL,
        TermName nvarchar(100) NOT NULL,
        StartDate date NOT NULL,
        EndDate date NOT NULL,
        IsArchived bit NOT NULL CONSTRAINT DF_AcademicTerms_Archived DEFAULT (0),
        CONSTRAINT UQ_AcademicTerms_Owner UNIQUE (AcademicTermId, UserId),
        CONSTRAINT UQ_AcademicTerms_Name UNIQUE (UserId, TermName),
        CONSTRAINT FK_AcademicTerms_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_AcademicTerms_Dates CHECK (EndDate >= StartDate)
    );

    CREATE TABLE dbo.UserSubjects (
        UserSubjectId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserSubjects PRIMARY KEY,
        UserId bigint NOT NULL,
        AcademicTermId bigint NOT NULL,
        SubjectId int NOT NULL,
        TeacherName nvarchar(100) NULL,
        ClassCode nvarchar(50) NULL,
        CreditWeight decimal(5,2) NOT NULL CONSTRAINT DF_UserSubjects_Credits DEFAULT (1),
        TargetScore10 decimal(4,2) NULL,
        ColorHex char(7) NOT NULL CONSTRAINT DF_UserSubjects_Color DEFAULT ('#5138EE'),
        IsArchived bit NOT NULL CONSTRAINT DF_UserSubjects_Archived DEFAULT (0),
        CONSTRAINT UQ_UserSubjects_Owner UNIQUE (UserSubjectId, UserId),
        CONSTRAINT UQ_UserSubjects_Enrollment UNIQUE (UserId, AcademicTermId, SubjectId),
        CONSTRAINT FK_UserSubjects_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_UserSubjects_TermOwner FOREIGN KEY (AcademicTermId, UserId) REFERENCES dbo.AcademicTerms(AcademicTermId, UserId),
        CONSTRAINT FK_UserSubjects_Subject FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects(SubjectId),
        CONSTRAINT CK_UserSubjects_Credits CHECK (CreditWeight > 0),
        CONSTRAINT CK_UserSubjects_Target CHECK (TargetScore10 BETWEEN 0 AND 10),
        CONSTRAINT CK_UserSubjects_Color CHECK (ColorHex COLLATE Latin1_General_100_BIN2 LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]')
    );

    -- 3. STUDY GROUPS. OwnerUserId is the sole source of owner authority.
    CREATE TABLE dbo.StudyGroups (
        StudyGroupId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudyGroups PRIMARY KEY,
        OwnerUserId bigint NOT NULL,
        SubjectId int NULL,
        GroupName nvarchar(150) NOT NULL,
        Description nvarchar(2000) NULL,
        Visibility varchar(10) NOT NULL CONSTRAINT DF_StudyGroups_Visibility DEFAULT ('Private'),
        MaxMembers smallint NOT NULL CONSTRAINT DF_StudyGroups_Max DEFAULT (20),
        IsArchived bit NOT NULL CONSTRAINT DF_StudyGroups_Archived DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_StudyGroups_Created DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_StudyGroups_Owner FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_StudyGroups_Subject FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects(SubjectId),
        CONSTRAINT CK_StudyGroups_Visibility CHECK (Visibility IN ('Public','Private')),
        CONSTRAINT CK_StudyGroups_Max CHECK (MaxMembers BETWEEN 2 AND 500),
        CONSTRAINT CK_StudyGroups_Name CHECK (LEN(LTRIM(RTRIM(GroupName))) > 0)
    );

    CREATE TABLE dbo.GroupMembers (
        StudyGroupId bigint NOT NULL,
        UserId bigint NOT NULL,
        MemberRole varchar(15) NOT NULL CONSTRAINT DF_GroupMembers_Role DEFAULT ('Member'),
        Status varchar(15) NOT NULL CONSTRAINT DF_GroupMembers_Status DEFAULT ('Pending'),
        RequestedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GroupMembers_Requested DEFAULT (SYSUTCDATETIME()),
        JoinedAtUtc datetime2(3) NULL,
        CONSTRAINT PK_GroupMembers PRIMARY KEY (StudyGroupId, UserId),
        CONSTRAINT FK_GroupMembers_Group FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId),
        CONSTRAINT FK_GroupMembers_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_GroupMembers_Role CHECK (MemberRole IN ('Member','Moderator')),
        CONSTRAINT CK_GroupMembers_Status CHECK (Status IN ('Pending','Active','Rejected','Left','Banned')),
        CONSTRAINT CK_GroupMembers_Joined CHECK (Status <> 'Active' OR JoinedAtUtc IS NOT NULL)
    );
    CREATE INDEX IX_GroupMembers_User ON dbo.GroupMembers(UserId, Status, StudyGroupId);

    CREATE TABLE dbo.GroupPosts (
        GroupPostId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GroupPosts PRIMARY KEY,
        StudyGroupId bigint NOT NULL,
        AuthorUserId bigint NOT NULL,
        Body nvarchar(max) NOT NULL,
        IsPinned bit NOT NULL CONSTRAINT DF_GroupPosts_Pinned DEFAULT (0),
        IsDeleted bit NOT NULL CONSTRAINT DF_GroupPosts_Deleted DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GroupPosts_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GroupPosts_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_GroupPosts_Member FOREIGN KEY (StudyGroupId, AuthorUserId) REFERENCES dbo.GroupMembers(StudyGroupId, UserId),
        CONSTRAINT CK_GroupPosts_Body CHECK (LEN(LTRIM(RTRIM(Body))) > 0)
    );
    CREATE INDEX IX_GroupPosts_Feed ON dbo.GroupPosts(StudyGroupId, IsDeleted, CreatedAtUtc DESC);

    -- 4. NOTES AND DOCUMENTS. Group visibility requires exactly one group.
    -- Composite subject/owner FK prevents attaching another user's subject.
    CREATE TABLE dbo.Notes (
        NoteId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notes PRIMARY KEY,
        OwnerUserId bigint NOT NULL,
        UserSubjectId bigint NULL,
        StudyGroupId bigint NULL,
        Title nvarchar(200) NOT NULL,
        Body nvarchar(max) NOT NULL CONSTRAINT DF_Notes_Body DEFAULT (N''),
        BodyFormat varchar(10) NOT NULL CONSTRAINT DF_Notes_Format DEFAULT ('Markdown'),
        Visibility varchar(10) NOT NULL CONSTRAINT DF_Notes_Visibility DEFAULT ('Private'),
        IsPinned bit NOT NULL CONSTRAINT DF_Notes_Pinned DEFAULT (0),
        IsDeleted bit NOT NULL CONSTRAINT DF_Notes_Deleted DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Notes_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Notes_Updated DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_Notes_User FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_Notes_SubjectOwner FOREIGN KEY (UserSubjectId, OwnerUserId) REFERENCES dbo.UserSubjects(UserSubjectId, UserId),
        CONSTRAINT FK_Notes_Group FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId),
        CONSTRAINT CK_Notes_Title CHECK (LEN(LTRIM(RTRIM(Title))) > 0),
        CONSTRAINT CK_Notes_Format CHECK (BodyFormat IN ('Markdown','PlainText')),
        CONSTRAINT CK_Notes_Sharing CHECK ((Visibility IN ('Private','Public') AND StudyGroupId IS NULL) OR (Visibility = 'Group' AND StudyGroupId IS NOT NULL))
    );
    CREATE INDEX IX_Notes_Owner ON dbo.Notes(OwnerUserId, IsDeleted, UpdatedAtUtc DESC) INCLUDE (Title, UserSubjectId);
    CREATE INDEX IX_Notes_Group ON dbo.Notes(StudyGroupId, Visibility, IsDeleted);

    CREATE TABLE dbo.Documents (
        DocumentId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Documents PRIMARY KEY,
        OwnerUserId bigint NOT NULL,
        UserSubjectId bigint NULL,
        StudyGroupId bigint NULL,
        Title nvarchar(200) NOT NULL,
        Description nvarchar(2000) NULL,
        ResourceType varchar(10) NOT NULL,
        StorageKey nvarchar(512) NULL,
        ExternalUrl nvarchar(2048) NULL,
        OriginalFileName nvarchar(255) NULL,
        MimeType varchar(150) NULL,
        FileSizeBytes bigint NULL,
        Visibility varchar(10) NOT NULL CONSTRAINT DF_Documents_Visibility DEFAULT ('Private'),
        IsDeleted bit NOT NULL CONSTRAINT DF_Documents_Deleted DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Documents_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Documents_Updated DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_Documents_User FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_Documents_SubjectOwner FOREIGN KEY (UserSubjectId, OwnerUserId) REFERENCES dbo.UserSubjects(UserSubjectId, UserId),
        CONSTRAINT FK_Documents_Group FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId),
        CONSTRAINT CK_Documents_Title CHECK (LEN(LTRIM(RTRIM(Title))) > 0),
        CONSTRAINT CK_Documents_Resource CHECK (
            (ResourceType = 'File' AND StorageKey IS NOT NULL AND LEN(StorageKey) > 0
             AND ExternalUrl IS NULL AND OriginalFileName IS NOT NULL AND MimeType IS NOT NULL
             AND FileSizeBytes IS NOT NULL AND FileSizeBytes > 0)
            OR (ResourceType = 'Link' AND ExternalUrl IS NOT NULL AND LEN(ExternalUrl) > 0
             AND StorageKey IS NULL AND OriginalFileName IS NULL AND MimeType IS NULL AND FileSizeBytes IS NULL)),
        CONSTRAINT CK_Documents_Sharing CHECK ((Visibility IN ('Private','Public') AND StudyGroupId IS NULL) OR (Visibility = 'Group' AND StudyGroupId IS NOT NULL))
    );
    CREATE INDEX IX_Documents_Owner ON dbo.Documents(OwnerUserId, IsDeleted, CreatedAtUtc DESC);
    CREATE INDEX IX_Documents_Subject ON dbo.Documents(UserSubjectId, Visibility, IsDeleted) INCLUDE (Title);
    CREATE INDEX IX_Documents_Group ON dbo.Documents(StudyGroupId, Visibility, IsDeleted);

    CREATE TABLE dbo.DocumentBookmarks (
        UserId bigint NOT NULL,
        DocumentId bigint NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DocumentBookmarks_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DocumentBookmarks PRIMARY KEY (UserId, DocumentId),
        CONSTRAINT FK_DocumentBookmarks_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_DocumentBookmarks_Document FOREIGN KEY (DocumentId) REFERENCES dbo.Documents(DocumentId)
    );

    -- 5. FLASHCARDS. Card definition is shared; progress belongs to each learner.
    CREATE TABLE dbo.FlashcardDecks (
        DeckId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FlashcardDecks PRIMARY KEY,
        OwnerUserId bigint NOT NULL,
        UserSubjectId bigint NULL,
        StudyGroupId bigint NULL,
        Title nvarchar(200) NOT NULL,
        Description nvarchar(2000) NULL,
        Visibility varchar(10) NOT NULL CONSTRAINT DF_FlashcardDecks_Visibility DEFAULT ('Private'),
        IsDeleted bit NOT NULL CONSTRAINT DF_FlashcardDecks_Deleted DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FlashcardDecks_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FlashcardDecks_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_FlashcardDecks_User FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_FlashcardDecks_SubjectOwner FOREIGN KEY (UserSubjectId, OwnerUserId) REFERENCES dbo.UserSubjects(UserSubjectId, UserId),
        CONSTRAINT FK_FlashcardDecks_Group FOREIGN KEY (StudyGroupId) REFERENCES dbo.StudyGroups(StudyGroupId),
        CONSTRAINT CK_FlashcardDecks_Title CHECK (LEN(LTRIM(RTRIM(Title))) > 0),
        CONSTRAINT CK_FlashcardDecks_Sharing CHECK ((Visibility IN ('Private','Public') AND StudyGroupId IS NULL) OR (Visibility = 'Group' AND StudyGroupId IS NOT NULL))
    );
    CREATE INDEX IX_FlashcardDecks_Owner ON dbo.FlashcardDecks(OwnerUserId, IsDeleted);
    CREATE INDEX IX_FlashcardDecks_Group ON dbo.FlashcardDecks(StudyGroupId, Visibility, IsDeleted);

    CREATE TABLE dbo.Flashcards (
        FlashcardId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Flashcards PRIMARY KEY,
        DeckId bigint NOT NULL,
        FrontText nvarchar(4000) NOT NULL,
        BackText nvarchar(4000) NOT NULL,
        SortOrder int NOT NULL CONSTRAINT DF_Flashcards_Order DEFAULT (0),
        IsDeleted bit NOT NULL CONSTRAINT DF_Flashcards_Deleted DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Flashcards_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Flashcards_Updated DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_Flashcards_Deck FOREIGN KEY (DeckId) REFERENCES dbo.FlashcardDecks(DeckId),
        CONSTRAINT CK_Flashcards_Text CHECK (LEN(LTRIM(RTRIM(FrontText))) > 0 AND LEN(LTRIM(RTRIM(BackText))) > 0),
        CONSTRAINT CK_Flashcards_Order CHECK (SortOrder >= 0)
    );
    CREATE INDEX IX_Flashcards_Deck ON dbo.Flashcards(DeckId, IsDeleted, SortOrder);

    CREATE TABLE dbo.FlashcardProgress (
        UserId bigint NOT NULL,
        FlashcardId bigint NOT NULL,
        NextReviewAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FlashcardProgress_Next DEFAULT (SYSUTCDATETIME()),
        LastReviewedAtUtc datetime2(3) NULL,
        RepetitionCount int NOT NULL CONSTRAINT DF_FlashcardProgress_Repetitions DEFAULT (0),
        IntervalDays int NOT NULL CONSTRAINT DF_FlashcardProgress_Interval DEFAULT (0),
        EaseFactor decimal(4,2) NOT NULL CONSTRAINT DF_FlashcardProgress_Ease DEFAULT (2.50),
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_FlashcardProgress PRIMARY KEY (UserId, FlashcardId),
        CONSTRAINT FK_FlashcardProgress_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_FlashcardProgress_Card FOREIGN KEY (FlashcardId) REFERENCES dbo.Flashcards(FlashcardId),
        CONSTRAINT CK_FlashcardProgress_Values CHECK (RepetitionCount >= 0 AND IntervalDays >= 0 AND EaseFactor >= 1.30),
        CONSTRAINT CK_FlashcardProgress_Dates CHECK (LastReviewedAtUtc IS NULL OR NextReviewAtUtc >= LastReviewedAtUtc)
    );
    CREATE INDEX IX_FlashcardProgress_Due ON dbo.FlashcardProgress(UserId, NextReviewAtUtc);

    CREATE TABLE dbo.FlashcardReviews (
        ReviewId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FlashcardReviews PRIMARY KEY,
        RequestId uniqueidentifier NOT NULL CONSTRAINT UQ_FlashcardReviews_Request UNIQUE,
        UserId bigint NOT NULL,
        FlashcardId bigint NOT NULL,
        Rating tinyint NOT NULL,
        ResponseMilliseconds int NULL,
        ReviewedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FlashcardReviews_Reviewed DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_FlashcardReviews_Progress FOREIGN KEY (UserId, FlashcardId) REFERENCES dbo.FlashcardProgress(UserId, FlashcardId),
        CONSTRAINT CK_FlashcardReviews_Rating CHECK (Rating BETWEEN 1 AND 4), -- Again, Hard, Good, Easy
        CONSTRAINT CK_FlashcardReviews_Response CHECK (ResponseMilliseconds >= 0)
    );
    CREATE INDEX IX_FlashcardReviews_History ON dbo.FlashcardReviews(UserId, ReviewedAtUtc DESC);

    -- 6. PERSONAL CALENDAR. MVP recurrence: one-off or weekly on the same weekday.
    CREATE TABLE dbo.ScheduleEvents (
        ScheduleEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScheduleEvents PRIMARY KEY,
        UserId bigint NOT NULL,
        UserSubjectId bigint NULL,
        Title nvarchar(200) NOT NULL,
        EventType varchar(15) NOT NULL CONSTRAINT DF_ScheduleEvents_Type DEFAULT ('Class'),
        StartAtLocal datetime2(0) NOT NULL,
        EndAtLocal datetime2(0) NOT NULL,
        TimeZoneId nvarchar(100) NOT NULL CONSTRAINT DF_ScheduleEvents_Zone DEFAULT (N'SE Asia Standard Time'),
        RepeatMode varchar(10) NOT NULL CONSTRAINT DF_ScheduleEvents_Repeat DEFAULT ('None'),
        RepeatUntilDate date NULL,
        Location nvarchar(250) NULL,
        Description nvarchar(2000) NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ScheduleEvents_Deleted DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ScheduleEvents_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ScheduleEvents_Updated DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT UQ_ScheduleEvents_Owner UNIQUE (ScheduleEventId, UserId),
        CONSTRAINT FK_ScheduleEvents_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_ScheduleEvents_SubjectOwner FOREIGN KEY (UserSubjectId, UserId) REFERENCES dbo.UserSubjects(UserSubjectId, UserId),
        CONSTRAINT CK_ScheduleEvents_Type CHECK (EventType IN ('Class','SelfStudy','Exam','Other')),
        CONSTRAINT CK_ScheduleEvents_Dates CHECK (EndAtLocal > StartAtLocal AND DATEDIFF(day, StartAtLocal, EndAtLocal) <= 1),
        CONSTRAINT CK_ScheduleEvents_Repeat CHECK ((RepeatMode = 'None' AND RepeatUntilDate IS NULL) OR (RepeatMode = 'Weekly' AND RepeatUntilDate IS NOT NULL AND RepeatUntilDate >= CAST(StartAtLocal AS date)))
    );
    CREATE INDEX IX_ScheduleEvents_Calendar ON dbo.ScheduleEvents(UserId, IsDeleted, StartAtLocal);

    CREATE TABLE dbo.ScheduleExceptions (
        ScheduleEventId bigint NOT NULL,
        OccurrenceDate date NOT NULL,
        IsCancelled bit NOT NULL,
        NewStartAtLocal datetime2(0) NULL,
        NewEndAtLocal datetime2(0) NULL,
        NewLocation nvarchar(250) NULL,
        CONSTRAINT PK_ScheduleExceptions PRIMARY KEY (ScheduleEventId, OccurrenceDate),
        CONSTRAINT FK_ScheduleExceptions_Event FOREIGN KEY (ScheduleEventId) REFERENCES dbo.ScheduleEvents(ScheduleEventId),
        CONSTRAINT CK_ScheduleExceptions_Change CHECK (
            (IsCancelled = 1 AND NewStartAtLocal IS NULL AND NewEndAtLocal IS NULL)
            OR (IsCancelled = 0 AND NewStartAtLocal IS NOT NULL AND NewEndAtLocal IS NOT NULL AND NewEndAtLocal > NewStartAtLocal))
    );

    CREATE TABLE dbo.Deadlines (
        DeadlineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Deadlines PRIMARY KEY,
        UserId bigint NOT NULL,
        UserSubjectId bigint NULL,
        Title nvarchar(200) NOT NULL,
        Description nvarchar(2000) NULL,
        DueAtUtc datetime2(3) NOT NULL,
        Priority tinyint NOT NULL CONSTRAINT DF_Deadlines_Priority DEFAULT (2),
        Status varchar(15) NOT NULL CONSTRAINT DF_Deadlines_Status DEFAULT ('Pending'),
        CompletedAtUtc datetime2(3) NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_Deadlines_Deleted DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Deadlines_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Deadlines_Updated DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT UQ_Deadlines_Owner UNIQUE (DeadlineId, UserId),
        CONSTRAINT FK_Deadlines_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_Deadlines_SubjectOwner FOREIGN KEY (UserSubjectId, UserId) REFERENCES dbo.UserSubjects(UserSubjectId, UserId),
        CONSTRAINT CK_Deadlines_Priority CHECK (Priority BETWEEN 1 AND 3), -- 1 low, 3 high
        CONSTRAINT CK_Deadlines_Status CHECK (Status IN ('Pending','InProgress','Completed','Cancelled')),
        CONSTRAINT CK_Deadlines_Completed CHECK ((Status = 'Completed' AND CompletedAtUtc IS NOT NULL) OR (Status <> 'Completed' AND CompletedAtUtc IS NULL))
    );
    CREATE INDEX IX_Deadlines_Due ON dbo.Deadlines(UserId, IsDeleted, Status, DueAtUtc) INCLUDE (Title, Priority);

    CREATE TABLE dbo.Reminders (
        ReminderId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Reminders PRIMARY KEY,
        UserId bigint NOT NULL,
        DeadlineId bigint NULL,
        ScheduleEventId bigint NULL,
        OccurrenceDate date NULL,
        Channel varchar(10) NOT NULL CONSTRAINT DF_Reminders_Channel DEFAULT ('InApp'),
        RemindAtUtc datetime2(3) NOT NULL,
        Status varchar(15) NOT NULL CONSTRAINT DF_Reminders_Status DEFAULT ('Pending'),
        SentAtUtc datetime2(3) NULL,
        LastError nvarchar(1000) NULL,
        CONSTRAINT FK_Reminders_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_Reminders_DeadlineOwner FOREIGN KEY (DeadlineId, UserId) REFERENCES dbo.Deadlines(DeadlineId, UserId),
        CONSTRAINT FK_Reminders_EventOwner FOREIGN KEY (ScheduleEventId, UserId) REFERENCES dbo.ScheduleEvents(ScheduleEventId, UserId),
        CONSTRAINT CK_Reminders_Target CHECK ((DeadlineId IS NOT NULL AND ScheduleEventId IS NULL AND OccurrenceDate IS NULL) OR (DeadlineId IS NULL AND ScheduleEventId IS NOT NULL AND OccurrenceDate IS NOT NULL)),
        CONSTRAINT CK_Reminders_Channel CHECK (Channel IN ('InApp','Email')),
        CONSTRAINT CK_Reminders_Status CHECK (Status IN ('Pending','Processing','Sent','Failed','Cancelled')),
        CONSTRAINT CK_Reminders_Sent CHECK ((Status = 'Sent' AND SentAtUtc IS NOT NULL) OR (Status <> 'Sent' AND SentAtUtc IS NULL))
    );
    CREATE INDEX IX_Reminders_Queue ON dbo.Reminders(Status, RemindAtUtc) INCLUDE (UserId, Channel);
    CREATE UNIQUE INDEX UX_Reminders_Deadline ON dbo.Reminders(UserId, DeadlineId, Channel, RemindAtUtc) WHERE DeadlineId IS NOT NULL;
    CREATE UNIQUE INDEX UX_Reminders_Event ON dbo.Reminders(UserId, ScheduleEventId, OccurrenceDate, Channel, RemindAtUtc) WHERE ScheduleEventId IS NOT NULL;

    -- 7. GRADES / GOALS. Weights are per assessment, not category percentages.
    CREATE TABLE dbo.GradeEntries (
        GradeEntryId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GradeEntries PRIMARY KEY,
        UserSubjectId bigint NOT NULL,
        Title nvarchar(150) NOT NULL,
        AssessmentType varchar(20) NOT NULL CONSTRAINT DF_GradeEntries_Type DEFAULT ('Other'),
        Score decimal(8,2) NOT NULL,
        MaxScore decimal(8,2) NOT NULL CONSTRAINT DF_GradeEntries_Max DEFAULT (10),
        Weight decimal(6,2) NOT NULL CONSTRAINT DF_GradeEntries_Weight DEFAULT (1),
        AssessedOn date NOT NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_GradeEntries_Deleted DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GradeEntries_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_GradeEntries_Updated DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_GradeEntries_Subject FOREIGN KEY (UserSubjectId) REFERENCES dbo.UserSubjects(UserSubjectId),
        CONSTRAINT CK_GradeEntries_Score CHECK (MaxScore > 0 AND Score >= 0 AND Score <= MaxScore),
        CONSTRAINT CK_GradeEntries_Weight CHECK (Weight > 0),
        CONSTRAINT CK_GradeEntries_Type CHECK (AssessmentType IN ('Quiz','Assignment','Midterm','Final','Other'))
    );
    CREATE INDEX IX_GradeEntries_Subject ON dbo.GradeEntries(UserSubjectId, IsDeleted) INCLUDE (Score, MaxScore, Weight);

    CREATE TABLE dbo.Goals (
        GoalId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Goals PRIMARY KEY,
        UserId bigint NOT NULL,
        UserSubjectId bigint NULL,
        Title nvarchar(200) NOT NULL,
        UnitCode varchar(15) NOT NULL,
        TargetValue decimal(12,2) NOT NULL,
        CurrentValue decimal(12,2) NOT NULL CONSTRAINT DF_Goals_Current DEFAULT (0),
        StartDate date NOT NULL,
        EndDate date NOT NULL,
        IsCancelled bit NOT NULL CONSTRAINT DF_Goals_Cancelled DEFAULT (0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Goals_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Goals_Updated DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_Goals_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_Goals_SubjectOwner FOREIGN KEY (UserSubjectId, UserId) REFERENCES dbo.UserSubjects(UserSubjectId, UserId),
        CONSTRAINT CK_Goals_Unit CHECK (UnitCode IN ('Minutes','Sessions','Tasks','Cards','Score10')),
        CONSTRAINT CK_Goals_Values CHECK (TargetValue > 0 AND CurrentValue >= 0),
        CONSTRAINT CK_Goals_Score CHECK (UnitCode <> 'Score10' OR (TargetValue <= 10 AND CurrentValue <= 10)),
        CONSTRAINT CK_Goals_Dates CHECK (EndDate >= StartDate)
    );
    CREATE INDEX IX_Goals_User ON dbo.Goals(UserId, IsCancelled, EndDate);

    -- 8. FOCUS / ACTIVITY / STREAK. Logging in alone does NOT extend a streak.
    CREATE TABLE dbo.StudySessions (
        StudySessionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudySessions PRIMARY KEY,
        UserId bigint NOT NULL,
        UserSubjectId bigint NULL,
        StartedAtUtc datetime2(3) NOT NULL,
        EndedAtUtc datetime2(3) NULL,
        FocusSeconds int NOT NULL CONSTRAINT DF_StudySessions_Focus DEFAULT (0),
        Status varchar(15) NOT NULL CONSTRAINT DF_StudySessions_Status DEFAULT ('Running'),
        CONSTRAINT FK_StudySessions_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_StudySessions_SubjectOwner FOREIGN KEY (UserSubjectId, UserId) REFERENCES dbo.UserSubjects(UserSubjectId, UserId),
        CONSTRAINT CK_StudySessions_Status CHECK (Status IN ('Running','Completed','Cancelled')),
        CONSTRAINT CK_StudySessions_Focus CHECK (FocusSeconds >= 0),
        CONSTRAINT CK_StudySessions_End CHECK (
            (Status = 'Running' AND EndedAtUtc IS NULL)
            OR (Status IN ('Completed','Cancelled') AND EndedAtUtc IS NOT NULL
                AND EndedAtUtc >= StartedAtUtc AND FocusSeconds <= DATEDIFF_BIG(second, StartedAtUtc, EndedAtUtc))),
        CONSTRAINT CK_StudySessions_Completed CHECK (Status <> 'Completed' OR FocusSeconds > 0)
    );
    CREATE UNIQUE INDEX UX_StudySessions_Running ON dbo.StudySessions(UserId) WHERE Status = 'Running';
    CREATE INDEX IX_StudySessions_User ON dbo.StudySessions(UserId, StartedAtUtc DESC);

    CREATE TABLE dbo.ActivityEvents (
        ActivityEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ActivityEvents PRIMARY KEY,
        RequestId uniqueidentifier NOT NULL CONSTRAINT UQ_ActivityEvents_Request UNIQUE,
        UserId bigint NOT NULL,
        EventType varchar(25) NOT NULL,
        OccurredAtUtc datetime2(3) NOT NULL,
        ActivityDate date NOT NULL, -- User-local date, frozen when event is recorded.
        TimeZoneId nvarchar(100) NOT NULL,
        IsStudyAction AS CONVERT(bit, CASE WHEN EventType IN ('NoteSaved','FlashcardReviewed','FocusCompleted','DeadlineCompleted') THEN 1 ELSE 0 END) PERSISTED,
        CONSTRAINT FK_ActivityEvents_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_ActivityEvents_Type CHECK (EventType IN ('Login','NoteSaved','DocumentViewed','DocumentDownloaded','FlashcardReviewed','FocusCompleted','DeadlineCompleted','GroupJoined'))
    );
    CREATE INDEX IX_ActivityEvents_Streak ON dbo.ActivityEvents(UserId, ActivityDate, IsStudyAction);
    CREATE INDEX IX_ActivityEvents_Analytics ON dbo.ActivityEvents(OccurredAtUtc, UserId) INCLUDE (EventType);

    -- 9. ADMIN. Settings must never contain passwords, Google secrets or tokens.
    CREATE TABLE dbo.SystemSettings (
        SettingKey varchar(100) NOT NULL CONSTRAINT PK_SystemSettings PRIMARY KEY,
        SettingValue nvarchar(2000) NOT NULL,
        Description nvarchar(500) NULL,
        UpdatedByUserId bigint NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SystemSettings_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_SystemSettings_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.Users(UserId)
    );

    CREATE TABLE dbo.ContentReports (
        ReportId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ContentReports PRIMARY KEY,
        ReporterUserId bigint NOT NULL,
        NoteId bigint NULL,
        DocumentId bigint NULL,
        DeckId bigint NULL,
        GroupPostId bigint NULL,
        Reason nvarchar(1000) NOT NULL,
        Status varchar(15) NOT NULL CONSTRAINT DF_ContentReports_Status DEFAULT ('Open'),
        ResolvedByUserId bigint NULL,
        ResolutionNote nvarchar(1000) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ContentReports_Created DEFAULT (SYSUTCDATETIME()),
        ResolvedAtUtc datetime2(3) NULL,
        CONSTRAINT FK_ContentReports_Reporter FOREIGN KEY (ReporterUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_ContentReports_Note FOREIGN KEY (NoteId) REFERENCES dbo.Notes(NoteId),
        CONSTRAINT FK_ContentReports_Document FOREIGN KEY (DocumentId) REFERENCES dbo.Documents(DocumentId),
        CONSTRAINT FK_ContentReports_Deck FOREIGN KEY (DeckId) REFERENCES dbo.FlashcardDecks(DeckId),
        CONSTRAINT FK_ContentReports_Post FOREIGN KEY (GroupPostId) REFERENCES dbo.GroupPosts(GroupPostId),
        CONSTRAINT FK_ContentReports_Resolver FOREIGN KEY (ResolvedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_ContentReports_Target CHECK (
            CASE WHEN NoteId IS NULL THEN 0 ELSE 1 END + CASE WHEN DocumentId IS NULL THEN 0 ELSE 1 END
            + CASE WHEN DeckId IS NULL THEN 0 ELSE 1 END + CASE WHEN GroupPostId IS NULL THEN 0 ELSE 1 END = 1),
        CONSTRAINT CK_ContentReports_Status CHECK (Status IN ('Open','Resolved','Dismissed')),
        CONSTRAINT CK_ContentReports_Resolution CHECK (
            (Status = 'Open' AND ResolvedByUserId IS NULL AND ResolvedAtUtc IS NULL)
            OR (Status IN ('Resolved','Dismissed') AND ResolvedByUserId IS NOT NULL AND ResolvedAtUtc IS NOT NULL))
    );
    CREATE INDEX IX_ContentReports_Queue ON dbo.ContentReports(Status, CreatedAtUtc);

    CREATE TABLE dbo.AuditLogs (
        AuditLogId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
        ActorUserId bigint NULL,
        ActionCode varchar(100) NOT NULL,
        EntityType varchar(100) NOT NULL,
        EntityKey nvarchar(100) NOT NULL, -- Audit reference, deliberately not an FK.
        Summary nvarchar(1000) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_AuditLogs_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_AuditLogs_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.Users(UserId)
    );
    CREATE INDEX IX_AuditLogs_Date ON dbo.AuditLogs(CreatedAtUtc DESC);

    -- Dynamic batches allow views/procedures to be created within this transaction.
    EXEC(N'CREATE VIEW dbo.vw_SubjectGradeSummary AS
        SELECT us.UserSubjectId, us.UserId, us.AcademicTermId, us.SubjectId,
               s.SubjectName, us.CreditWeight, us.TargetScore10,
               COUNT(g.GradeEntryId) AS EnteredAssessmentCount,
               CAST(SUM((g.Score / NULLIF(g.MaxScore, 0)) * 10.0 * g.Weight)
                    / NULLIF(SUM(g.Weight), 0) AS decimal(5,2)) AS CurrentAverage10
        FROM dbo.UserSubjects AS us
        JOIN dbo.Subjects AS s ON s.SubjectId = us.SubjectId
        LEFT JOIN dbo.GradeEntries AS g ON g.UserSubjectId = us.UserSubjectId AND g.IsDeleted = 0
        GROUP BY us.UserSubjectId, us.UserId, us.AcademicTermId, us.SubjectId,
                 s.SubjectName, us.CreditWeight, us.TargetScore10;');

    EXEC(N'CREATE VIEW dbo.vw_TermGradeSummary AS
        SELECT UserId, AcademicTermId,
               COUNT(CurrentAverage10) AS SubjectsWithGrades,
               CAST(SUM(CurrentAverage10 * CreditWeight)
                 / NULLIF(SUM(CASE WHEN CurrentAverage10 IS NOT NULL THEN CreditWeight END), 0)
                 AS decimal(5,2)) AS CurrentAverage10
        FROM dbo.vw_SubjectGradeSummary
        GROUP BY UserId, AcademicTermId;');

    EXEC(N'CREATE VIEW dbo.vw_StudyStreaks AS
        WITH Days AS (
            SELECT DISTINCT UserId, ActivityDate FROM dbo.ActivityEvents WHERE IsStudyAction = 1
        ), Numbered AS (
            SELECT UserId, ActivityDate,
                   DATEADD(day, -CONVERT(int, ROW_NUMBER() OVER (PARTITION BY UserId ORDER BY ActivityDate)), ActivityDate) AS IslandKey
            FROM Days
        ), Islands AS (
            SELECT UserId, MIN(ActivityDate) AS FirstDay, MAX(ActivityDate) AS LastDay,
                   COUNT(*) AS StreakDays FROM Numbered GROUP BY UserId, IslandKey
        ), LocalToday AS (
            SELECT u.UserId,
                CONVERT(date, SYSUTCDATETIME() AT TIME ZONE ''UTC'' AT TIME ZONE COALESCE(s.TimeZoneId, N''SE Asia Standard Time'')) AS Today
            FROM dbo.Users AS u LEFT JOIN dbo.UserSettings AS s ON s.UserId = u.UserId
        )
        SELECT t.UserId, MAX(i.LastDay) AS LastStudyDate,
            COALESCE(MAX(i.StreakDays), 0) AS LongestStreakDays,
            COALESCE(MAX(CASE WHEN i.LastDay IN (t.Today, DATEADD(day, -1, t.Today)) THEN i.StreakDays ELSE 0 END), 0) AS CurrentStreakDays
        FROM LocalToday AS t LEFT JOIN Islands AS i ON i.UserId = t.UserId
        GROUP BY t.UserId;');

    EXEC(N'CREATE VIEW dbo.vw_DailyUsageUtc AS
        SELECT CAST(OccurredAtUtc AS date) AS ActivityDateUtc,
               COUNT_BIG(*) AS EventCount,
               COUNT(DISTINCT UserId) AS ActiveUsers,
               COUNT(DISTINCT CASE WHEN IsStudyAction = 1 THEN UserId END) AS ActiveLearners
        FROM dbo.ActivityEvents GROUP BY CAST(OccurredAtUtc AS date);');

    -- Call only from a trusted backend AFTER verifying the real business action.
    -- RequestId must be stable across retries. A replay does not add another day.
    EXEC(N'CREATE PROCEDURE dbo.usp_RecordActivity
        @UserId bigint, @EventType varchar(25), @RequestId uniqueidentifier
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        DECLARE @OwnTransaction bit = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
        DECLARE @Now datetime2(3) = SYSUTCDATETIME(), @Zone nvarchar(100);
        BEGIN TRY
            IF @OwnTransaction = 1 BEGIN TRANSACTION;
            SELECT @Zone = COALESCE(s.TimeZoneId, N''SE Asia Standard Time'')
            FROM dbo.Users AS u LEFT JOIN dbo.UserSettings AS s ON s.UserId = u.UserId
            WHERE u.UserId = @UserId AND u.Status = ''Active'';
            IF @Zone IS NULL THROW 51010, N''Active user not found.'', 1;
            IF NOT EXISTS (SELECT 1 FROM sys.time_zone_info WHERE name = @Zone)
                THROW 51011, N''Invalid SQL Server time zone.'', 1;
            IF EXISTS (SELECT 1 FROM dbo.ActivityEvents WITH (UPDLOCK, HOLDLOCK) WHERE RequestId = @RequestId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.ActivityEvents WHERE RequestId = @RequestId AND UserId = @UserId AND EventType = @EventType)
                    THROW 51012, N''RequestId already belongs to a different event.'', 1;
            END
            ELSE
                INSERT dbo.ActivityEvents(RequestId, UserId, EventType, OccurredAtUtc, ActivityDate, TimeZoneId)
                VALUES (@RequestId, @UserId, @EventType, @Now, CONVERT(date, @Now AT TIME ZONE ''UTC'' AT TIME ZONE @Zone), @Zone);
            IF @OwnTransaction = 1 COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @OwnTransaction = 1 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END;');

    EXEC(N'CREATE PROCEDURE dbo.usp_CreateStudyGroup
        @OwnerUserId bigint, @GroupName nvarchar(150), @SubjectId int = NULL,
        @Visibility varchar(10) = ''Private'', @MaxMembers smallint = 20,
        @StudyGroupId bigint = NULL OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        DECLARE @OwnTransaction bit = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
        BEGIN TRY
            IF @OwnTransaction = 1 BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @OwnerUserId AND Status = ''Active'')
                THROW 51020, N''Active owner not found.'', 1;
            INSERT dbo.StudyGroups(OwnerUserId, SubjectId, GroupName, Visibility, MaxMembers)
            VALUES (@OwnerUserId, @SubjectId, @GroupName, @Visibility, @MaxMembers);
            SET @StudyGroupId = CONVERT(bigint, SCOPE_IDENTITY());
            INSERT dbo.GroupMembers(StudyGroupId, UserId, MemberRole, Status, JoinedAtUtc)
            VALUES (@StudyGroupId, @OwnerUserId, ''Moderator'', ''Active'', SYSUTCDATETIME());
            IF @OwnTransaction = 1 COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @OwnTransaction = 1 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END;');

    INSERT dbo.SystemSettings(SettingKey, SettingValue, Description) VALUES
        ('SchemaVersion', N'1.0.0', N'Phiên bản schema; không sửa từ giao diện admin.'),
        ('AppName', N'Study Hub', N'Tên ứng dụng.'),
        ('MaxUploadBytes', N'20971520', N'Giới hạn đề xuất 20 MiB; backend phải kiểm tra.'),
        ('AllowedUploadExtensions', N'pdf,pptx,docx,png,jpg,jpeg', N'Danh sách gợi ý; kiểm tra cả MIME/chữ ký file.'),
        ('AllowPublicSharing', N'false', N'Mặc định không mở chia sẻ công khai; backend phải thực thi.');

    COMMIT TRANSACTION;
    PRINT N'Schema created successfully. Next: optional 02_SeedDemo.sql, then 03_ChecksAndQueries.sql.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
