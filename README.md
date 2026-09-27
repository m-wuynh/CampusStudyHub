
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
- Connection string: `StudyHub.Web/appsettings.json` và file ghi đè khi Debug
  `StudyHub.Web/appsettings.Development.json` → `ConnectionStrings:StudyHub`.
- Entity và `StudyHubDbContext` được scaffold từ 27 bảng và 4 view trong database.
- `SqlStudyGroupRepository` ánh xạ bảng `StudyGroups`, `GroupMembers`, `GroupPosts` sang model của BLL.

Quy trình thay đổi schema bằng EF Core Migration:

```powershell
dotnet tool restore

# Sau khi sửa entity và StudyHubDbContext, tạo migration mới
dotnet tool run dotnet-ef migrations add TenMigration `
  --project StudyHub.DAL/StudyHub.DAL.csproj `
  --startup-project StudyHub.Web/StudyHub.Web.csproj `
  --context StudyHubDbContext `
  --output-dir Migrations

# Kiểm tra rồi áp dụng migration vào database
dotnet tool run dotnet-ef database update `
  --project StudyHub.DAL/StudyHub.DAL.csproj `
  --startup-project StudyHub.Web/StudyHub.Web.csproj `
  --context StudyHubDbContext
```

Không sửa trực tiếp schema trong SSMS rồi scaffold đè lên `StudyHubDbContext`. Mỗi thay
đổi database phải đi cùng một migration trong `StudyHub.DAL/Migrations` để các thành
viên trong nhóm có thể cập nhật cùng một phiên bản schema.

Chạy bản ASP.NET Core:

```powershell
dotnet run --project StudyHub.Web/StudyHub.Web.csproj --launch-profile https
```


## Feature nhóm học tập

- Ba cách tham gia theo mô hình Discord: nhóm công khai vào ngay (`Open`), nhóm công khai cần duyệt (`Approval`) và nhóm riêng tư chỉ qua lời mời (`InviteOnly`).
- Thành viên có vòng đời `Pending`, `Active`, `Rejected`, `Left`, `Banned`; người bị ban không thể tự tham gia lại và nhóm không thể vượt `MaxMembers`.
- Ba vai trò gọn cho ứng dụng học tập: Owner, Moderator và Member. Owner/Moderator được duyệt, từ chối, mời ra hoặc cấm thành viên; Owner được bổ nhiệm Moderator.
- Lời mời có mã ngẫu nhiên, ngày hết hạn và giới hạn lượt sử dụng. Link có dạng `/Groups/Invite/{code}`.
- Workspace nhóm gồm Tổng quan, Thông báo, Chat, Tài nguyên và Thành viên. Người chưa được duyệt chỉ xem phần giới thiệu công khai.
- Thông báo được tách khỏi chat và hỗ trợ ghim một thông báo quan trọng.
- Chat cập nhật tức thời bằng SignalR; nếu không tải được SignalR client, giao diện tự chuyển sang REST và polling dự phòng.
- Ghi chú, tài liệu và flashcard chỉ xuất hiện trong nhóm khi dữ liệu có đúng `StudyGroupId` và `Visibility = 'Group'`.
- Các thao tác thay đổi thành viên chạy trong transaction SQL và API dùng antiforgery token.

Sau khi pull code có thay đổi schema, áp dụng EF Core migrations:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project StudyHub.DAL/StudyHub.DAL.csproj `
  --startup-project StudyHub.Web/StudyHub.Web.csproj `
  --context StudyHubDbContext
```

`BaselineExistingDatabase` đăng ký schema Database First hiện hữu mà không tạo lại
bảng hoặc xóa dữ liệu. Các thay đổi schema tiếp theo phải được tạo và áp dụng bằng
EF Core Migration.
