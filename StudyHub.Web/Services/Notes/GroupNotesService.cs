using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Notes;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;

namespace StudyHub.Web.Services.Notes;

public sealed class GroupNotesService(
    StudyHubDbContext db,
    ICurrentUser currentUser,
    IStudyGroupGateway groups,
    IPdfStorage storage,
    IHttpContextAccessor httpContextAccessor,
    IWebHostEnvironment environment)
{
    private const int MaxDeltaBytes = 2 * 1024 * 1024;
    private const int MaxAnnotationBytes = 8 * 1024 * 1024;
    private const int MaxAnnotations = 5000;
    private const string EmptyDelta = "{\"ops\":[{\"insert\":\"\\n\"}]}";

    public Task<IReadOnlyList<NoteGroupOption>> GetEligibleGroupsAsync(CancellationToken ct) =>
        groups.GetActiveGroupsAsync(currentUser.UserId, ct);

    public async Task<IReadOnlyList<NotesDevelopmentPersona>> GetDevelopmentPersonasAsync(CancellationToken ct)
    {
        var realPersonas = await groups.GetDevelopmentPersonasAsync(ct);
        if (realPersonas.Count > 0 || !environment.IsDevelopment()) return realPersonas;

        var user = await db.Users.AsNoTracking()
            .Where(item => item.UserId == currentUser.UserId && item.Status == "Active")
            .Select(item => new { item.UserId, item.DisplayName, item.Email, item.RoleCode })
            .FirstOrDefaultAsync(ct);
        var group = user is null
            ? null
            : await groups.GetDevelopmentTestGroupAsync(user.UserId, ct);
        if (user is null || group is null) return [];

        return
        [
            new NotesDevelopmentPersona("simulation-owner", user.UserId, user.DisplayName, user.Email,
                user.RoleCode, "Owner · tài khoản thật", group.GroupId, group.Name),
            new NotesDevelopmentPersona("simulation-member", user.UserId, user.DisplayName, user.Email,
                user.RoleCode, "Member · giả lập trong Notes", group.GroupId, group.Name, IsSimulated: true)
        ];
    }

    public async Task<NoteGroupDescriptor> CreateDevelopmentTestGroupAsync(CancellationToken ct)
    {
        if (!environment.IsDevelopment()) throw new GroupNotesException(404, "Không tìm thấy trang.");
        if (!await db.Users.AnyAsync(item => item.UserId == currentUser.UserId && item.Status == "Active", ct))
            throw new GroupNotesException(403, "Tài khoản hiện tại không còn Active trong database.");
        return await groups.CreateDevelopmentTestGroupAsync(currentUser.UserId, ct);
    }

    public async Task<bool> CanCreateDevelopmentTestGroupAsync(CancellationToken ct) =>
        environment.IsDevelopment() && await db.Users.AnyAsync(
            item => item.UserId == currentUser.UserId && item.Status == "Active", ct);

    public async Task<NotesDevelopmentPersona?> FindDevelopmentPersonaAsync(
        string personaKey, long userId, long groupId, CancellationToken ct)
    {
        var personas = await GetDevelopmentPersonasAsync(ct);
        return personas.SingleOrDefault(persona => persona.PersonaKey == personaKey &&
            persona.UserId == userId && persona.GroupId == groupId);
    }

    public async Task ShareDocumentAsync(long documentId, long groupId, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        RequireOwnerPersonaForSharing(groupId);
        var document = await db.Documents.FirstOrDefaultAsync(item => item.DocumentId == documentId &&
            item.OwnerUserId == currentUser.UserId && !item.IsDeleted && item.ResourceType == "File", ct);
        if (document is null) throw new GroupNotesException(404, "Không tìm thấy PDF thuộc thư viện của bạn.");
        if (document.IsArchived) throw new GroupNotesException(409, "PDF đã lưu trữ, không thể chia sẻ.");
        if (await db.GroupDocumentShares.AnyAsync(item => item.DocumentId == documentId && item.StudyGroupId == groupId && item.IsActive, ct))
            throw new GroupNotesException(409, "PDF này đã được chia sẻ cho nhóm.");

        var versions = await db.DocumentVersions.Where(item => item.DocumentId == documentId).ToListAsync(ct);
        if (versions.Count == 0) throw new GroupNotesException(409, "PDF chưa có phiên bản lưu trữ.");
        var versionIds = versions.Select(item => item.DocumentVersionId).ToArray();
        var existingSetIds = await db.GroupAnnotationSets.AsNoTracking()
            .Where(item => item.StudyGroupId == groupId && versionIds.Contains(item.DocumentVersionId))
            .Select(item => item.DocumentVersionId).ToListAsync(ct);
        var missingVersions = versions.Where(item => !existingSetIds.Contains(item.DocumentVersionId)).ToList();
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.GroupDocumentShares.Add(new GroupDocumentShare
        {
            DocumentId = documentId,
            StudyGroupId = groupId,
            SharedByUserId = currentUser.UserId,
            IsActive = true,
            SharedAtUtc = now
        });
        db.GroupAnnotationSets.AddRange(missingVersions.Select(version => new GroupAnnotationSet
        {
            StudyGroupId = groupId,
            DocumentVersionId = version.DocumentVersionId,
            PayloadJson = "[]",
            UpdatedAtUtc = now
        }));
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            throw new GroupNotesException(409, "PDF vừa được chia sẻ hoặc nhóm/quyền truy cập đã đổi. Tải lại rồi thử lại.");
        }
    }

    public async Task UnshareDocumentAsync(long documentId, long groupId, CancellationToken ct)
    {
        RequireOwnerPersonaForSharing(groupId);
        var share = await db.GroupDocumentShares.FirstOrDefaultAsync(item => item.DocumentId == documentId &&
            item.StudyGroupId == groupId && item.SharedByUserId == currentUser.UserId && item.IsActive, ct);
        if (share is null) throw new GroupNotesException(404, "Không tìm thấy lượt chia sẻ đang hoạt động.");
        share.IsActive = false;
        share.UnsharedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ShareNoteSnapshotAsync(long noteId, long groupId, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        RequireOwnerPersonaForSharing(groupId);
        var source = await db.Notes.AsNoTracking().FirstOrDefaultAsync(item => item.NoteId == noteId &&
            item.OwnerUserId == currentUser.UserId && !item.IsDeleted, ct);
        if (source is null) throw new GroupNotesException(404, "Không tìm thấy ghi chú cá nhân.");
        if (source.IsArchived) throw new GroupNotesException(409, "Ghi chú đã lưu trữ, không thể chia sẻ.");
        if (await db.GroupNotes.AnyAsync(item => item.SourceNoteId == noteId && item.StudyGroupId == groupId && !item.IsDeleted, ct))
            throw new GroupNotesException(409, "Ghi chú này đã có snapshot trong nhóm.");

        db.GroupNotes.Add(new GroupNote
        {
            StudyGroupId = groupId,
            OwnerUserId = currentUser.UserId,
            SourceNoteId = noteId,
            Title = source.Title,
            ContentDeltaJson = NormalizeDelta(source.ContentDeltaJson),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new GroupNotesException(409, "Ghi chú vừa được chia sẻ vào nhóm. Tải lại workspace."); }
    }

    public async Task<GroupWorkspaceData> GetWorkspaceAsync(long groupId, CancellationToken ct)
    {
        var group = await RequireActiveMemberAsync(groupId, ct);
        var sharedRows = await db.GroupDocumentShares.AsNoTracking()
            .Where(share => share.StudyGroupId == groupId && share.IsActive &&
                !db.Documents.Any(document => document.DocumentId == share.DocumentId && document.IsDeleted))
            .Join(db.Documents.AsNoTracking(), share => share.DocumentId, document => document.DocumentId,
                (share, document) => new { share, document })
            .ToListAsync(ct);
        var documentIds = sharedRows.Select(item => item.document.DocumentId).ToArray();
        var versions = await db.DocumentVersions.AsNoTracking()
            .Where(version => documentIds.Contains(version.DocumentId))
            .OrderByDescending(version => version.DocumentVersionId)
            .ToListAsync(ct);
        var latestVersions = versions.GroupBy(item => item.DocumentId).ToDictionary(grouping => grouping.Key, grouping => grouping.First());
        var versionIds = latestVersions.Values.Select(item => item.DocumentVersionId).ToArray();
        var annotationRows = await db.GroupAnnotationSets.AsNoTracking()
            .Where(set => set.StudyGroupId == groupId && versionIds.Contains(set.DocumentVersionId))
            .ToListAsync(ct);
        var annotationTokens = annotationRows.ToDictionary(set => set.DocumentVersionId, set => Convert.ToBase64String(set.RowVersion));
        var documents = sharedRows
            .Where(item => latestVersions.ContainsKey(item.document.DocumentId))
            .Select(item =>
            {
                var version = latestVersions[item.document.DocumentId];
                return new GroupSharedDocument(
                    item.document.DocumentId, item.document.Title, version.OriginalFileName,
                    version.FileSizeBytes, version.DocumentVersionId, item.document.IsArchived,
                    IsContentOwner(groupId, item.document.OwnerUserId),
                    annotationTokens.GetValueOrDefault(version.DocumentVersionId));
            }).ToList();

        var simulatedMember = IsSimulatedMember(groupId);
        var noteRows = await db.GroupNotes.AsNoTracking()
            .Where(note => note.StudyGroupId == groupId && !note.IsDeleted)
            .OrderByDescending(note => note.UpdatedAtUtc)
            .ToListAsync(ct);
        var notes = noteRows.Select(note => new GroupSharedNote(
            note.GroupNoteId, note.Title, note.ContentDeltaJson,
            simulatedMember ? long.MinValue : note.OwnerUserId,
            note.IsArchived, Convert.ToBase64String(note.RowVersion), note.UpdatedAtUtc)).ToList();
        var requestRows = await db.GroupChangeRequests.AsNoTracking()
            .Where(request => request.StudyGroupId == groupId && request.Status == "Pending")
            .OrderBy(request => request.CreatedAtUtc)
            .ToListAsync(ct);
        var requestNoteIds = requestRows.Where(item => item.TargetGroupNoteId.HasValue)
            .Select(item => item.TargetGroupNoteId!.Value).Distinct().ToArray();
        var requestVersionIds = requestRows.Where(item => item.DocumentVersionId.HasValue)
            .Select(item => item.DocumentVersionId!.Value).Distinct().ToArray();
        var noteOwners = await db.GroupNotes.AsNoTracking().Where(item => requestNoteIds.Contains(item.GroupNoteId))
            .ToDictionaryAsync(item => item.GroupNoteId, item => item.OwnerUserId, ct);
        var versionOwners = await db.DocumentVersions.AsNoTracking()
            .Where(item => requestVersionIds.Contains(item.DocumentVersionId))
            .Join(db.Documents.AsNoTracking(), version => version.DocumentId, document => document.DocumentId,
                (version, document) => new { version.DocumentVersionId, document.OwnerUserId })
            .ToDictionaryAsync(item => item.DocumentVersionId, item => item.OwnerUserId, ct);
        var requests = requestRows.Select(item =>
        {
            long? reviewerId = item.TargetType == "GroupNote"
                ? item.TargetGroupNoteId is long noteId
                    ? noteOwners.GetValueOrDefault(noteId, group.OwnerUserId)
                    : group.OwnerUserId
                : item.DocumentVersionId is long versionId
                    ? versionOwners.GetValueOrDefault(versionId)
                    : null;
            return new GroupChangeRequestItem(
                item.GroupChangeRequestId, item.TargetType, item.Operation, item.TargetGroupNoteId,
                item.DocumentVersionId, item.ProposedTitle, item.RequesterUserId, item.Status,
                item.CreatedAtUtc, simulatedMember ? long.MinValue : reviewerId, RequestPreview(item));
        }).ToList();

        return new GroupWorkspaceData(groupId, group.Name, group.OwnerUserId, documents, notes, requests);
    }

    public async Task<GroupPdfDetails?> GetSharedPdfAsync(long groupId, long documentId, long? versionId, CancellationToken ct)
    {
        await RequireSharedPdfAsync(groupId, documentId, ct);
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(item => item.DocumentId == documentId && !item.IsDeleted, ct);
        if (document is null) return null;
        var version = versionId is long id
            ? await db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(item => item.DocumentId == documentId && item.DocumentVersionId == id, ct)
            : await db.DocumentVersions.AsNoTracking().Where(item => item.DocumentId == documentId)
                .OrderByDescending(item => item.DocumentVersionId).FirstOrDefaultAsync(ct);
        return version is null ? null : new GroupPdfDetails(
            document.DocumentId, document.Title, version.DocumentVersionId, version.OriginalFileName,
            version.FileSizeBytes, version.PageCount, document.IsArchived,
            IsContentOwner(groupId, document.OwnerUserId));
    }

    public async Task<string?> GetSharedPdfPathAsync(long groupId, long documentId, long? versionId, CancellationToken ct)
    {
        var details = await GetSharedPdfAsync(groupId, documentId, versionId, ct);
        if (details is null) return null;
        var version = await db.DocumentVersions.AsNoTracking().FirstAsync(item => item.DocumentVersionId == details.VersionId, ct);
        var path = storage.GetPhysicalPath(version.StorageKey);
        return File.Exists(path) ? path : null;
    }

    public async Task<GroupAnnotationData> GetAnnotationsAsync(long groupId, long documentId, long versionId, CancellationToken ct)
    {
        var details = await GetSharedPdfAsync(groupId, documentId, versionId, ct)
            ?? throw new GroupNotesException(404, "Không tìm thấy PDF hoặc phiên bản.");
        var set = await GetOrCreateAnnotationSetAsync(groupId, versionId, ct);
        return new GroupAnnotationData(set.PayloadJson, Convert.ToBase64String(set.RowVersion), details.IsArchived,
            details.CanManage, details.PageCount);
    }

    public async Task<string> SaveOwnerAnnotationsAsync(
        long groupId, long documentId, long versionId, JsonElement payload, string? versionToken, CancellationToken ct)
    {
        var details = await GetSharedPdfAsync(groupId, documentId, versionId, ct)
            ?? throw new GroupNotesException(404, "Không tìm thấy PDF hoặc phiên bản.");
        if (!details.CanManage) throw new GroupNotesException(403, "Thành viên không được ghi trực tiếp lên annotation chính thức.");
        if (details.IsArchived) throw new GroupNotesException(409, "PDF đã lưu trữ, annotation chỉ đọc.");
        ValidateAnnotations(payload, details.PageCount);
        var set = await GetOrCreateAnnotationSetAsync(groupId, versionId, ct);
        RequireVersion(set.RowVersion, versionToken);
        set.PayloadJson = payload.GetRawText();
        set.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditLogs.Add(CreateAudit("GroupAnnotation.Updated", "GroupAnnotationSet", $"{groupId}:{versionId}", "Chủ sở hữu cập nhật annotation chính thức."));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new GroupNotesException(409, "Annotation đã đổi ở nơi khác. Tải lại trước khi lưu."); }
        return Convert.ToBase64String(set.RowVersion);
    }

    public async Task<GroupNoteMutationResult> CreateGroupNoteAsync(long groupId, GroupNoteMutation request, CancellationToken ct)
    {
        var group = await RequireActiveMemberAsync(groupId, ct);
        var title = ValidateTitle(request.Title);
        var delta = ValidateDelta(request.ContentDeltaJson);
        if (!IsContentOwner(groupId, group.OwnerUserId))
        {
            var change = AddRequest(groupId, "GroupNote", "CREATE", null, null, title, delta, null);
            await db.SaveChangesAsync(ct);
            return new(false, change.GroupChangeRequestId, null);
        }

        var note = new GroupNote
        {
            StudyGroupId = groupId, OwnerUserId = currentUser.UserId, Title = title,
            ContentDeltaJson = delta, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.GroupNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return new(true, null, new GroupSharedNote(note.GroupNoteId, note.Title, note.ContentDeltaJson,
            note.OwnerUserId, false, Convert.ToBase64String(note.RowVersion), note.UpdatedAtUtc));
    }

    public async Task<GroupNoteMutationResult> SaveGroupNoteAsync(
        long groupId, long noteId, GroupNoteMutation request, string? operation, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        var note = await db.GroupNotes.FirstOrDefaultAsync(item => item.GroupNoteId == noteId &&
            item.StudyGroupId == groupId && !item.IsDeleted, ct);
        if (note is null) throw new GroupNotesException(404, "Không tìm thấy ghi chú trong nhóm.");
        if (note.IsArchived) throw new GroupNotesException(409, "Ghi chú đã lưu trữ, chỉ có thể xem.");
        if (!IsContentOwner(groupId, note.OwnerUserId))
            throw new GroupNotesException(403, "Thành viên không được sửa trực tiếp Group Note; hãy gửi ChangeRequest.");
        var action = NormalizeOperation(operation, "UPDATE");
        var title = action == "DELETE" ? note.Title : ValidateTitle(request.Title);
        var delta = action == "DELETE" ? null : ValidateDelta(request.ContentDeltaJson);
        RequireVersion(note.RowVersion, request.BaseVersionToken);

        if (action == "DELETE") note.IsDeleted = true;
        else { note.Title = title; note.ContentDeltaJson = delta!; }
        note.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new GroupNotesException(409, "Ghi chú đã đổi ở nơi khác. Tải lại trước khi lưu."); }
        return new(true, null, note.IsDeleted ? null : new GroupSharedNote(note.GroupNoteId, note.Title,
            note.ContentDeltaJson, note.OwnerUserId, note.IsArchived, Convert.ToBase64String(note.RowVersion), note.UpdatedAtUtc));
    }

    public async Task<GroupChangeRequestItem> SubmitChangeAsync(long groupId, GroupChangeRequestInput request, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        var type = request.TargetType?.Trim();
        var operation = NormalizeOperation(request.Operation, "UPDATE");
        if (type == "GroupNote")
        {
            if (operation == "CREATE")
            {
                if (request.TargetId is not null || !string.IsNullOrWhiteSpace(request.BaseVersionToken))
                    throw new GroupNotesException(400, "Yêu cầu tạo ghi chú mới không nhận target hoặc version.");
                var change = AddRequest(groupId, type, operation, null, null,
                    ValidateTitle(request.ProposedTitle), ValidateDelta(request.ProposedPayloadJson), null);
                await db.SaveChangesAsync(ct);
                return ToRequest(change);
            }

            var note = await db.GroupNotes.FirstOrDefaultAsync(item => item.GroupNoteId == request.TargetId &&
                item.StudyGroupId == groupId && !item.IsDeleted, ct);
            if (note is null) throw new GroupNotesException(404, "Không tìm thấy ghi chú trong nhóm.");
            if (note.IsArchived) throw new GroupNotesException(409, "Ghi chú đã lưu trữ, không thể gửi đề xuất.");
            RequireVersion(note.RowVersion, request.BaseVersionToken);
            var proposedTitle = operation == "DELETE" ? note.Title : ValidateTitle(request.ProposedTitle);
            var proposed = operation == "DELETE" ? null : ValidateDelta(request.ProposedPayloadJson);
            var changeRequest = AddRequest(groupId, type, operation, note.GroupNoteId, null,
                proposedTitle, proposed, request.BaseVersionToken);
            await db.SaveChangesAsync(ct);
            return ToRequest(changeRequest);
        }

        if (type == "GroupAnnotation")
        {
            if (request.TargetId is not null || request.DocumentVersionId is not long versionId)
                throw new GroupNotesException(400, "Yêu cầu annotation cần DocumentVersionId.");
            var documentId = await RequireSharedVersionAsync(groupId, versionId, ct);
            var document = await db.Documents.AsNoTracking().FirstAsync(item => item.DocumentId == documentId, ct);
            if (document.IsArchived) throw new GroupNotesException(409, "PDF đã lưu trữ, không thể gửi đề xuất.");
            var set = await GetOrCreateAnnotationSetAsync(groupId, versionId, ct);
            RequireVersion(set.RowVersion, request.BaseVersionToken);
            var proposed = request.ProposedPayloadJson is null && operation == "DELETE"
                ? "[]" : ValidateAnnotationsJson(request.ProposedPayloadJson, null);
            var annotationRequest = AddRequest(groupId, type, operation, null, versionId,
                null, proposed, request.BaseVersionToken);
            await db.SaveChangesAsync(ct);
            return ToRequest(annotationRequest);
        }

        throw new GroupNotesException(400, "TargetType chỉ nhận GroupNote hoặc GroupAnnotation.");
    }

    public async Task<ChangeDecisionResult> DecideChangeAsync(
        long groupId, long requestId, bool approve, string? resolutionNote, CancellationToken ct)
    {
        var group = await RequireActiveMemberAsync(groupId, ct);
        var request = await db.GroupChangeRequests.FirstOrDefaultAsync(item => item.GroupChangeRequestId == requestId &&
            item.StudyGroupId == groupId, ct);
        if (request is null) throw new GroupNotesException(404, "Không tìm thấy đề xuất.");
        if (request.Status != "Pending") throw new GroupNotesException(409, $"Đề xuất đã được xử lý ({request.Status}).");

        GroupNote? note = null;
        GroupAnnotationSet? annotationSet = null;
        long ownerId;
        bool archived = false;
        if (request.TargetType == "GroupNote")
        {
            if (request.Operation == "CREATE") ownerId = group.OwnerUserId;
            else
            {
                note = await db.GroupNotes.FirstOrDefaultAsync(item => item.GroupNoteId == request.TargetGroupNoteId &&
                    item.StudyGroupId == groupId && !item.IsDeleted, ct);
                if (note is null) throw new GroupNotesException(404, "Ghi chú đích đã bị xóa.");
                ownerId = note.OwnerUserId;
                archived = note.IsArchived;
            }
        }
        else
        {
            var documentId = await RequireSharedVersionAsync(groupId, request.DocumentVersionId!.Value, ct);
            var document = await db.Documents.FirstOrDefaultAsync(item => item.DocumentId == documentId && !item.IsDeleted, ct);
            if (document is null) throw new GroupNotesException(404, "PDF đích không còn khả dụng.");
            ownerId = document.OwnerUserId;
            archived = document.IsArchived;
            annotationSet = await GetOrCreateAnnotationSetAsync(groupId, request.DocumentVersionId.Value, ct);
        }
        if (!IsContentOwner(groupId, ownerId))
            throw new GroupNotesException(403, "Chỉ chủ sở hữu của nội dung mới được duyệt hoặc từ chối.");
        if (approve && archived) throw new GroupNotesException(409, "Nội dung đã lưu trữ, không thể áp dụng đề xuất.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (approve && !BaseVersionMatches(request, note, annotationSet))
        {
            request.Status = "Conflict";
            request.ReviewerUserId = currentUser.UserId;
            request.ResolvedAtUtc = DateTime.UtcNow;
            request.ResolutionNote = "Phiên bản chính thức đã thay đổi trước khi duyệt.";
            db.AuditLogs.Add(CreateAudit("ChangeRequest.Conflict", "GroupChangeRequest", requestId.ToString(), request.ResolutionNote));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new("Conflict", null);
        }

        var before = request.TargetType == "GroupNote" ? note?.Title ?? "(new)" : annotationSet!.PayloadJson;
        GroupNote? createdNote = null;
        if (approve)
        {
            if (request.TargetType == "GroupNote" && request.Operation == "CREATE")
            {
                createdNote = new GroupNote
                {
                    StudyGroupId = groupId,
                    OwnerUserId = currentUser.UserId,
                    Title = request.ProposedTitle!,
                    ContentDeltaJson = request.ProposedPayloadJson!,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                db.GroupNotes.Add(createdNote);
            }
            else if (request.TargetType == "GroupNote")
            {
                if (request.Operation == "DELETE") note!.IsDeleted = true;
                else
                {
                    note!.Title = request.ProposedTitle!;
                    note.ContentDeltaJson = request.ProposedPayloadJson!;
                    note.UpdatedAtUtc = DateTime.UtcNow;
                }
            }
            else
            {
                annotationSet!.PayloadJson = request.ProposedPayloadJson ?? "[]";
                annotationSet.UpdatedAtUtc = DateTime.UtcNow;
            }
            request.Status = "Approved";
        }
        else request.Status = "Rejected";
        request.ReviewerUserId = currentUser.UserId;
        request.ResolvedAtUtc = DateTime.UtcNow;
        request.ResolutionNote = TrimToNull(resolutionNote, 1000);
        db.AuditLogs.Add(CreateAudit(
            approve ? "ChangeRequest.Approved" : "ChangeRequest.Rejected",
            "GroupChangeRequest", requestId.ToString(),
            $"{request.TargetType} {request.Operation}; before={ShortAuditValue(before)}; after={(approve ? ShortAuditValue(request.ProposedTitle ?? request.ProposedPayloadJson) : "unchanged")}. {request.ResolutionNote}"));

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var staleRequest = await db.GroupChangeRequests.FirstOrDefaultAsync(item => item.GroupChangeRequestId == requestId, ct);
            if (staleRequest is not null && staleRequest.Status == "Pending")
            {
                staleRequest.Status = "Conflict";
                staleRequest.ReviewerUserId = currentUser.UserId;
                staleRequest.ResolvedAtUtc = DateTime.UtcNow;
                staleRequest.ResolutionNote = "Phiên bản chính thức đã thay đổi đồng thời.";
                db.AuditLogs.Add(CreateAudit("ChangeRequest.Conflict", "GroupChangeRequest", requestId.ToString(), staleRequest.ResolutionNote));
                await db.SaveChangesAsync(ct);
            }
            return new("Conflict", null);
        }

        var created = createdNote is null ? null : new GroupSharedNote(
            createdNote.GroupNoteId, createdNote.Title, createdNote.ContentDeltaJson, createdNote.OwnerUserId,
            createdNote.IsArchived, Convert.ToBase64String(createdNote.RowVersion), createdNote.UpdatedAtUtc);
        return new(request.Status, created);
    }

    public async Task<bool> SetGroupNoteArchiveAsync(long groupId, long noteId, bool archived, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        var note = await db.GroupNotes.FirstOrDefaultAsync(item => item.GroupNoteId == noteId &&
            item.StudyGroupId == groupId && !item.IsDeleted, ct);
        if (note is null) return false;
        if (!IsContentOwner(groupId, note.OwnerUserId))
            throw new GroupNotesException(403, "Chỉ chủ sở hữu mới được lưu trữ ghi chú.");
        note.IsArchived = archived;
        note.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<NoteGroupDescriptor> RequireActiveMemberAsync(long groupId, CancellationToken ct)
    {
        if (groupId <= 0) throw new GroupNotesException(400, "Mã nhóm không hợp lệ.");
        var group = await groups.GetActiveGroupAsync(groupId, ct);
        if (group is null) throw new GroupNotesException(404, "Nhóm không tồn tại hoặc đã ngừng hoạt động.");
        if (!await groups.IsActiveMemberAsync(currentUser.UserId, groupId, ct))
            throw new GroupNotesException(403, "Bạn không còn là thành viên đang hoạt động của nhóm.");
        return group;
    }

    private bool IsSimulatedMember(long groupId)
    {
        if (!environment.IsDevelopment()) return false;
        var principal = httpContextAccessor.HttpContext?.User;
        return principal?.FindFirst("notes_dev_persona")?.Value == "member-simulation" &&
            long.TryParse(principal.FindFirst("notes_dev_group")?.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var personaGroupId) && personaGroupId == groupId;
    }

    private bool IsContentOwner(long groupId, long ownerUserId) =>
        !IsSimulatedMember(groupId) && currentUser.UserId == ownerUserId;

    private void RequireOwnerPersonaForSharing(long groupId)
    {
        if (IsSimulatedMember(groupId))
            throw new GroupNotesException(403, "Member giả lập không được chia sẻ hoặc thu hồi tài liệu trong nhóm kiểm thử.");
    }

    private async Task<long> RequireSharedPdfAsync(long groupId, long documentId, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        var shared = await db.GroupDocumentShares.AsNoTracking().AnyAsync(item => item.DocumentId == documentId &&
            item.StudyGroupId == groupId && item.IsActive, ct);
        var exists = await db.Documents.AsNoTracking().AnyAsync(item => item.DocumentId == documentId && !item.IsDeleted, ct);
        if (!shared || !exists) throw new GroupNotesException(404, "PDF chưa được chia sẻ cho nhóm hoặc không còn khả dụng.");
        return documentId;
    }

    private async Task<long> RequireSharedVersionAsync(long groupId, long versionId, CancellationToken ct)
    {
        var documentId = await db.DocumentVersions.AsNoTracking()
            .Where(item => item.DocumentVersionId == versionId)
            .Select(item => (long?)item.DocumentId).FirstOrDefaultAsync(ct);
        if (documentId is null) throw new GroupNotesException(404, "Không tìm thấy phiên bản PDF.");
        await RequireSharedPdfAsync(groupId, documentId.Value, ct);
        return documentId.Value;
    }

    private async Task<GroupAnnotationSet> GetOrCreateAnnotationSetAsync(long groupId, long versionId, CancellationToken ct)
    {
        var existing = await db.GroupAnnotationSets.FirstOrDefaultAsync(item => item.StudyGroupId == groupId &&
            item.DocumentVersionId == versionId, ct);
        if (existing is not null) return existing;
        var created = new GroupAnnotationSet
        {
            StudyGroupId = groupId, DocumentVersionId = versionId, PayloadJson = "[]", UpdatedAtUtc = DateTime.UtcNow
        };
        db.GroupAnnotationSets.Add(created);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(created).State = EntityState.Detached;
            return await db.GroupAnnotationSets.FirstAsync(item => item.StudyGroupId == groupId && item.DocumentVersionId == versionId, ct);
        }
        return created;
    }

    private GroupChangeRequest AddRequest(long groupId, string targetType, string operation,
        long? noteId, long? versionId, string? title, string? payload, string? baseToken)
    {
        var request = new GroupChangeRequest
        {
            StudyGroupId = groupId,
            RequesterUserId = currentUser.UserId,
            TargetGroupNoteId = noteId,
            DocumentVersionId = versionId,
            TargetType = targetType,
            Operation = operation,
            ProposedTitle = title,
            ProposedPayloadJson = payload,
            BaseVersionToken = baseToken,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.GroupChangeRequests.Add(request);
        return request;
    }

    private AuditLog CreateAudit(string action, string entityType, string key, string? summary) => new()
    {
        ActorUserId = currentUser.UserId,
        ActionCode = action,
        EntityType = entityType,
        EntityKey = key.Length <= 100 ? key : key[..100],
        Summary = TrimToNull(summary, 1000),
        CreatedAtUtc = DateTime.UtcNow
    };

    private static bool BaseVersionMatches(GroupChangeRequest request, GroupNote? note, GroupAnnotationSet? set) =>
        request.TargetType == "GroupNote"
            ? request.Operation == "CREATE" ? string.IsNullOrEmpty(request.BaseVersionToken)
                : note is not null && VersionMatches(note.RowVersion, request.BaseVersionToken)
            : set is not null && VersionMatches(set.RowVersion, request.BaseVersionToken);

    private static void RequireVersion(byte[] current, string? expected)
    {
        if (!VersionMatches(current, expected))
            throw new GroupNotesException(409, "Nội dung đã thay đổi. Tải bản mới trước khi gửi hoặc lưu.");
    }

    private static bool VersionMatches(byte[] current, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected)) return false;
        try { return CryptographicOperations.FixedTimeEquals(current, Convert.FromBase64String(expected)); }
        catch (FormatException) { return false; }
    }

    private static void ValidateAnnotations(JsonElement payload, int? pageCount)
    {
        if (payload.ValueKind != JsonValueKind.Array || payload.GetArrayLength() > MaxAnnotations ||
            Encoding.UTF8.GetByteCount(payload.GetRawText()) > MaxAnnotationBytes)
            throw new GroupNotesException(400, "Danh sách annotation không hợp lệ hoặc vượt quá giới hạn 8 MB.");
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in payload.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()) || id.GetString()!.Length > 100 ||
                !item.TryGetProperty("pageIndex", out var page) || !page.TryGetInt32(out var pageIndex) || pageIndex < 0 ||
                pageIndex > 100_000 || (pageCount is int count && pageIndex >= count) || !keys.Add(id.GetString()!))
                throw new GroupNotesException(400, "Annotation cần id duy nhất và pageIndex hợp lệ.");
        }
    }

    private static string ValidateAnnotationsJson(string? raw, int? pageCount)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new GroupNotesException(400, "Thiếu annotation payload.");
        try
        {
            using var document = JsonDocument.Parse(raw);
            ValidateAnnotations(document.RootElement, pageCount);
            return document.RootElement.GetRawText();
        }
        catch (JsonException) { throw new GroupNotesException(400, "Annotation payload không phải JSON hợp lệ."); }
    }

    private static string ValidateDelta(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || Encoding.UTF8.GetByteCount(raw) > MaxDeltaBytes)
            throw new GroupNotesException(400, "Nội dung ghi chú trống hoặc vượt quá 2 MB.");
        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("ops", out var ops) || ops.ValueKind != JsonValueKind.Array)
                throw new GroupNotesException(400, "Delta ghi chú cần có mảng ops.");
            return document.RootElement.GetRawText();
        }
        catch (JsonException) { throw new GroupNotesException(400, "Delta ghi chú không phải JSON hợp lệ."); }
    }

    private static string NormalizeDelta(string? raw) => string.IsNullOrWhiteSpace(raw) ? EmptyDelta : ValidateDelta(raw);

    private static string ValidateTitle(string? value)
    {
        var title = value?.Trim();
        return !string.IsNullOrWhiteSpace(title) && title.Length <= 200
            ? title
            : throw new GroupNotesException(400, "Tiêu đề cần có từ 1 đến 200 ký tự.");
    }

    private static string NormalizeOperation(string? value, string fallback)
    {
        var operation = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
        return operation is "CREATE" or "UPDATE" or "DELETE"
            ? operation
            : throw new GroupNotesException(400, "Operation chỉ nhận CREATE, UPDATE hoặc DELETE.");
    }

    private static string? TrimToNull(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];

    private static string ShortAuditValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "(empty)";
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];
        return $"sha256:{digest}";
    }

    private static string RequestPreview(GroupChangeRequest request)
    {
        if (request.Operation == "DELETE") return "Đề xuất xóa nội dung hiện tại.";
        if (string.IsNullOrWhiteSpace(request.ProposedPayloadJson)) return "Không có nội dung đề xuất.";
        try
        {
            using var json = JsonDocument.Parse(request.ProposedPayloadJson);
            if (request.TargetType == "GroupAnnotation")
                return $"Bộ annotation mới: {json.RootElement.GetArrayLength()} mục.";
            if (!json.RootElement.TryGetProperty("ops", out var ops)) return "Delta ghi chú mới.";
            var text = string.Concat(ops.EnumerateArray()
                .Where(operation => operation.TryGetProperty("insert", out var insert) && insert.ValueKind == JsonValueKind.String)
                .Select(operation => operation.GetProperty("insert").GetString()));
            text = text.Trim();
            return text.Length <= 500 ? text : $"{text[..500]}…";
        }
        catch (JsonException) { return "Nội dung đề xuất không thể xem trước."; }
    }

    private static GroupChangeRequestItem ToRequest(GroupChangeRequest request) => new(
        request.GroupChangeRequestId, request.TargetType, request.Operation,
        request.TargetGroupNoteId, request.DocumentVersionId, request.ProposedTitle,
        request.RequesterUserId, request.Status, request.CreatedAtUtc);
}

