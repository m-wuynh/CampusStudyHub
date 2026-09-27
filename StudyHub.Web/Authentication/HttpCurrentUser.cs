using System.Globalization;
using System.Security.Claims;
using StudyHub.BLL.Services.Auth;

namespace StudyHub.Web.Authentication;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User
        ?? throw new UnauthorizedAccessException("Không có phiên HTTP hiện tại.");

    public long UserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
                ? userId
                : throw new UnauthorizedAccessException("Phiên đăng nhập không có mã người dùng hợp lệ.");
        }
    }

    public string DisplayName => User.Identity?.Name ?? "Sinh viên";
}
