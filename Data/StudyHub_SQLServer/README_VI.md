# Study Hub — Bộ script SQL Server

Thiết kế theo các tiện ích trong ảnh: Tổng quan; Ghi chú & Tài liệu; Flashcard; Lịch học cá nhân; Nhóm học tập; Điểm số & Mục tiêu; Cài đặt; Chuỗi ngày học. Bổ sung dữ liệu cho Google Login và quản trị cơ bản theo yêu cầu website.

Đây là schema độc lập cho database mới, không phải migration cho repository/`ApplicationDbContext` hiện có. Chưa đối chiếu entity hay database của bạn. Không chạy chồng lên database đang dùng.

## 1. Cách chạy

Dùng SQL Server **2019 trở lên**, bao gồm Express/LocalDB, và SQL Server Management Studio (SSMS). Các file có dấu tiếng Việt, lưu UTF-8.

1. Mở kết nối tới SQL Server trong SSMS.
2. Chạy `00_CreateDatabase.sql`: tạo `StudyHub` nếu chưa có. Cần quyền tạo database. Nếu không có quyền, nhờ quản trị viên tạo database rỗng; không tự nâng quyền.
3. Chạy toàn bộ `01_Schema.sql`: tạo bảng, khóa, index, view, procedure và cấu hình mặc định. Script dừng nếu đã tồn tại bảng người dùng.
4. Trên môi trường phát triển, chạy `02_SeedDemo.sql` để xóa dữ liệu nghiệp vụ hiện tại và tạo lại bộ dữ liệu mẫu THPT. Script giữ nguyên schema và `SystemSettings`; không chạy trên database có dữ liệu cần bảo toàn.
5. Chạy `03_ChecksAndQueries.sql` để kiểm tra ràng buộc và xem ví dụ truy vấn.
6. Trên database phát triển, chạy riêng `04_SmokeTests.sql`: 9 kiểm thử chức năng. Script rollback dữ liệu thử; số IDENTITY có thể vẫn bị nhảy, đây là bình thường.

Chạy từng file theo thứ tự; gặp lỗi thì dừng và đọc thông báo. Sao lưu trước khi chạy lại file 02 vì thao tác reset dữ liệu không thể hoàn tác sau khi transaction đã commit. Muốn đổi tên database, sửa tên trong file 00 và cả `USE`/`DB_NAME()` ở các file còn lại.

`GO` là dấu phân tách batch của công cụ như SSMS/sqlcmd. Không gửi cả file chứa `GO` vào một `SqlCommand` hay `ExecuteSqlRaw`. Nếu dùng ứng dụng để triển khai, cần công cụ hiểu batch hoặc chuyển schema thành migrations.

**Mức kiểm chứng:** đã kiểm tra tĩnh bằng bộ phân tích cú pháp T-SQL, bao gồm SQL tạo view/procedure nằm trong chuỗi động, và kiểm tra cấu trúc khóa ngoại. Môi trường tạo file không có SQL Server nên chưa thực thi schema hay xác nhận 9 smoke test chạy thành công trên engine thật. File 04 là bộ kiểm thử để nhóm chạy tại máy mình, không phải bằng chứng đã chạy.

## 2. Tiện ích tương ứng với bảng nào?

Schema gồm **27 bảng, 4 view, 2 stored procedure**.

| Phần giao diện | Bảng chính | Công dụng |
| --- | --- | --- |
| Google Login, hồ sơ | `Users`, `ExternalLogins`, `UserSettings` | Tài khoản, định danh Google, trường/lớp, tùy chọn cá nhân |
| Môn học, học kỳ | `Subjects`, `AcademicTerms`, `UserSubjects` | Danh mục môn và môn của từng người theo học kỳ |
| Ghi chú & Tài liệu | `Notes`, `Documents`, `DocumentBookmarks` | Ghi chú Markdown, file/link tài liệu, lưu yêu thích |
| Flashcard | `FlashcardDecks`, `Flashcards`, `FlashcardProgress`, `FlashcardReviews` | Bộ thẻ, nội dung, tiến độ và lịch sử ôn riêng từng người |
| Lịch học cá nhân | `ScheduleEvents`, `ScheduleExceptions`, `Deadlines`, `Reminders` | Lịch một lần/hằng tuần, đổi/hủy buổi, hạn nộp, nhắc lịch |
| Nhóm học tập | `StudyGroups`, `GroupMembers`, `GroupPosts` | Nhóm, yêu cầu tham gia/thành viên, bài thảo luận |
| Điểm số & Mục tiêu | `GradeEntries`, `Goals` | Điểm theo thang và hệ số; mục tiêu cá nhân |
| Tổng quan & chuỗi ngày học | `StudySessions`, `ActivityEvents` | Phiên học tập trung, sự kiện sử dụng, cơ sở tính streak |
| Cài đặt & quản trị | `SystemSettings`, `ContentReports`, `AuditLogs` | Cấu hình ứng dụng, báo cáo nội dung, lịch sử quản trị |

