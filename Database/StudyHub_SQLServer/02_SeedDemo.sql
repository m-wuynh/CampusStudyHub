/* DỮ LIỆU MẪU THPT.
   Xóa toàn bộ dữ liệu nghiệp vụ hiện có và tạo lại dữ liệu mẫu theo chương trình THPT.
   Giữ nguyên schema, view, stored procedure và SystemSettings.
   Chỉ chạy trên môi trường phát triển/kiểm thử.
*/
USE [StudyHub];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
    IF DB_NAME() <> N'StudyHub' THROW 51100, N'Sai database. Cần sử dụng StudyHub.', 1;
    IF OBJECT_ID(N'dbo.ActivityEvents', N'U') IS NULL THROW 51101, N'Hãy chạy 01_Schema.sql trước.', 1;

    BEGIN TRANSACTION;

    /* Xóa theo thứ tự phụ thuộc khóa ngoại. */
    DELETE FROM dbo.ContentReports;
    DELETE FROM dbo.AuditLogs;
    DELETE FROM dbo.Reminders;
    DELETE FROM dbo.ScheduleExceptions;
    DELETE FROM dbo.FlashcardReviews;
    DELETE FROM dbo.FlashcardProgress;
    DELETE FROM dbo.DocumentBookmarks;
    DELETE FROM dbo.ActivityEvents;
    DELETE FROM dbo.StudySessions;
    DELETE FROM dbo.Goals;
    DELETE FROM dbo.GradeEntries;
    DELETE FROM dbo.GroupPosts;
    DELETE FROM dbo.Notes;
    DELETE FROM dbo.Documents;
    DELETE FROM dbo.Flashcards;
    DELETE FROM dbo.FlashcardDecks;
    DELETE FROM dbo.Deadlines;
    DELETE FROM dbo.ScheduleEvents;
    DELETE FROM dbo.GroupMembers;
    DELETE FROM dbo.StudyGroups;
    DELETE FROM dbo.UserSubjects;
    DELETE FROM dbo.AcademicTerms;
    DELETE FROM dbo.ExternalLogins;
    DELETE FROM dbo.UserSettings;
    UPDATE dbo.SystemSettings SET UpdatedByUserId = NULL WHERE UpdatedByUserId IS NOT NULL;
    DELETE FROM dbo.Users;
    DELETE FROM dbo.Subjects;

    /* UserId = 1 được giữ ổn định cho tài khoản demo hiện tại của ứng dụng. */
    DBCC CHECKIDENT ('dbo.Users', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Subjects', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.AcademicTerms', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.UserSubjects', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.StudyGroups', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.GroupPosts', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Notes', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Documents', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.FlashcardDecks', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Flashcards', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.FlashcardReviews', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.ScheduleEvents', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Deadlines', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Reminders', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.GradeEntries', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Goals', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.StudySessions', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.ActivityEvents', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.ContentReports', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.AuditLogs', RESEED, 0) WITH NO_INFOMSGS;

    DECLARE @Now datetime2(3) = SYSUTCDATETIME();
    DECLARE @Today date = CONVERT(date, @Now AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time');

    INSERT dbo.Users(DisplayName, Email, EducationLevel, SchoolName, ClassName, StudentCode)
    VALUES
        (N'Nguyễn Minh Anh', N'minhanh.thpt@example.invalid', 'HighSchool', N'THPT Nguyễn Du', N'11A1', N'HS11001'),
        (N'Trần Bảo An', N'baoan.thpt@example.invalid', 'HighSchool', N'THPT Nguyễn Du', N'11A1', N'HS11002'),
        (N'Lê Khánh Linh', N'khanhlinh.thpt@example.invalid', 'HighSchool', N'THPT Nguyễn Du', N'11A2', N'HS11003'),
        (N'Phạm Gia Huy', N'giahuy.thpt@example.invalid', 'HighSchool', N'THPT Nguyễn Du', N'12A1', N'HS12001');
    INSERT dbo.Users(DisplayName, Email, EducationLevel, RoleCode)
    VALUES (N'Quản trị viên Study Hub', N'admin@example.invalid', 'Other', 'Admin');

    DECLARE @MinhAnh bigint = (SELECT UserId FROM dbo.Users WHERE StudentCode = N'HS11001');
    DECLARE @BaoAn bigint = (SELECT UserId FROM dbo.Users WHERE StudentCode = N'HS11002');
    DECLARE @KhanhLinh bigint = (SELECT UserId FROM dbo.Users WHERE StudentCode = N'HS11003');
    DECLARE @GiaHuy bigint = (SELECT UserId FROM dbo.Users WHERE StudentCode = N'HS12001');
    DECLARE @Admin bigint = (SELECT UserId FROM dbo.Users WHERE RoleCode = 'Admin');

    INSERT dbo.UserSettings(UserId, DailyStudyGoalMinutes)
    VALUES (@MinhAnh, 90), (@BaoAn, 60), (@KhanhLinh, 75), (@GiaHuy, 120), (@Admin, 30);

    INSERT dbo.Subjects(SubjectCode, SubjectName, Description) VALUES
        ('THPT-TOAN', N'Toán', N'Hàm số, đại số, hình học, xác suất và thống kê trong chương trình THPT.'),
        ('THPT-NV', N'Ngữ văn', N'Đọc hiểu, nghị luận xã hội, nghị luận văn học và tiếng Việt.'),
        ('THPT-TA', N'Tiếng Anh', N'Từ vựng, ngữ pháp và bốn kỹ năng theo chương trình THPT.'),
        ('THPT-VL', N'Vật lý', N'Dao động, sóng, điện, từ, quang học và vật lý hiện đại.'),
        ('THPT-HH', N'Hóa học', N'Cấu tạo chất, phản ứng hóa học, hóa vô cơ và hóa hữu cơ.'),
        ('THPT-SH', N'Sinh học', N'Trao đổi chất, di truyền, tiến hóa, sinh thái và môi trường.'),
        ('THPT-LS', N'Lịch sử', N'Lịch sử Việt Nam và thế giới trong chương trình THPT.'),
        ('THPT-DL', N'Địa lý', N'Địa lý tự nhiên, dân cư, kinh tế Việt Nam và kỹ năng Atlat.'),
        ('THPT-TH', N'Tin học', N'Tư duy máy tính, dữ liệu, mạng máy tính và lập trình cơ bản.'),
        ('THPT-GDKTPL', N'Giáo dục kinh tế và pháp luật', N'Kiến thức kinh tế, pháp luật và trách nhiệm công dân.');

    DECLARE @Toan int = (SELECT SubjectId FROM dbo.Subjects WHERE SubjectCode = 'THPT-TOAN');
    DECLARE @NguVan int = (SELECT SubjectId FROM dbo.Subjects WHERE SubjectCode = 'THPT-NV');
    DECLARE @TiengAnh int = (SELECT SubjectId FROM dbo.Subjects WHERE SubjectCode = 'THPT-TA');
    DECLARE @VatLy int = (SELECT SubjectId FROM dbo.Subjects WHERE SubjectCode = 'THPT-VL');
    DECLARE @HoaHoc int = (SELECT SubjectId FROM dbo.Subjects WHERE SubjectCode = 'THPT-HH');
    DECLARE @SinhHoc int = (SELECT SubjectId FROM dbo.Subjects WHERE SubjectCode = 'THPT-SH');
    DECLARE @LichSu int = (SELECT SubjectId FROM dbo.Subjects WHERE SubjectCode = 'THPT-LS');
    DECLARE @DiaLy int = (SELECT SubjectId FROM dbo.Subjects WHERE SubjectCode = 'THPT-DL');
    DECLARE @TinHoc int = (SELECT SubjectId FROM dbo.Subjects WHERE SubjectCode = 'THPT-TH');
    DECLARE @GdKtpl int = (SELECT SubjectId FROM dbo.Subjects WHERE SubjectCode = 'THPT-GDKTPL');

    INSERT dbo.AcademicTerms(UserId, TermName, StartDate, EndDate) VALUES
        (@MinhAnh, N'Học kỳ I - Năm học 2026-2027', '2026-09-05', '2027-01-15'),
        (@BaoAn, N'Học kỳ I - Năm học 2026-2027', '2026-09-05', '2027-01-15'),
        (@KhanhLinh, N'Học kỳ I - Năm học 2026-2027', '2026-09-05', '2027-01-15'),
        (@GiaHuy, N'Học kỳ I - Năm học 2026-2027', '2026-09-05', '2027-01-15');

    DECLARE @TermMinhAnh bigint = (SELECT AcademicTermId FROM dbo.AcademicTerms WHERE UserId = @MinhAnh);
    DECLARE @TermBaoAn bigint = (SELECT AcademicTermId FROM dbo.AcademicTerms WHERE UserId = @BaoAn);
    DECLARE @TermKhanhLinh bigint = (SELECT AcademicTermId FROM dbo.AcademicTerms WHERE UserId = @KhanhLinh);
    DECLARE @TermGiaHuy bigint = (SELECT AcademicTermId FROM dbo.AcademicTerms WHERE UserId = @GiaHuy);

    INSERT dbo.UserSubjects(UserId, AcademicTermId, SubjectId, TeacherName, ClassCode, TargetScore10, ColorHex) VALUES
        (@MinhAnh, @TermMinhAnh, @Toan, N'Cô Nguyễn Thu Hà', N'11A1-TOAN', 9.0, '#4F46E5'),
        (@MinhAnh, @TermMinhAnh, @NguVan, N'Cô Trần Hoài An', N'11A1-NV', 8.0, '#EC4899'),
        (@MinhAnh, @TermMinhAnh, @TiengAnh, N'Thầy Lê Minh Quân', N'11A1-TA', 8.5, '#F59E0B'),
        (@MinhAnh, @TermMinhAnh, @VatLy, N'Thầy Phạm Quốc Việt', N'11A1-VL', 8.5, '#0EA5E9'),
        (@MinhAnh, @TermMinhAnh, @HoaHoc, N'Cô Đỗ Thanh Mai', N'11A1-HH', 8.5, '#10B981'),
        (@MinhAnh, @TermMinhAnh, @SinhHoc, N'Cô Vũ Ngọc Lan', N'11A1-SH', 8.0, '#22C55E'),
        (@MinhAnh, @TermMinhAnh, @LichSu, N'Thầy Nguyễn Đức Long', N'11A1-LS', 8.0, '#F97316'),
        (@MinhAnh, @TermMinhAnh, @DiaLy, N'Cô Trần Hải Yến', N'11A1-DL', 8.0, '#14B8A6'),
        (@MinhAnh, @TermMinhAnh, @TinHoc, N'Thầy Hoàng Tuấn', N'11A1-TH', 9.0, '#6366F1'),
        (@MinhAnh, @TermMinhAnh, @GdKtpl, N'Cô Phạm Thu Hương', N'11A1-GDKTPL', 8.5, '#8B5CF6'),
        (@BaoAn, @TermBaoAn, @Toan, N'Cô Nguyễn Thu Hà', N'11A1-TOAN', 8.0, '#4F46E5'),
        (@BaoAn, @TermBaoAn, @NguVan, N'Cô Trần Hoài An', N'11A1-NV', 8.5, '#EC4899'),
        (@KhanhLinh, @TermKhanhLinh, @TiengAnh, N'Thầy Lê Minh Quân', N'11A2-TA', 9.0, '#F59E0B'),
        (@KhanhLinh, @TermKhanhLinh, @SinhHoc, N'Cô Vũ Ngọc Lan', N'11A2-SH', 8.5, '#22C55E'),
        (@GiaHuy, @TermGiaHuy, @LichSu, N'Thầy Nguyễn Đức Long', N'12A1-LS', 9.0, '#F97316'),
        (@GiaHuy, @TermGiaHuy, @DiaLy, N'Cô Trần Hải Yến', N'12A1-DL', 9.0, '#14B8A6');

    DECLARE @UsToan bigint = (SELECT UserSubjectId FROM dbo.UserSubjects WHERE UserId = @MinhAnh AND SubjectId = @Toan);
    DECLARE @UsNguVan bigint = (SELECT UserSubjectId FROM dbo.UserSubjects WHERE UserId = @MinhAnh AND SubjectId = @NguVan);
    DECLARE @UsTiengAnh bigint = (SELECT UserSubjectId FROM dbo.UserSubjects WHERE UserId = @MinhAnh AND SubjectId = @TiengAnh);
    DECLARE @UsVatLy bigint = (SELECT UserSubjectId FROM dbo.UserSubjects WHERE UserId = @MinhAnh AND SubjectId = @VatLy);
    DECLARE @UsHoaHoc bigint = (SELECT UserSubjectId FROM dbo.UserSubjects WHERE UserId = @MinhAnh AND SubjectId = @HoaHoc);
    DECLARE @UsSinhHoc bigint = (SELECT UserSubjectId FROM dbo.UserSubjects WHERE UserId = @MinhAnh AND SubjectId = @SinhHoc);

    DECLARE @GroupToan bigint, @GroupVan bigint, @GroupAnh bigint, @GroupLy bigint,
            @GroupHoa bigint, @GroupSinh bigint, @GroupSu bigint, @GroupDia bigint;

    EXEC dbo.usp_CreateStudyGroup @MinhAnh, N'Toán 11 - Hàm số lượng giác', @Toan, 'Public', 25, @GroupToan OUTPUT;
    EXEC dbo.usp_CreateStudyGroup @BaoAn, N'Ngữ văn 11 - Nghị luận văn học', @NguVan, 'Public', 20, @GroupVan OUTPUT;
    EXEC dbo.usp_CreateStudyGroup @KhanhLinh, N'Tiếng Anh 11 - Global Success', @TiengAnh, 'Public', 30, @GroupAnh OUTPUT;
    EXEC dbo.usp_CreateStudyGroup @MinhAnh, N'Vật lý 11 - Dao động và sóng', @VatLy, 'Private', 15, @GroupLy OUTPUT;
    EXEC dbo.usp_CreateStudyGroup @BaoAn, N'Hóa học 11 - Cân bằng và hydrocarbon', @HoaHoc, 'Public', 25, @GroupHoa OUTPUT;
    EXEC dbo.usp_CreateStudyGroup @KhanhLinh, N'Sinh học 11 - Trao đổi chất', @SinhHoc, 'Public', 25, @GroupSinh OUTPUT;
    EXEC dbo.usp_CreateStudyGroup @GiaHuy, N'Lịch sử 12 - Ôn thi tốt nghiệp THPT', @LichSu, 'Public', 40, @GroupSu OUTPUT;
    EXEC dbo.usp_CreateStudyGroup @GiaHuy, N'Địa lý 12 - Kỹ năng Atlat', @DiaLy, 'Public', 40, @GroupDia OUTPUT;

    INSERT dbo.GroupMembers(StudyGroupId, UserId, Status, JoinedAtUtc) VALUES
        (@GroupToan, @BaoAn, 'Active', @Now), (@GroupToan, @KhanhLinh, 'Active', @Now),
        (@GroupVan, @MinhAnh, 'Active', @Now), (@GroupVan, @KhanhLinh, 'Active', @Now),
        (@GroupAnh, @MinhAnh, 'Active', @Now),
        (@GroupLy, @BaoAn, 'Active', @Now),
        (@GroupHoa, @MinhAnh, 'Active', @Now),
        (@GroupSu, @MinhAnh, 'Pending', NULL);

    UPDATE dbo.StudyGroups SET Description = CASE StudyGroupId
        WHEN @GroupToan THEN N'Cùng luyện công thức lượng giác, phương trình lượng giác và bài tập vận dụng lớp 11.'
        WHEN @GroupVan THEN N'Phân tích tác phẩm, lập dàn ý và luyện viết bài nghị luận văn học.'
        WHEN @GroupAnh THEN N'Luyện từ vựng, ngữ pháp, nghe và nói theo các unit Tiếng Anh 11.'
        WHEN @GroupLy THEN N'Nhóm nội bộ lớp 11A1 luyện bài tập dao động điều hòa và sóng cơ.'
        WHEN @GroupHoa THEN N'Hệ thống kiến thức cân bằng hóa học, đại cương hữu cơ và hydrocarbon.'
        WHEN @GroupSinh THEN N'Ôn trao đổi nước, khoáng, quang hợp, hô hấp và cảm ứng ở sinh vật.'
        WHEN @GroupSu THEN N'Hệ thống chuyên đề Lịch sử Việt Nam và thế giới phục vụ kỳ thi tốt nghiệp.'
        WHEN @GroupDia THEN N'Luyện đọc Atlat, nhận xét biểu đồ và phân tích các vùng kinh tế Việt Nam.'
    END;

    INSERT dbo.GroupPosts(StudyGroupId, AuthorUserId, Body, IsPinned) VALUES
        (@GroupToan, @MinhAnh, N'Tuần này nhóm ôn công thức lượng giác và phương trình lượng giác cơ bản.', 1),
        (@GroupToan, @BaoAn, N'Mình đã tổng hợp 20 bài tập theo ba mức độ, tối nay sẽ chia sẻ với nhóm.', 0),
        (@GroupVan, @BaoAn, N'Mọi người chuẩn bị dàn ý phân tích hình tượng nhân vật trước buổi học nhóm nhé.', 1),
        (@GroupAnh, @KhanhLinh, N'Buổi tới luyện speaking theo chủ đề A long and healthy life.', 1),
        (@GroupLy, @MinhAnh, N'Ôn kỹ đồ thị li độ, vận tốc và gia tốc trong dao động điều hòa.', 1),
        (@GroupHoa, @BaoAn, N'Chúng ta sẽ chữa bài về nguyên lý chuyển dịch cân bằng Le Chatelier.', 1),
        (@GroupSu, @GiaHuy, N'Đã cập nhật sơ đồ thời gian các giai đoạn lịch sử Việt Nam hiện đại.', 1),
        (@GroupDia, @GiaHuy, N'Mang Atlat Địa lý Việt Nam để luyện xác định vùng kinh tế.', 1);

    INSERT dbo.Notes(OwnerUserId, UserSubjectId, StudyGroupId, Title, Body, Visibility, IsPinned) VALUES
        (@MinhAnh, @UsToan, @GroupToan, N'Công thức lượng giác cần nhớ', N'# Công thức trọng tâm' + NCHAR(10) + N'- sin²x + cos²x = 1' + NCHAR(10) + N'- sin(a ± b), cos(a ± b)' + NCHAR(10) + N'- Công thức biến đổi tổng thành tích', 'Group', 1),
        (@MinhAnh, @UsNguVan, NULL, N'Dàn ý bài nghị luận văn học', N'Mở bài giới thiệu vấn đề; thân bài phân tích dẫn chứng; kết bài đánh giá giá trị nội dung và nghệ thuật.', 'Private', 0),
        (@MinhAnh, @UsVatLy, NULL, N'Dao động điều hòa', N'x = A cos(ωt + φ); vận tốc sớm pha π/2 so với li độ; gia tốc ngược pha với li độ.', 'Public', 1),
        (@MinhAnh, @UsHoaHoc, @GroupHoa, N'Cân bằng hóa học', N'Khi thay đổi nồng độ, áp suất hoặc nhiệt độ, cân bằng chuyển dịch theo chiều làm giảm tác động.', 'Group', 0),
        (@MinhAnh, @UsSinhHoc, NULL, N'Quang hợp ở thực vật', N'Pha sáng tạo ATP, NADPH; chu trình Calvin sử dụng sản phẩm pha sáng để cố định CO₂.', 'Private', 0);

    DECLARE @TaiLieuToan bigint;
    INSERT dbo.Documents(OwnerUserId, UserSubjectId, StudyGroupId, Title, Description, ResourceType, ExternalUrl, Visibility)
    VALUES (@MinhAnh, @UsToan, @GroupToan, N'Học liệu môn Toán THPT', N'Cổng học liệu tham khảo phục vụ ôn tập chương trình phổ thông.', 'Link', N'https://hanhtrangso.nxbgd.vn/', 'Group');
    SET @TaiLieuToan = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.Documents(OwnerUserId, UserSubjectId, Title, Description, ResourceType, ExternalUrl, Visibility) VALUES
        (@MinhAnh, @UsNguVan, N'Thư viện bài đọc Ngữ văn', N'Nguồn tham khảo để luyện kỹ năng đọc hiểu.', 'Link', N'https://hanhtrangso.nxbgd.vn/', 'Private'),
        (@MinhAnh, @UsTiengAnh, N'Từ điển học tập tiếng Anh', N'Tra cứu từ vựng và cách phát âm.', 'Link', N'https://dictionary.cambridge.org/', 'Public');
    INSERT dbo.DocumentBookmarks(UserId, DocumentId) VALUES (@BaoAn, @TaiLieuToan);

    DECLARE @DeckToan bigint, @DeckAnh bigint;
    INSERT dbo.FlashcardDecks(OwnerUserId, UserSubjectId, StudyGroupId, Title, Description, Visibility)
    VALUES (@MinhAnh, @UsToan, @GroupToan, N'Công thức lượng giác lớp 11', N'Ôn nhanh các công thức và giá trị lượng giác.', 'Group');
    SET @DeckToan = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.FlashcardDecks(OwnerUserId, UserSubjectId, Title, Description, Visibility)
    VALUES (@MinhAnh, @UsTiengAnh, N'Từ vựng Tiếng Anh 11', N'Từ vựng theo các chủ đề trong học kỳ I.', 'Private');
    SET @DeckAnh = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.Flashcards(DeckId, FrontText, BackText, SortOrder) VALUES
        (@DeckToan, N'Giá trị của sin²x + cos²x?', N'Bằng 1 với mọi x.', 1),
        (@DeckToan, N'Công thức cos(a + b)?', N'cos a cos b − sin a sin b.', 2),
        (@DeckToan, N'Chu kỳ của hàm số y = sin x?', N'2π.', 3),
        (@DeckAnh, N'sustainable', N'bền vững', 1),
        (@DeckAnh, N'life expectancy', N'tuổi thọ trung bình', 2),
        (@DeckAnh, N'generational conflict', N'xung đột thế hệ', 3);

    DECLARE @Card1 bigint = (SELECT FlashcardId FROM dbo.Flashcards WHERE DeckId = @DeckToan AND SortOrder = 1);
    DECLARE @Card2 bigint = (SELECT FlashcardId FROM dbo.Flashcards WHERE DeckId = @DeckToan AND SortOrder = 2);
    INSERT dbo.FlashcardProgress(UserId, FlashcardId, NextReviewAtUtc, LastReviewedAtUtc, RepetitionCount, IntervalDays) VALUES
        (@MinhAnh, @Card1, DATEADD(day, 2, @Now), @Now, 2, 2),
        (@MinhAnh, @Card2, DATEADD(day, 1, @Now), @Now, 1, 1);
    INSERT dbo.FlashcardReviews(RequestId, UserId, FlashcardId, Rating, ResponseMilliseconds, ReviewedAtUtc) VALUES
        (NEWID(), @MinhAnh, @Card1, 4, 3200, @Now),
        (NEWID(), @MinhAnh, @Card2, 3, 5100, @Now);

    DECLARE @Tomorrow datetime2(0) = DATEADD(day, 1, CONVERT(datetime2(0), @Today));
    DECLARE @EventToan bigint, @EventAnh bigint;
    INSERT dbo.ScheduleEvents(UserId, UserSubjectId, Title, StartAtLocal, EndAtLocal, RepeatMode, RepeatUntilDate, Location)
    VALUES (@MinhAnh, @UsToan, N'Học thêm Toán', DATEADD(hour, 17, @Tomorrow), DATEADD(minute, 90, DATEADD(hour, 17, @Tomorrow)), 'Weekly', DATEADD(day, 90, @Today), N'Phòng 204');
    SET @EventToan = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.ScheduleEvents(UserId, UserSubjectId, Title, EventType, StartAtLocal, EndAtLocal, RepeatMode, RepeatUntilDate, Location)
    VALUES (@MinhAnh, @UsTiengAnh, N'Luyện speaking Tiếng Anh', 'SelfStudy', DATEADD(hour, 19, @Tomorrow), DATEADD(hour, 20, @Tomorrow), 'Weekly', DATEADD(day, 90, @Today), N'Thư viện');
    SET @EventAnh = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.ScheduleExceptions(ScheduleEventId, OccurrenceDate, IsCancelled)
    VALUES (@EventToan, DATEADD(day, 8, @Today), 1);

    DECLARE @DeadlineToan bigint, @DeadlineVan bigint;
    INSERT dbo.Deadlines(UserId, UserSubjectId, Title, Description, DueAtUtc, Priority) VALUES
        (@MinhAnh, @UsToan, N'Hoàn thành bài tập phương trình lượng giác', N'Làm bài 1 đến bài 15 trong phiếu ôn tập.', DATEADD(day, 3, @Now), 3),
        (@MinhAnh, @UsNguVan, N'Nộp bài nghị luận văn học', N'Viết bài hoàn chỉnh khoảng 600 chữ.', DATEADD(day, 5, @Now), 2);
    SELECT @DeadlineToan = MIN(DeadlineId), @DeadlineVan = MAX(DeadlineId) FROM dbo.Deadlines WHERE UserId = @MinhAnh;
    INSERT dbo.Reminders(UserId, DeadlineId, RemindAtUtc) VALUES
        (@MinhAnh, @DeadlineToan, DATEADD(day, 2, @Now)),
        (@MinhAnh, @DeadlineVan, DATEADD(day, 4, @Now));
    INSERT dbo.Reminders(UserId, ScheduleEventId, OccurrenceDate, RemindAtUtc)
    VALUES (@MinhAnh, @EventAnh, CAST(DATEADD(day, 1, @Today) AS date), DATEADD(hour, 11, @Now));

    INSERT dbo.GradeEntries(UserSubjectId, Title, AssessmentType, Score, MaxScore, Weight, AssessedOn) VALUES
        (@UsToan, N'Kiểm tra thường xuyên 1', 'Quiz', 8.5, 10, 1, DATEADD(day, -14, @Today)),
        (@UsToan, N'Kiểm tra giữa kỳ', 'Midterm', 8.8, 10, 2, DATEADD(day, -3, @Today)),
        (@UsNguVan, N'Bài viết số 1', 'Assignment', 8.0, 10, 1, DATEADD(day, -12, @Today)),
        (@UsNguVan, N'Kiểm tra giữa kỳ', 'Midterm', 8.2, 10, 2, DATEADD(day, -2, @Today)),
        (@UsTiengAnh, N'Vocabulary Unit 1-2', 'Quiz', 9.0, 10, 1, DATEADD(day, -10, @Today)),
        (@UsVatLy, N'Bài tập dao động điều hòa', 'Assignment', 8.5, 10, 1, DATEADD(day, -7, @Today)),
        (@UsHoaHoc, N'Kiểm tra cân bằng hóa học', 'Quiz', 8.0, 10, 1, DATEADD(day, -5, @Today));

    INSERT dbo.Goals(UserId, UserSubjectId, Title, UnitCode, TargetValue, CurrentValue, StartDate, EndDate) VALUES
        (@MinhAnh, @UsToan, N'Luyện Toán 300 phút trong tuần', 'Minutes', 300, 120, @Today, DATEADD(day, 6, @Today)),
        (@MinhAnh, @UsTiengAnh, N'Ôn 60 thẻ từ vựng', 'Cards', 60, 18, @Today, DATEADD(day, 13, @Today)),
        (@MinhAnh, @UsNguVan, N'Hoàn thành 3 bài luyện viết', 'Tasks', 3, 1, @Today, DATEADD(day, 20, @Today));

    INSERT dbo.StudySessions(UserId, UserSubjectId, StartedAtUtc, EndedAtUtc, FocusSeconds, Status) VALUES
        (@MinhAnh, @UsToan, DATEADD(minute, -50, @Now), @Now, 2700, 'Completed'),
        (@MinhAnh, @UsTiengAnh, DATEADD(day, -1, DATEADD(minute, -30, @Now)), DATEADD(day, -1, @Now), 1500, 'Completed'),
        (@BaoAn, NULL, DATEADD(day, -1, DATEADD(minute, -40, @Now)), DATEADD(day, -1, @Now), 2100, 'Completed');

    INSERT dbo.ActivityEvents(RequestId, UserId, EventType, OccurredAtUtc, ActivityDate, TimeZoneId)
    SELECT NEWID(), @MinhAnh, v.EventType, DATEADD(day, -v.OffsetDays, @Now), DATEADD(day, -v.OffsetDays, @Today), N'SE Asia Standard Time'
    FROM (VALUES (0, 'FocusCompleted'), (1, 'FlashcardReviewed'), (2, 'NoteSaved'), (3, 'FocusCompleted'), (4, 'NoteSaved')) v(OffsetDays, EventType);
    INSERT dbo.ActivityEvents(RequestId, UserId, EventType, OccurredAtUtc, ActivityDate, TimeZoneId)
    VALUES (NEWID(), @BaoAn, 'GroupJoined', @Now, @Today, N'SE Asia Standard Time');

    INSERT dbo.AuditLogs(ActorUserId, ActionCode, EntityType, EntityKey, Summary)
    VALUES (@Admin, 'HighSchoolSeeded', 'System', N'THPT-2026-2027', N'Khởi tạo dữ liệu mẫu theo chương trình THPT năm học 2026-2027.');

    COMMIT TRANSACTION;

    SELECT UserId, DisplayName, ClassName FROM dbo.Users WHERE RoleCode = 'Student' ORDER BY UserId;
    SELECT SubjectId, SubjectCode, SubjectName FROM dbo.Subjects ORDER BY SubjectId;
    SELECT StudyGroupId, GroupName, Visibility FROM dbo.StudyGroups ORDER BY StudyGroupId;
    PRINT N'Đã xóa dữ liệu demo cũ và tạo dữ liệu mẫu THPT thành công.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
