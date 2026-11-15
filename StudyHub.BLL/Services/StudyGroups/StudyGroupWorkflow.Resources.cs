using StudyHub.DAL.Entities.Pdf;
using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using StudyHub.BLL.Services.StudyGroups.Models;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed partial class StudyGroupWorkflow
{
    private async Task RestorePrivateResourcesAsync(
    long groupId,
    long userId,
    CancellationToken ct)
{
    // Giữ nguyên cơ chế flashcard hiện có.
    await data.Query<FlashcardDeck>(false)
        .Where(x =>
            x.StudyGroupId == groupId &&
            x.OwnerUserId == userId)
        .ExecuteUpdateAsync(
            setters => setters
                .SetProperty(x => x.StudyGroupId, (long?)null)
                .SetProperty(x => x.Visibility, "Private")
                .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow),
            ct);

    // Khi chủ sở hữu rời, bị kick hoặc bị ban, PDF chỉ bị thu hồi khỏi nhóm;
    // file PDF cá nhân gốc vẫn thuộc về chủ sở hữu.
    var unsharedAtUtc = DateTime.UtcNow;
    var sharedDocumentIds = await data.Query<PdfDocumentShare>(false)
        .Where(share =>
            share.StudyGroupId == groupId &&
            share.IsActive)
        .Join(
            data.Query<PdfDocument>(false).Where(document => document.OwnerUserId == userId),
            share => share.DocumentId,
            document => document.DocumentId,
            (share, document) => document.DocumentId)
        .Distinct()
        .ToListAsync(ct);

    await data.Query<PdfDocumentShare>(false)
        .Where(share =>
            share.StudyGroupId == groupId &&
            share.IsActive &&
            data.Query<PdfDocument>(false).Any(document =>
                document.DocumentId == share.DocumentId &&
                document.OwnerUserId == userId))
        .ExecuteUpdateAsync(
            setters => setters
                .SetProperty(share => share.IsActive, false)
                .SetProperty(share => share.UnsharedAtUtc, unsharedAtUtc),
            ct);

    if (sharedDocumentIds.Count > 0)
    {
        await data.Query<PdfDocument>(false)
            .Where(document => sharedDocumentIds.Contains(document.DocumentId))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    document => document.UpdatedAtUtc,
                    unsharedAtUtc),
                ct);
    }

    // Giữ nguyên xử lý đăng ký buổi học tương lai.
    var now = DateTimeOffset.UtcNow;

    await data.Query<GroupSessionAttendance>(false)
        .Where(x =>
            x.UserId == userId &&
            x.Session.StudyGroupId == groupId &&
            x.Session.Status == "Scheduled" &&
            x.Session.StartsAtUtc > now)
        .ExecuteDeleteAsync(ct);
}

}
