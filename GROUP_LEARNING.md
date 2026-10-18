# Study Group: hoạt động học nhóm

## Cập nhật database và chạy

Migration mới: `20261011182331_AddGroupLearningWorkspace`. Migration bổ sung tám bảng cho buổi học, điểm danh, bài tập, người nhận, lịch sử bài nộp, câu hỏi, trả lời và mục tiêu tuần. Không thay đổi nội dung các bảng học tập cá nhân.

Chạy từ thư mục gốc với database phát triển đã được tạo theo hướng dẫn README:

```powershell
dotnet ef database update --project StudyHub.DAL --startup-project StudyHub.Web
dotnet run --project StudyHub.Web --launch-profile https
```

Migration ban đầu của dự án là baseline cho database có sẵn; `database update` không tự tạo đầy đủ schema gốc trên một database rỗng.

Mở `/Groups/{groupId}` bằng tài khoản thành viên Active. Các tab mới gồm **Buổi học**, **Bài tập**, **Hỏi đáp**, **Tiến độ**; tab **Tài nguyên** hỗ trợ chia sẻ và đọc trực tiếp.

## Quy tắc và phạm vi

- **Buổi học:** Owner/Moderator tạo và sửa lịch trước khi bắt đầu. Bắt đầu phải ở tương lai, kết thúc sau bắt đầu, tối đa 24 giờ. Thành viên đăng ký/hủy trước giờ bắt đầu. Người quản lý điểm danh sau giờ bắt đầu, có thể bổ sung người tham dự không đăng ký trước. Kết thúc phải có tổng kết; hủy có thể ghi lý do. Buổi đã kết thúc/hủy không sửa lịch. Có thể hiệu chỉnh điểm danh sau buổi học.
- **Bài tập:** Owner/Moderator giao cho các thành viên Active được chọn, hoặc toàn bộ thành viên tại thời điểm tạo. Người vào sau không tự nhận bài. Danh sách người nhận giữ nguyên khi sửa bài tập. Bài có thể liên kết một buổi học cùng nhóm. Lời giải bằng văn bản hoặc liên kết HTTP/HTTPS; chưa có upload tệp bài nộp riêng. Có lựa chọn nhận bài muộn; đóng bài tập sẽ chặn mọi lần nộp mới.
- **Phản hồi:** chỉ người được giao nộp được bài của mình. Mỗi lần nộp tạo phiên bản mới; giữ cả nội dung và nhận xét các lần trước. Chỉ Owner/Moderator đánh giá phiên bản mới nhất thành `Completed` hoặc `NeedsRevision`. Yêu cầu sửa phải có nhận xét. Bài hoàn thành không được nộp lại cho đến khi người quản lý yêu cầu sửa; vẫn phải đáp ứng trạng thái mở và hạn nộp. Thành viên chỉ xem bài làm của mình, quản lý xem mọi bài nộp.
- **Tài nguyên:** chia sẻ Note, PDF thuộc module Notes, tài liệu dạng link và Flashcard Deck sẵn có. Chỉ chủ sở hữu được chia sẻ. Chủ sở hữu hoặc quản lý nhóm được gỡ chia sẻ. Mỗi tài nguyên thuộc tối đa một nhóm; chia sẻ tài nguyên Public sẽ đổi sang Group. Gỡ chia sẻ, rời nhóm, kick hoặc ban sẽ đưa tài nguyên về Private. Nội dung tài nguyên là bản hiện hành, không tạo bản sao khi chia sẻ. Người đọc phải là thành viên Active. Màn hình chung chỉ đọc, ghi chú/notebook hiển thị văn bản an toàn; PDF có endpoint kiểm tra quyền riêng, flashcard mở từng đáp án. Các trang chỉnh sửa cá nhân tiếp tục kiểm tra chủ sở hữu.
- **Tiến độ:** tuần bắt đầu thứ Hai theo UTC+7. Buổi học tính theo giờ bắt đầu và trạng thái Completed. Bài tập tính theo hạn nộp; số lượt hoàn thành dựa vào trạng thái phiên bản nộp mới nhất của từng người. Mục tiêu “bài tập cả nhóm hoàn thành” chỉ tăng khi tất cả người được giao đã hoàn thành. Bài giao vẫn giữ lịch sử người đã rời nhóm, nên họ còn nằm trong tổng nhóm; bảng thành viên chỉ liệt kê người Active. Thành viên xem tổng nhóm và tiến độ cá nhân, quản lý xem tiến độ từng thành viên. Các tỷ lệ được tính từ dữ liệu hiện hành, không phải báo cáo chốt bất biến theo tuần.
- **Hỏi đáp:** thành viên Active đặt câu hỏi theo môn/chủ đề, trả lời và tìm kiếm. Người hỏi hoặc quản lý được chọn/bỏ chọn câu trả lời; câu được chọn bắt buộc thuộc đúng câu hỏi. Câu đã giải quyết vẫn có thể nhận thảo luận bổ sung.

