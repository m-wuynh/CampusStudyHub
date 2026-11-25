using StudyHub.BLL.Services.Auth;

namespace StudyHub.BLL.Services.Pdf;

internal static class PdfCurrentUser
{
    public static long RequirePdfUserId(this ICurrentUser currentUser)
    {
        if (currentUser.UserId <= 0)
        {
            throw new PdfException(401, "Bạn cần đăng nhập.");
        }

        return currentUser.UserId;
    }
}