public sealed class GroupNotesException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed record GroupWorkspaceData(long GroupId, string GroupName, long GroupOwnerUserId,
    IReadOnlyList<GroupSharedDocument> Documents, IReadOnlyList<GroupSharedNote> Notes,
    IReadOnlyList<GroupChangeRequestItem> PendingRequests);
public sealed record GroupSharedDocument(long DocumentId, string Title, string FileName, long SizeBytes,
    long VersionId, bool IsArchived, bool CanManage, string? AnnotationVersionToken);
public sealed record GroupSharedNote(long GroupNoteId, string Title, string ContentDeltaJson, long OwnerUserId,
    bool IsArchived, string VersionToken, DateTime UpdatedAtUtc);
public sealed record GroupChangeRequestItem(long Id, string TargetType, string Operation, long? TargetGroupNoteId,
    long? DocumentVersionId, string? ProposedTitle, long RequesterUserId, string Status, DateTime CreatedAtUtc,
    long? ReviewOwnerUserId = null, string? ProposedPreview = null);
public sealed record GroupPdfDetails(long DocumentId, string Title, long VersionId, string FileName,
    long SizeBytes, int? PageCount, bool IsArchived, bool CanManage);
public sealed record GroupAnnotationData(string PayloadJson, string VersionToken, bool IsArchived,
    bool CanManage, int? PageCount);
public sealed record GroupNoteMutation(string? Title, string? ContentDeltaJson, string? BaseVersionToken);
public sealed record GroupNoteMutationResult(bool Applied, long? RequestId, GroupSharedNote? Note);
public sealed record GroupChangeRequestInput(string? TargetType, string? Operation, long? TargetId,
    long? DocumentVersionId, string? ProposedTitle, string? ProposedPayloadJson, string? BaseVersionToken);
public sealed record ChangeDecisionResult(string Status, GroupSharedNote? CreatedNote);
