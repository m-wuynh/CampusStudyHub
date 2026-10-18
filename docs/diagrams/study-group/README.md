# Sơ đồ Study Group

Đối chiếu với mã nguồn hiện có ngày 12/10/2026, bao gồm quản lý nhóm và năm phần học tập đã triển khai.

Mở **[index.html](index.html)** bằng trình duyệt để xem toàn bộ sơ đồ, phóng to/thu nhỏ và tải ảnh. Trang xem chứa sẵn SVG, hoạt động không cần mạng.

| Loại | Nội dung | SVG | PNG | Mermaid |
|---|---|---|---|---|
| Use case | Tác nhân, quyền chung, quyền quản lý và quyền riêng của Owner | [SVG](01-usecase.svg) | [PNG](01-usecase.png) | [Nguồn](01-usecase.mmd) |
| Screenflow | Trang, tab, form/hộp thoại và điều hướng | [SVG](02-screenflow.svg) | [PNG](02-screenflow.png) | [Nguồn](02-screenflow.mmd) |
| Swimlane | Tham gia nhóm: Open, Approval, lời mời | [SVG](03-swimlane-join.svg) | [PNG](03-swimlane-join.png) | [Nguồn](03-swimlane-join.mmd) |
| Swimlane | Học nhóm, giao/nộp bài, phản hồi và tiến độ | [SVG](04-swimlane-learning.svg) | [PNG](04-swimlane-learning.png) | [Nguồn](04-swimlane-learning.mmd) |
| Swimlane | Chia sẻ, đọc và thu hồi tài nguyên | [SVG](05-swimlane-resources.svg) | [PNG](05-swimlane-resources.png) | [Nguồn](05-swimlane-resources.mmd) |

SVG giữ chữ và đường nét khi phóng to, phù hợp chèn vào báo cáo. PNG dùng được trong công cụ không hỗ trợ SVG. Screenflow nhiều nhánh nên nên xem ngang hoặc mở SVG để đọc rõ.

## Cách đọc và giới hạn phạm vi

- Use case dùng actor hình người, oval cho chức năng và khung ranh giới hệ thống. Mũi tên tam giác rỗng hướng tới vai trò tổng quát. “Người quản lý” là vai trò chung cho Owner và Moderator. Một oval có thể gom các thao tác cùng mục tiêu để giữ sơ đồ tổng quan dễ đọc.
- Quan hệ kế thừa quyền không bỏ qua điều kiện nghiệp vụ: Owner không được rời nhóm; chỉ người được giao mới nộp bài; chỉ chủ sở hữu được chia sẻ tài nguyên; gỡ cấm phải có yêu cầu và đúng người có quyền xử lý. Moderator không quản lý Owner hoặc Moderator khác.
- Đăng nhập là điều kiện tiên quyết của use case trong phân hệ, nên không nối tất cả chức năng tới một use case đăng nhập.
- Screenflow: xanh dương là trang; xanh lá là tab trong trang nhóm; cam là form/hộp thoại; xám là trạng thái hoặc thao tác tại chỗ. Các tab mới cùng nằm trong `/Groups/{id}`. Lưu thành công cập nhật dữ liệu và đóng hộp thoại về tab hiện tại; hủy đóng hộp thoại. Lỗi nhập liệu giữ form để người dùng sửa.
- Người ngoài chỉ xem trước nhóm khi đủ quyền xem. Nhóm riêng tư không tự xuất hiện cho người ngoài; đường dẫn trực tiếp có thể trả 404. Link mời có trang riêng để kiểm tra và chấp nhận. Những nhánh lỗi không làm thay đổi màn hình được lược bớt trong screenflow tổng quan.
- Swimlane đọc từ trên xuống. Mỗi cột là một bên chịu trách nhiệm. Nét đứt thể hiện hoạt động liên quan hoặc vòng quay lại; không hàm ý tự gửi email/thông báo hay tự tải lại màn hình.
- Luồng học/nộp bài là kịch bản sử dụng kết hợp. Bài tập có thể tạo độc lập; tham dự/điểm danh/kết thúc buổi học không phải điều kiện nộp bài. Nhánh Cần sửa quay lại kiểm tra hạn nộp, trạng thái Open và phiên bản trước khi nhận bài mới.
- Tiến độ được tính khi đọc dữ liệu theo tuần UTC+7; hệ thống không ghi một “điểm tiến độ” riêng sau mỗi lần nộp. Tổng hợp dựa trên buổi Completed và phiên bản bài nộp mới nhất.
- Khi rời nhóm/kick/ban, tài nguyên người đó chia sẻ trở về Private; việc thu hồi áp dụng cho những request đọc tiếp theo. Hệ thống giữ nội dung và chủ sở hữu.
- Chuyển Owner, lưu trữ nhóm qua giao diện, họp video tích hợp và upload tệp bài nộp riêng chưa có luồng giao diện tương ứng nên không xuất hiện trong sơ đồ.

## Mã nguồn và tái xuất

Các file `.mmd` mô tả nội dung bằng Mermaid và có thể sửa bằng công cụ hỗ trợ Mermaid. SVG use case và swimlane dùng bố cục cố định để giữ actor/oval và các cột trách nhiệm dễ đọc; mã dựng bố cục nằm trong [render/VectorDiagrams.cs](render/VectorDiagrams.cs). Mermaid cho cùng nội dung nhưng bố cục tự động khác ảnh xuất.

Chạy từ thư mục gốc dự án:

```powershell
dotnet run --project docs/diagrams/study-group/render/DiagramRenderer.csproj -- docs/diagrams/study-group
```

Cần .NET 9 và Microsoft Edge. Lần dựng đầu cần mạng để tải gói Playwright và Mermaid 11.4.1; các file HTML/SVG/PNG đã xuất xem được offline. Nếu sửa nội dung use case/swimlane, cập nhật cả `.mmd` và `VectorDiagrams.cs`; screenflow được dựng trực tiếp từ `.mmd`.

## Đối chiếu mã nguồn

- `StudyHub.Web/Views/StudyGroups/Index.cshtml`, `Details.cshtml`, `_LearningPanels.cshtml`, `Resource.cshtml`.
- `StudyHub.Web/wwwroot/js/group-details.js`, `group-learning.js`.
- `StudyHub.Web/Controllers/StudyGroups/StudyGroupsApiController.cs`, `GroupLearningApiController.cs`, `GroupResourcesController.cs`.
- `StudyHub.DAL/Repositories/StudyGroups/SqlStudyGroupRepository.cs`, `SqlGroupLearningRepository.cs`, `SqlGroupLearningRepository.Reads.cs`.
- [Quy tắc nghiệp vụ đầy đủ](../../../GROUP_LEARNING.md).
