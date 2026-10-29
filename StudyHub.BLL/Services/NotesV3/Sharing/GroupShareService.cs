using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.Sharing;

public sealed class GroupShareService(
    IRepository repository,
    ICurrentUser currentUser,
    INotesV3AccessService access) : IGroupShareService
{
    // Giới hạn tổng binary seed của một lần copy.
    private const long MaxCopySeedBytes = 16L * 1024 * 1024;

    public async Task<IReadOnlyList<EligibleNotesGroup>>
    ListEligibleGroupsAsync(CancellationToken ct = default)
{
    RequireAuthenticated();

    var groups = repository.Query<StudyGroup>();
    var members = repository.Query<GroupMember>();

    return await (
        from studyGroup in groups
        join membership in members
            on studyGroup.StudyGroupId equals membership.StudyGroupId
        where !studyGroup.IsArchived
              && membership.UserId == currentUser.UserId
              && membership.Status == "Active"
        orderby studyGroup.GroupName, studyGroup.StudyGroupId
        select new EligibleNotesGroup(
            studyGroup.StudyGroupId,
            studyGroup.GroupName))
        .ToListAsync(ct);
}

    public async Task<IReadOnlyList<GroupPdfItem>> ListSharedPdfsAsync(
        long groupId,
        CancellationToken ct = default)
    {
        await access.RequireGroupAsync(groupId, ct);

        var shares = repository.Query<NotesV3DocumentShare>();
        var documents = repository.Query<NotesV3Document>();
        var versions = repository.Query<NotesV3DocumentVersion>();

        var rows = await (
            from share in shares
            join document in documents
                on share.DocumentId equals document.DocumentId
            from version in versions
                .Where(item =>
                    item.DocumentId == document.DocumentId)
                .OrderByDescending(item =>
                    item.DocumentVersionId)
                .Take(1)
            where share.StudyGroupId == groupId
                  && share.IsActive
                  && !document.IsDeleted
                  && !document.IsArchived
            orderby share.SharedAtUtc descending,
                share.GroupDocumentShareId descending
            select new
            {
                share.GroupDocumentShareId,
                document.DocumentId,
                version.DocumentVersionId,
                document.Title,
                version.OriginalFileName,
                version.FileSizeBytes,
                version.PageCount,
                document.OwnerUserId,
                share.RowVersion
            })
            .ToListAsync(ct);

        return rows.Select(item => new GroupPdfItem(
            item.GroupDocumentShareId,
            item.DocumentId,
            item.DocumentVersionId,
            item.Title,
            item.OriginalFileName,
            item.FileSizeBytes,
            item.PageCount,
            item.OwnerUserId == currentUser.UserId,
            Convert.ToBase64String(item.RowVersion)))
            .ToList();
    }

    public async Task<PdfShareResult> SharePdfAsync(
        long documentId,
        long groupId,
        CancellationToken ct = default)
    {
        await access.RequireOwnedPdfAsync(
            documentId,
            forWrite: true,
            cancellationToken: ct);

        await access.RequireGroupAsync(groupId, ct);

        var hasVersion = await repository
            .Query<NotesV3DocumentVersion>()
            .AnyAsync(
                item => item.DocumentId == documentId &&
                        item.PageCount.HasValue &&
                        item.PageCount.Value > 0,
                ct);

        if (!hasVersion)
        {
            throw new NotesV3Exception(
                409,
                "PDF chưa có phiên bản được kiểm tra hợp lệ.");
        }

        var existing = await FindActiveShareAsync(
            documentId,
            groupId,
            ct);

        if (existing is not null)
        {
            return ToResult(existing);
        }

        var share = new NotesV3DocumentShare
        {
            DocumentId = documentId,
            StudyGroupId = groupId,
            SharedByUserId = currentUser.UserId,
            IsActive = true,
            SharedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(share, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConflict(exception))
        {
            // Hai request share cùng PDF/nhóm.
            // Unique filtered index chỉ cho một share đang active.
            existing = await FindActiveShareAsync(
                documentId,
                groupId,
                ct);

            if (existing is null)
            {
                throw;
            }

            return ToResult(existing);
        }

        return ToResult(share);
    }

    public async Task UnsharePdfAsync(
        long shareId,
        string versionToken,
        CancellationToken ct = default)
    {
        RequireAuthenticated();

        var share = await repository
            .Query<NotesV3DocumentShare>(asNoTracking: false)
            .SingleOrDefaultAsync(
                item => item.GroupDocumentShareId == shareId,
                ct)
            ?? throw Missing();

        // Chủ PDF vẫn được thu hồi quyền chia sẻ
        // khi không còn là thành viên của nhóm.
        await access.RequireOwnedPdfAsync(
            share.DocumentId,
            cancellationToken: ct);

        if (!share.IsActive)
        {
            return;
        }

        RequireVersion(share.RowVersion, versionToken);

        share.IsActive = false;
        share.UnsharedAtUtc = DateTime.UtcNow;

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict();
        }

        // Không xóa Page Notes hoặc updates của session.
        // AccessService từ chối các lần đọc/ghi session đã thu hồi.
        // Thông báo live sẽ nối ở Presentation/SignalR.
    }

    public async Task<CopyNotebookResult> CopyNotebookAsync(
        CopyNotebookRequest request,
        CancellationToken ct = default)
    {
        var source = await access.RequireNotebookAsync(
            request.SourceNotebookId,
            ct);

        if (source.StudyGroupId.HasValue)
        {
            throw new NotesV3Exception(
                400,
                "Chỉ hỗ trợ chép notebook cá nhân vào nhóm.");
        }

        await access.RequireGroupAsync(request.GroupId, ct);

        var sections = await repository
            .Query<NotesV3Section>()
            .Where(item =>
                item.NotebookId == source.NotebookId &&
                !item.IsDeleted)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.SectionId)
            .ToListAsync(ct);

        var sectionIds = sections
            .Select(item => item.SectionId)
            .ToArray();

        var pages = await repository
            .Query<NotesV3Page>()
            .Where(item =>
                item.SectionId.HasValue &&
                sectionIds.Contains(item.SectionId.Value) &&
                !item.IsDeleted)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.PageId)
            .ToListAsync(ct);

        var seeds = ValidateSeeds(request.Seeds, pages);

        var sourcePageIds = pages
            .Select(item => item.PageId)
            .ToArray();

        var references = await repository
            .Query<NotesV3Reference>()
            .Where(item => sourcePageIds.Contains(item.PageId))
            .ToListAsync(ct);

        await using var transaction =
            await repository.BeginTransactionAsync(ct);

        var copiedNotebook = new NotesV3Notebook
        {
            OwnerUserId = currentUser.UserId,
            StudyGroupId = request.GroupId,
            Title = source.Title,
            SortOrder = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(copiedNotebook, ct);
        await repository.SaveChangesAsync(ct);

        var copiedSections = new Dictionary<long, NotesV3Section>();

        foreach (var section in sections)
        {
            var copy = new NotesV3Section
            {
                NotebookId = copiedNotebook.NotebookId,
                Title = section.Title,
                SortOrder = section.SortOrder
            };

            copiedSections.Add(section.SectionId, copy);
        }

        await repository.AddRangeAsync(
            copiedSections.Values,
            ct);

        await repository.SaveChangesAsync(ct);

        var copiedPages = new Dictionary<long, NotesV3Page>();

        foreach (var page in pages)
        {
            var copy = new NotesV3Page
            {
                SectionId = copiedSections[
                    page.SectionId!.Value].SectionId,
                Title = page.Title,
                SortOrder = page.SortOrder,

                // Nội dung nhóm lấy từ Yjs updates, không từ Delta này.
                ContentDeltaJson = NotesV3Validation.EmptyDelta
            };

            copiedPages.Add(page.PageId, copy);
        }

        await repository.AddRangeAsync(
            copiedPages.Values,
            ct);

        await repository.SaveChangesAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var page in pages)
        {
            var seed = seeds[page.PageId];

            await repository.AddAsync(
                new NotesV3CollabUpdate
                {
                    PageId = copiedPages[page.PageId].PageId,
                    OperationId = Guid.NewGuid(),
                    ActorUserId = currentUser.UserId,
                    UpdateBytes = seed.InitialUpdate,
                    CreatedAtUtc = now
                },
                ct);
        }

        foreach (var reference in references)
        {
            await repository.AddAsync(
                new NotesV3Reference
                {
                    PageId = copiedPages[reference.PageId].PageId,
                    DocumentVersionId = reference.DocumentVersionId,
                    GroupDocumentShareId =
                        reference.GroupDocumentShareId,
                    PhysicalPageNumber =
                        reference.PhysicalPageNumber,
                    AnchorJson = reference.AnchorJson,
                    CapturedText = reference.CapturedText
                },
                ct);
        }

        await repository.SaveChangesAsync(ct);

        // Notebook, sections, pages, reference và seed
        // chỉ cùng xuất hiện sau khi transaction commit.
        await transaction.CommitAsync(ct);

        return new CopyNotebookResult(
            copiedNotebook.NotebookId,
            request.GroupId);
    }

    private Task<NotesV3DocumentShare?> FindActiveShareAsync(
        long documentId,
        long groupId,
        CancellationToken ct)
    {
        return repository
            .Query<NotesV3DocumentShare>()
            .SingleOrDefaultAsync(
                item => item.DocumentId == documentId &&
                        item.StudyGroupId == groupId &&
                        item.IsActive,
                ct);
    }

    private static Dictionary<long, NotebookPageSeed> ValidateSeeds(
        IReadOnlyList<NotebookPageSeed>? seeds,
        IReadOnlyList<NotesV3Page> pages)
    {
        if (seeds is null || seeds.Count != pages.Count)
        {
            throw new NotesV3Exception(
                400,
                "Phải có đúng một Yjs seed cho mỗi trang được chép.");
        }

        var result = new Dictionary<long, NotebookPageSeed>();
        long totalBytes = 0;

        foreach (var seed in seeds)
        {
            if (seed is null ||
                seed.InitialUpdate is null ||
                seed.InitialUpdate.Length < 1 ||
                seed.InitialUpdate.Length >
                    NotesV3Validation.MaxUpdateBytes ||
                !result.TryAdd(seed.SourcePageId, seed))
            {
                throw new NotesV3Exception(
                    400,
                    "Yjs seed bị thiếu, trùng trang hoặc vượt giới hạn.");
            }

            totalBytes += seed.InitialUpdate.Length;

            if (totalBytes > MaxCopySeedBytes)
            {
                throw new NotesV3Exception(
                    413,
                    "Tổng Yjs seed của một lần chép tối đa 16 MiB.");
            }
        }

        foreach (var page in pages)
        {
            if (!result.TryGetValue(page.PageId, out var seed))
            {
                throw new NotesV3Exception(
                    400,
                    "Danh sách seed không khớp các trang của notebook.");
            }

            RequireVersion(
                page.RowVersion,
                seed.SourceVersionToken);
        }

        return result;
    }

    private void RequireAuthenticated()
    {
        if (currentUser.UserId <= 0)
        {
            throw new NotesV3Exception(
                401,
                "Bạn cần đăng nhập.");
        }
    }

    private static void RequireVersion(
        byte[] current,
        string expectedToken)
    {
        var expected = NotesV3Validation.Version(expectedToken);

        if (!current.AsSpan().SequenceEqual(expected))
        {
            throw Conflict();
        }
    }

    private static bool IsUniqueConflict(
        DbUpdateException exception) =>
        exception.InnerException is SqlException
        {
            Number: 2601 or 2627
        };

    private static PdfShareResult ToResult(
        NotesV3DocumentShare share) =>
        new(
            share.GroupDocumentShareId,
            share.DocumentId,
            share.StudyGroupId,
            Convert.ToBase64String(share.RowVersion));

    private static NotesV3Exception Missing() =>
        new(404, "Không tìm thấy lượt chia sẻ.");

    private static NotesV3Exception Conflict() =>
        new(
            409,
            "Dữ liệu đã thay đổi. Tải lại trước khi thực hiện.");
}