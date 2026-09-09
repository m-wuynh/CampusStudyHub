# Campus Study Hub

Không gian học tập dành cho sinh viên: quản lý môn học, lịch học, deadline, kho tài liệu và nhóm học theo từng lớp.

## Đã có trong code base

- Đăng nhập Google bằng cookie session; tự tạo hồ sơ từ tên, email, ảnh đại diện.
- Hồ sơ bắt buộc gồm trường, khoa/chuyên ngành và khóa học.
- Phân quyền `student` / `admin`, khóa tài khoản và kiểm tra quyền ở service trước khi đọc/ghi dữ liệu.
- Dashboard: môn đang học, deadline, việc hoàn thành, giờ học, tài liệu mới và nhóm học.
- CRUD môn học, mã mời lớp; deadline; lịch lặp hằng tuần; lọc theo ngày/tuần/tháng và môn.
- Tài liệu dạng liên kết hoặc file PDF/TXT/DOCX/PPTX tối đa 10 MB; tìm kiếm, lọc, lưu, thống kê mở/tải, báo cáo.
- Bài đăng nhóm học, gửi yêu cầu tham gia, chủ nhóm duyệt/từ chối, giới hạn số thành viên.
- Trang admin: thống kê, tìm người dùng, khóa/mở khóa, ẩn/khôi phục nội dung báo cáo.
- SQLite tự tạo tại `App_Data/campus.db`; event log cho các hành vi chính; endpoint `/health`.
- Giao diện responsive cho mobile, tablet và desktop; có trang landing, login, pricing, privacy, terms.
- Bộ kiểm tra nghiệp vụ độc lập và GitHub Actions tại `tests/CampusStudyHub.Checks` và `.github/workflows/ci.yml`.

## Chạy lần đầu

Cài .NET 8 SDK, mở PowerShell tại thư mục chứa `CampusStudyHub.slnx`, sau đó:

```powershell
dotnet restore
dotnet run --project .\CampusStudyHub\CampusStudyHub\CampusStudyHub.csproj
```

Mở địa chỉ hiện trong terminal, thường là `http://localhost:5260`. Ở môi trường Development, trang `/login` có tài khoản demo; bật/tắt bằng `Demo:Enabled` trong `CampusStudyHub/CampusStudyHub/appsettings.Development.json`.

Chạy kiểm tra nghiệp vụ:

```powershell
dotnet run --project .\tests\CampusStudyHub.Checks\CampusStudyHub.Checks.csproj
```

## Cấu hình Google OAuth

Tạo OAuth Client loại **Web application** trong Google Cloud Console. Thêm redirect URI `https://localhost:5001/signin-google` (hoặc đúng host/port mà ứng dụng in ra), rồi lưu secret bằng User Secrets, không ghi vào Git:

```powershell
dotnet user-secrets init --project .\CampusStudyHub\CampusStudyHub\CampusStudyHub.csproj
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID" --project .\CampusStudyHub\CampusStudyHub\CampusStudyHub.csproj
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET" --project .\CampusStudyHub\CampusStudyHub\CampusStudyHub.csproj
dotnet user-secrets set "Authentication:AdminEmails:0" "admin@your-school.edu" --project .\CampusStudyHub\CampusStudyHub\CampusStudyHub.csproj
```

Giới hạn email sinh viên (không bắt buộc):

```powershell
dotnet user-secrets set "Authentication:AllowedDomains:0" "your-school.edu" --project .\CampusStudyHub\CampusStudyHub\CampusStudyHub.csproj
```

Đăng nhập Google cần chạy HTTPS khi triển khai. Cookie, secret và file upload không được commit. Khi deploy thật, đổi `Demo:Enabled` thành `false`, dùng database/storage phù hợp và bổ sung thông tin pháp lý trong Privacy/Terms.

## Cấu trúc chính

`Program.cs` cấu hình auth, SQLite, endpoint và seed demo. `Data/HubDb.cs` là entity/schema. `Services/HubService.cs` là nghiệp vụ và authorization. `Components/Pages/Hub.razor` chứa các màn hình học tập/admin. `wwwroot/app.css` là design system responsive.

## Tự tạo repo và thêm thành viên GitHub

1. Trên GitHub chọn **New repository**, đặt tên, chọn Public/Private và tạo repo trống (không tạo README/.gitignore nếu thư mục local đã có).
2. Trong PowerShell tại thư mục dự án, chạy các lệnh Git mà GitHub hiển thị để commit và push. Kiểm tra `.gitignore` trước khi push để chắc chắn không có `App_Data`, secret hoặc file build.
3. Muốn mời thành viên: mở repo → **Settings** → **Collaborators** (hoặc **Manage access**) → **Invite a collaborator** → nhập username/email GitHub → chọn quyền `Write` cho thành viên code → gửi lời mời. Người được mời phải chấp nhận invitation trước khi push.
4. Mỗi thành viên clone repo, tạo branch riêng, push branch và mở Pull Request; không commit trực tiếp vào `main`. Bật branch protection/required review khi nhóm bắt đầu làm chung.

Chi tiết thao tác tạo repository và mời collaborator xem tài liệu chính thức: <https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-new-repository> và <https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/repository-access-and-collaboration/inviting-collaborators-to-a-personal-repository>.

## Phạm vi chưa triển khai

Email nhắc deadline, thanh toán thật, chat/video real-time, AI lập kế hoạch và đồng bộ lịch ngoài là phần mở rộng. Pricing hiện chỉ mô phỏng trang giới thiệu, chưa thu tiền.
