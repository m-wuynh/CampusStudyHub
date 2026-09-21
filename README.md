
# Campus Study Hub

Ứng dụng ASP.NET Core MVC hỗ trợ quản lý ghi chú, flashcards, lịch học, điểm số và nhóm học tập.

## Kiến trúc ASP.NET Core 3 layer

```text
StudyHub.Web       Presentation: MVC Controller, Razor View, ViewModel, JavaScript, DI
       │
       ├──────────────► StudyHub.BLL
       │                Business: DTO, service và nghiệp vụ
       │                       ▲
       ▼                       │
StudyHub.DAL ───────────────────┘
Data Access: EF Core Database First, Repository, SQL Server
```

Mã nguồn được chia tiếp theo feature để mỗi thành viên có thể làm độc lập:

```text
StudyHub.Web/
  Controllers/<Feature>/   MVC controller và API controller
  ViewModels/<Feature>/    Model chỉ phục vụ giao diện
  Views/<Feature>/         Razor MVC view

StudyHub.BLL/
  DTOs/                    Toàn bộ dữ liệu vào/ra dùng chung của BLL
  Services/<Feature>/      Hàm, interface và thuật toán nghiệp vụ theo feature

StudyHub.DAL/
  Entities/                Entity scaffold từ SQL Server
  Enums/<Feature>/         Trạng thái và lựa chọn dùng chung với dữ liệu
  Repositories/Common/     IRepository và EfRepository dùng chung
  Repositories/<Feature>/  Interface và implementation repository của feature
  Persistence/             StudyHubDbContext
```

BLL chỉ gọi một đầu mối `IRepository`. Các hàm generic như `ListAsync<TEntity>()`,
`AddAsync<TEntity>()`, `Update<TEntity>()` dùng chung cho mọi entity; truy vấn có quy
tắc riêng được gọi qua repository feature, ví dụ `IRepository.StudyGroups`.
`SaveChanges()`/`SaveChangesAsync()` được quản lý tập trung tại đầu mối này;
controller không gọi `StudyHubDbContext` trực tiếp.

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
- Ứng dụng lưu nhóm, thành viên và chat trong SQL Server qua `StudyGroups`, `GroupMembers`, `GroupPosts`.
