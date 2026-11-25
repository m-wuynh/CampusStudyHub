namespace StudyHub.BLL.Services.Pdf;

internal static class PdfTrashPolicy
{
    public const int RecoveryDays = 30;

    public static int DaysLeft(DateTime? deletedAtUtc, DateTime nowUtc)
    {
        if (deletedAtUtc is null)
        {
            return 0;
        }

        var remaining = deletedAtUtc.Value.AddDays(RecoveryDays) - nowUtc;
        return Math.Max(0, (int)Math.Ceiling(remaining.TotalDays));
    }
}
