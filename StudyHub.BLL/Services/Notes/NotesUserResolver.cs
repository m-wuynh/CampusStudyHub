using StudyHub.BLL.DTOs.Notes;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Notes;

public sealed class NotesUserResolver(IRepository repository)
{
    private static readonly SemaphoreSlim DemoLock = new(1, 1);

    public async Task<NotesUserDto> ResolveAsync(
        bool authenticated,
        string? idClaim,
        bool development,
        CancellationToken ct)
    {
        // Ưu tiên tài khoản đăng nhập thật.
        if (authenticated)
        {
            if (!long.TryParse(idClaim, out var id) || id <= 0)
            {
                throw new NotesException(
                    "Tài khoản đăng nhập chưa có mã người dùng hợp lệ.",
                    401);
            }

            var user = await repository.FirstOrDefaultAsync<User>(
                x => x.UserId == id && x.Status == "Active",
                ct);

            if (user == null)
            {
                throw new NotesException(
                    "Tài khoản không tồn tại hoặc đã bị khóa.",
                    403);
            }

            return new NotesUserDto(
                user.UserId,
                user.DisplayName,
                IsDemo: false);
        }

        // Production không được dùng tài khoản demo.
        if (!development)
        {
            throw new NotesException(
                "Bạn cần đăng nhập để dùng Notes.",
                401);
        }

        await DemoLock.WaitAsync(ct);

        try
        {
            var demo = await repository.FirstOrDefaultAsync<User>(
                x => x.StudentCode == "NOTES-DEMO"
                     && x.Email == "notes-demo@example.invalid",
                ct);

            if (demo == null)
            {
                demo = new User
                {
                    DisplayName = "Người dùng thử Notes",
                    Email = "notes-demo@example.invalid",
                    StudentCode = "NOTES-DEMO",
                    EducationLevel = "Other",
                    RoleCode = "Student",
                    Status = "Active",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                await repository.AddAsync(demo, ct);
                await repository.SaveChangesAsync(ct);
            }

            if (demo.Status != "Active")
            {
                throw new NotesException(
                    "User demo đã bị khóa.",
                    403);
            }

            return new NotesUserDto(
                demo.UserId,
                demo.DisplayName,
                IsDemo: true);
        }
        finally
        {
            DemoLock.Release();
        }
    }
}
