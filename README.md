
# Campus Study Hub

Ứng dụng hỗ trợ quản lý ghi chú, flashcards, lịch học, điểm số và nhóm học tập. Repo gồm bản React/Vite và ứng dụng ASP.NET Core Razor Pages trong `StudyHub.Web`.

## Kiến trúc ASP.NET Core 3 layer

```text
StudyHub.Web       Presentation: Razor Pages, JavaScript, Minimal API, DI
       │
       ├──────────────► StudyHub.BLL
       │                Business: entity, DTO, interface, nghiệp vụ
       │                       ▲
       ▼                       │
StudyHub.DAL ───────────────────┘
Data Access: EF Core Database First, Repository, SQL Server
```

- Database: SQL Server Express `StudyHub`.
- Connection string: `StudyHub.Web/appsettings.json` → `ConnectionStrings:StudyHub`.
- Entity và `StudyHubDbContext` được scaffold từ 27 bảng và 4 view trong database.
- `SqlStudyGroupRepository` ánh xạ bảng `StudyGroups`, `GroupMembers`, `GroupPosts` sang model của BLL.

Scaffold lại DAL sau khi schema SQL Server thay đổi:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef dbcontext scaffold "Name=ConnectionStrings:StudyHub" Microsoft.EntityFrameworkCore.SqlServer `
  --project StudyHub.DAL/StudyHub.DAL.csproj `
  --startup-project StudyHub.Web/StudyHub.Web.csproj `
  --context StudyHubDbContext --context-dir Persistence --output-dir Entities `
  --context-namespace StudyHub.DAL.Persistence --namespace StudyHub.DAL.Entities `
  --no-onconfiguring --force
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
- Bản ASP.NET Core lưu nhóm, thành viên và chat trong SQL Server qua `StudyGroups`, `GroupMembers`, `GroupPosts`. Bản React/Vite cũ vẫn dùng `localStorage` cho dữ liệu demo frontend.


## Run Locally

**Prerequisites:**  Node.js


1. Install dependencies:
   `npm install`
2. Nếu dùng tính năng AI, đặt `GEMINI_API_KEY` trong `.env.local`.
3. Chạy ứng dụng:
   `npm run dev`