Không tạo bảng riêng cho Dashboard: tổng hợp từ dữ liệu nghiệp vụ. Không thêm marketplace, thanh toán, subscription hay chat thời gian thực vì không nằm trong các tiện ích hiện tại.

## 3. Quan hệ quan trọng

- Một `User` có nhiều `AcademicTerms`; mỗi học kỳ chứa các `UserSubjects`.
- `Subjects` là danh mục dùng chung. `UserSubjects` là việc **một người học môn đó trong một học kỳ**. Học lại ở học kỳ khác là một dòng mới, không ghi đè điểm cũ.
- `Notes`, `Documents`, `FlashcardDecks`, `ScheduleEvents`, `Deadlines`, `Goals`, `StudySessions` có thể gắn với `UserSubjectId`; để NULL nếu không thuộc môn cụ thể.
- Khóa ngoại ghép `(UserSubjectId, UserId/OwnerUserId)` ngăn dữ liệu cá nhân tham chiếu môn của người khác. Khóa ghép tương tự bảo vệ học kỳ và mục tiêu nhắc lịch.
- `GradeEntries` thuộc `UserSubjects`; chủ sở hữu được xác định qua môn, không lặp `UserId` không cần thiết.
- Nhóm có một `OwnerUserId`; `GroupMembers` dùng khóa `(StudyGroupId, UserId)`. Người tạo nhóm cũng phải có membership Active.
- Mỗi deck có nhiều card. Tiến độ dùng khóa `(UserId, FlashcardId)` vì nhiều người học cùng thẻ nhưng ngày ôn tiếp theo khác nhau.
- Một reminder chỉ trỏ tới một deadline hoặc một buổi lịch. Một content report chỉ trỏ tới một trong bốn loại nội dung được hỗ trợ.

`Users` chỉ lưu tên trường/lớp dạng hồ sơ trong MVP. Không có danh mục trường được xác thực hay tích hợp hệ thống nhà trường. `EducationLevel` hỗ trợ `HighSchool`, `University`, `Other`; ảnh THPT không làm schema bị giới hạn ở lớp 11.

## 4. Google Login phải triển khai thế nào?

`ExternalLogins` lưu `(Provider = Google, ProviderSubject = sub, UserId)`. `sub` là định danh của tài khoản Google; không dùng email để tự động nối tài khoản. Cột subject dùng collation phân biệt hoa/thường. Không lưu password, access token, refresh token hay Google client secret trong schema này.

Luồng backend:

1. Nhận kết quả OpenID Connect, xác minh chữ ký và các claim `iss`, `aud`, `exp` bằng thư viện phù hợp; xử lý state/nonce theo luồng.
2. Tra `ExternalLogins` bằng provider và `sub` đã xác minh.
3. Nếu chưa tồn tại, tạo `Users`, `UserSettings`, `ExternalLogins` trong một transaction. Bắt lỗi unique khi hai yêu cầu đăng nhập đầu tiên chạy đồng thời, rồi truy vấn lại.
4. Chỉ cấp session cho tài khoản `Active`. Lấy `UserId`/role từ phiên đã xác thực ở server, không tin `UserId` hoặc `RoleCode` do client gửi.
5. Dữ liệu demo không có external login nên không đăng nhập được bằng Google. Không tự liên kết Google thật vào admin demo.

Nếu dự án đang dùng ASP.NET Core Identity: **không dùng đồng thời hai hệ tài khoản độc lập**. Có thể thay `Users` bằng `AspNetUsers` và `ExternalLogins` bằng `AspNetUserLogins`, rồi đổi toàn bộ kiểu/FK `UserId` theo khóa Identity thực tế. Bản này mặc định `bigint` — không phải mặc định khóa chuỗi của Identity.