Hệ thống lưu thời gian UTC; form và lịch hiển thị theo giờ Việt Nam. Thao tác sửa lịch, bài tập, mục tiêu, chọn lời giải và nộp lại bài có kiểm tra phiên bản để phát hiện dữ liệu cũ. Các thay đổi kiểm tra quyền và ghi dữ liệu trong cùng transaction. Đăng ký buổi học tương lai được bỏ khi người dùng rời nhóm/kick/ban, lịch sử điểm danh được giữ lại.

## Cấu trúc code

| Lớp | File chính |
|---|---|
| Entity và cấu hình EF | `StudyHub.DAL/Entities/GroupLearning.cs`, `StudyHub.DAL/Persistence/StudyHubDbContext.GroupLearning.cs` |
| Truy vấn, phân quyền và transaction | `StudyHub.DAL/Repositories/StudyGroups/SqlGroupLearningRepository*.cs` |
| Kiểm tra đầu vào, ánh xạ lỗi | `StudyHub.BLL/Services/StudyGroups/GroupLearningService.cs` |
| Request DTO | `StudyHub.BLL/DTOs/GroupLearningRequests.cs` |
| REST API | `StudyHub.Web/Controllers/StudyGroups/GroupLearningApiController.cs` |
| Đọc tài nguyên | `GroupResourcesController.cs`, `Views/StudyGroups/Resource.cshtml` |
| Giao diện | `Views/StudyGroups/_LearningPanels.cshtml`, `wwwroot/js/group-learning.js`, `wwwroot/css/group-learning.css` |

API dưới `/api/groups/{groupId}/learning`; các thao tác thay đổi yêu cầu cookie đăng nhập và header `RequestVerificationToken`. ID buổi học, bài tập, câu hỏi đều được kiểm tra thuộc đúng nhóm. Endpoint đọc tài nguyên: `/Groups/{groupId}/Resources/{Note|Document|Flashcard}/{id}`.

Đợt này tải dữ liệu hoạt động nhóm trong một request, không có phân trang. Với nhóm có lịch sử lớn, bước tiếp theo là phân trang từng tab và chuyển thống kê tuần sang truy vấn tổng hợp SQL. Chat tiếp tục cập nhật qua SignalR; các tab học tập có nút Làm mới và tự tải lại sau khi lưu. Không tự gửi email, tạo link họp hay tích hợp lịch bên ngoài.

## Kiểm thử

```powershell
dotnet test StudyHub.Tests/StudyHub.Tests.csproj
```

Mặc định cần SQL Server `localhost\SQLEXPRESS` hỗ trợ Windows Authentication và trình duyệt Microsoft Edge. Có thể cấu hình server kiểm thử khác bằng biến môi trường `STUDYHUB_TEST_SQL`; tên database trong chuỗi sẽ được thay bằng tên ngẫu nhiên `StudyHub_GroupLearningTests_<guid>`. Bộ test tạo rồi xóa đúng database thử nghiệm đó; không dùng connection string phát triển, không chạy dịch vụ gửi email.

Các test API chạy migration mới trên schema đã có, kiểm tra quyền/antiforgery, ID khác nhóm, xung đột phiên bản, đăng ký/điểm danh, hạn nộp, lịch sử phản hồi, nội dung riêng tư và tính tiến độ theo UTC+7. Test trình duyệt dùng Edge headless để thao tác các form như người dùng và phát hiện lỗi JavaScript.

Nếu máy chưa có Edge, có thể chạy các test API:

```powershell
dotnet test StudyHub.Tests/StudyHub.Tests.csproj --filter FullyQualifiedName~GroupLearningTests
```
