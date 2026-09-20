
# Campus Study Hub

Ứng dụng hỗ trợ quản lý ghi chú, flashcards, lịch học, điểm số và nhóm học tập. Repo gồm bản React/Vite và ứng dụng ASP.NET Core Razor Pages trong `StudyHub.Web`.

## Kiến trúc ASP.NET Core 3 layer

```text
StudyHub.Web       Presentation: Razor Pages, JavaScript, Minimal API, DI
       │
       ├──────────────► StudyHub.Business
       │                Business: entity, DTO, interface, nghiệp vụ
       │                       ▲
       ▼                       │
StudyHub.Data ─────────────────┘
Data Access: Repository đọc/ghi JSON
```

Chạy bản ASP.NET Core:

```powershell
dotnet run --project StudyHub.Web/StudyHub.Web.csproj --launch-profile https
```

## Feature nhóm học tập

- Tạo, sửa và xóa nhóm; cấu hình môn học, mục tiêu, sức chứa, hình thức, lịch và liên kết liên hệ.
- Tìm kiếm, lọc theo môn; xem nhóm đã tham gia, khám phá nhóm công khai và lưu nhóm yêu thích.
- Sinh viên gửi hoặc hủy yêu cầu tham gia; trưởng nhóm duyệt hoặc từ chối; kiểm soát nhóm đã đủ người.
- Nội dung bảng tin, thành viên, ghi chú và flashcards chỉ hiển thị cho thành viên.
- Thành viên đăng thông báo, chia sẻ ghi chú/flashcards và rời nhóm.
- Chat box riêng cho từng nhóm: chỉ thành viên được đọc/gửi, hỗ trợ Enter để gửi, Shift + Enter để xuống dòng, tự cuộn tới tin mới và đồng bộ giữa các tab cùng trình duyệt.
- Dữ liệu nhóm và tin nhắn được lưu trong `localStorage` (khóa `study-hub-groups-v3`) để không mất khi tải lại trang. Đây là tầng demo frontend; khi nối backend cần thay `localStorage` bằng API, database và SignalR/WebSocket để nhiều thiết bị nhận tin nhắn theo thời gian thực.


## Run Locally

**Prerequisites:**  Node.js


1. Install dependencies:
   `npm install`
2. Nếu dùng tính năng AI, đặt `GEMINI_API_KEY` trong `.env.local`.
3. Chạy ứng dụng:
   `npm run dev`