Tham khảo: [Google OpenID Connect — định danh `sub` và xác minh ID token](https://developers.google.com/identity/openid-connect/openid-connect).

## 5. Quy tắc dữ liệu cần biết khi viết backend

### Phân quyền và tài liệu

- `Private`: chỉ chủ sở hữu. `Group`: chủ sở hữu và thành viên Active của nhóm chưa lưu trữ. `Public`: người dùng được hệ thống cho phép đọc nội dung công khai.
- Mặc định nội dung Private, nhóm Private, `AllowPublicSharing=false`. Backend phải chặn việc bật Public khi cấu hình này tắt; tắt cấu hình không tự thu hồi nội dung Public đã tồn tại. Nếu muốn thu hồi, cần thao tác quản trị riêng.
- Trước khi chia sẻ vào nhóm, kiểm tra người đăng là thành viên Active. Khóa ngoại chỉ kiểm tra nhóm tồn tại, không thay thế chính sách này.
- Owner được xác định duy nhất từ `StudyGroups.OwnerUserId`, không từ `MemberRole`. `Moderator` là quyền điều phối, không đồng nghĩa chủ nhóm.
- Tạo nhóm qua `usp_CreateStudyGroup` hoặc transaction tương đương. Duyệt thành viên phải khóa dòng nhóm trong transaction và kiểm tra `MaxMembers`; DB chưa tự enforce sức chứa. Không cho owner rời nhóm nếu chưa chuyển quyền hoặc lưu trữ nhóm.
- `GroupPosts` có FK membership, nhưng backend vẫn cần kiểm tra membership đang Active. Thành viên rời nhóm không làm mất lịch sử bài đăng.
- `Documents` chỉ lưu metadata. File thật nằm ở nơi lưu trữ riêng; `StorageKey` là khóa nội bộ, không phải URL tải công khai. Kiểm tra quyền mỗi lần tải và tạo URL ngắn hạn nếu cần.
- Bookmark và lịch sử flashcard không tự cấp quyền truy cập khi chủ sở hữu đổi nội dung sang Private. Truy vấn mẫu đã kiểm tra quyền chia sẻ hiện tại.
- Giới hạn upload, loại file, URL an toàn và quét nội dung cần xử lý ở backend. Không render Markdown/URL thành HTML không được làm sạch.
- Admin chỉ được truy cập các API quản trị được phân quyền; có cột role không tự bảo vệ database. Không cấp kết nối SQL trực tiếp cho trình duyệt/mobile.

### Điểm số

`GradeEntries` dùng **hệ số trên từng đầu điểm**:

```text
Điểm hiện tại /10 = SUM((Score / MaxScore) × 10 × Weight) / SUM(Weight)
```

Ví dụ: 8/10 hệ số 1, 85/100 hệ số 2, 8.5/10 hệ số 2 → **8.40/10**. Chưa có điểm trả NULL, không coi là 0. Điểm môn đã xóa mềm không tính vào trung bình.

`vw_TermGradeSummary` tính trung bình các môn đã có điểm theo `CreditWeight`; mặc định 1 cho trường hợp không dùng tín chỉ. Đây là điểm tạm tính từ phần đã nhập, **không phải GPA chính thức hay điểm cuối kỳ**. Không tự quy đổi thang 4. Nếu trường dùng tỷ trọng theo nhóm đầu điểm (ví dụ tổng quiz 20% bất kể số bài), cần thêm bảng nhóm đánh giá và quy tắc riêng; không gán 20% cho từng quiz.

`Goals` là mục tiêu có tiến độ cập nhật qua backend/người dùng. DB không tự đồng bộ `CurrentValue` từ flashcard, điểm hay phiên học; cần chọn và triển khai một nguồn cập nhật nhất quán. Có thể vượt chỉ tiêu, phần trăm hiển thị chặn ở 100%.

### Lịch và thông báo

- `ScheduleEvents`: lịch một lần hoặc lặp hằng tuần vào cùng thứ/giờ; bản đầu chưa hỗ trợ RRULE tùy ý. `RepeatUntilDate` bao gồm ngày kết thúc nếu đúng ngày lặp.
- Lưu giờ lịch bằng `StartAtLocal`/`EndAtLocal` cùng `TimeZoneId`. Sinh từng buổi trong phạm vi được yêu cầu, áp dụng `ScheduleExceptions`, sau đó chuyển từng buổi sang UTC.
- `ScheduleExceptions.OccurrenceDate` là ngày gốc của buổi, kể cả khi dời sang ngày khác. Backend kiểm tra ngày ngoại lệ thật sự thuộc chuỗi và kiểm tra trường hợp giờ không tồn tại/trùng do DST nếu hỗ trợ ngoài Việt Nam.
- `TimeZoneId` dùng tên SQL Server/Windows như `SE Asia Standard Time`; kiểm tra tên với `sys.time_zone_info`. Không truyền thẳng tên IANA như `Asia/Ho_Chi_Minh` nếu chưa chuyển đổi.
- `DueAtUtc`, `RemindAtUtc`, `CreatedAtUtc` dùng UTC. SQL không tự gửi email/push: cần background worker đọc hàng đợi.
- Mỗi buổi lặp có reminder riêng. Worker nhận việc nguyên tử, xử lý retry/timeout cho trạng thái Processing và tránh gửi trùng; gửi thành công cập nhật `Status=Sent`, `SentAtUtc`.
- Khi sửa/hủy deadline hoặc buổi học, backend cập nhật/hủy reminder tương ứng trong transaction. Database không tự làm điều này.

Tham khảo: [Microsoft — AT TIME ZONE](https://learn.microsoft.com/en-us/sql/t-sql/queries/at-time-zone-transact-sql).

### Streak và thống kê

- Hoạt động tính streak: `NoteSaved`, `FlashcardReviewed`, `FocusCompleted`, `DeadlineCompleted`. Login hay mở tài liệu đơn thuần không tính.
- Backend chỉ gọi `usp_RecordActivity` sau hành động học hợp lệ, không cho client tự khai báo đã học. Với cập nhật nghiệp vụ, gọi trong cùng transaction và rollback toàn bộ nếu có lỗi.
- `RequestId` ổn định theo một hành động giúp retry không tạo thêm sự kiện. Một ngày có nhiều sự kiện vẫn chỉ tính một ngày học.
- Ngày hoạt động được chốt theo múi giờ tại lúc ghi. Đổi múi giờ không viết lại lịch sử; nên hạn chế đổi thường xuyên nếu streak là chỉ số quan trọng.
- Streak hiện tại vẫn được giữ nếu lần học cuối là hôm qua; nếu đã bỏ trọn một ngày thì về 0. Longest streak giữ kỷ lục cũ.
- `vw_DailyUsageUtc` đo người dùng có event theo ngày UTC. Không phải toàn bộ lượt truy cập website, không suy ra retention nếu chưa định nghĩa cohort. API cần ghi đủ các sự kiện đã chọn; nên đặt chính sách lưu giữ event/audit phù hợp.

### Xóa và cập nhật

- Các FK mặc định `NO ACTION`; không có chuỗi xóa cascade. Dùng `IsDeleted`, `IsArchived`, `Status` theo từng bảng. Đây không phải xóa vĩnh viễn dữ liệu cá nhân.
- Khóa tài khoản phải làm backend từ chối session; xóa tài khoản thật cần quy trình thu hồi và dọn/ẩn danh dữ liệu liên quan, xử lý file thật riêng.
- Backend cập nhật `UpdatedAtUtc` khi sửa. Cột `RowVersion` hỗ trợ phát hiện hai người/thiết bị sửa cùng lúc, không phải thời gian cập nhật.
- `SystemSettings` lưu chuỗi cấu hình, backend phải kiểm tra kiểu và giá trị. Không lưu bí mật trong bảng cấu hình/audit.
- `ContentReports` đảm bảo đối tượng tồn tại; backend kiểm tra người báo cáo được xem đối tượng và người xử lý có role Admin. Ghi `AuditLogs` cùng transaction với xử lý quản trị.

Tham khảo: [Microsoft — CREATE TABLE, CHECK và FOREIGN KEY](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-table-transact-sql).

## 6. Chia việc cho nhóm 5 người

| Thành viên | Phạm vi database/API |
| --- | --- |
| 1 | Google Login, hồ sơ, môn học/học kỳ, phân quyền dùng chung |
| 2 | Notes, Documents, Bookmarks và kiểm duyệt nội dung |
| 3 | Flashcard decks/cards, thuật toán ôn, progress/reviews |
| 4 | Calendar, exceptions, deadlines, reminder worker |
| 5 | Nhóm học, điểm/mục tiêu, dashboard/streak, cấu hình quản trị |

Mọi người thống nhất schema/FK trước khi tạo entity. Nếu dùng EF Core: map rõ các composite FK và `DeleteBehavior.NoAction`, dùng `decimal` cho điểm/hệ số, `DateTime` cho `datetime2`, cấu hình `IsRowVersion()` cho rowversion. Chọn database-first hoặc migrations làm nguồn sự thật, tránh vừa sửa SQL vừa chạy migration độc lập.

Website responsive được thực hiện ở frontend; database này không quyết định bố cục theo thiết bị.
