using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Globalization;
using System.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Notes;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;
using StudyHub.Web.Hubs;

namespace StudyHub.Web.Services.Notes;

public sealed class GroupNotesService(
    StudyHubDbContext db,
    ICurrentUser currentUser,
    IStudyGroupGateway groups,
    IPdfStorage storage,
    IHttpContextAccessor httpContextAccessor,
    IWebHostEnvironment environment,
    IHubContext<NotesHub> notesHub,
    ILogger<GroupNotesService> logger)
{
    private const int MaxDeltaBytes = 2 * 1024 * 1024;
    private const int MaxAnnotationBytes = 8 * 1024 * 1024;
    private const int MaxAnnotations = 5000;
    private const string EmptyDelta = "{\"ops\":[{\"insert\":\"\\n\"}]}";

    public Task<IReadOnlyList<NoteGroupOption>> GetEligibleGroupsAsync(CancellationToken ct) =>
        groups.GetActiveGroupsAsync(currentUser.UserId, ct);

    public async Task<IReadOnlyList<DocumentShareStatus>> GetDocumentSharesAsync(long documentId, CancellationToken ct)
    {
        var owned = await db.Documents.AsNoTracking().AnyAsync(item => item.DocumentId == documentId &&
            item.OwnerUserId == currentUser.UserId && !item.IsDeleted && item.ResourceType == "File", ct);
        if (!owned) throw new GroupNotesException(404, "Không tìm thấy PDF thuộc thư viện của bạn.");
        return await db.GroupDocumentShares.AsNoTracking()
            .Where(item => item.DocumentId == documentId && item.IsActive)
            .Join(db.StudyGroups.AsNoTracking(), share => share.StudyGroupId, group => group.StudyGroupId,
                (share, group) => new { group.StudyGroupId, group.GroupName })
            .OrderBy(item => item.GroupName)
            .Select(item => new DocumentShareStatus(item.StudyGroupId, item.GroupName))
            .ToListAsync(ct);
    }

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
        var sessionized = await HasSessionTableAsync(ct);
        if (!sessionized && await db.GroupDocumentShares.AnyAsync(item => item.DocumentId == documentId && item.StudyGroupId == groupId, ct))
            throw new GroupNotesException(409, "Không thể chia sẻ lại PDF trước khi annotation được tách theo phiên chia sẻ mới.");

        var versions = await db.DocumentVersions.Where(item => item.DocumentId == documentId).ToListAsync(ct);
        if (versions.Count == 0) throw new GroupNotesException(409, "PDF chưa có phiên bản lưu trữ.");
        var versionIds = versions.Select(item => item.DocumentVersionId).ToArray();
        var existingSetIds = sessionized ? [] : await db.GroupAnnotationSets.AsNoTracking()
            .Where(item => item.StudyGroupId == groupId && versionIds.Contains(item.DocumentVersionId))
            .Select(item => item.DocumentVersionId).ToListAsync(ct);
        var missingVersions = versions.Where(item => !existingSetIds.Contains(item.DocumentVersionId)).ToList();
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var shareEventQueued = await StageEventAsync(groupId, "PdfShareChanged", new { documentId }, ct);
        var share = new GroupDocumentShare
        {
            DocumentId = documentId,
            StudyGroupId = groupId,
            SharedByUserId = currentUser.UserId,
            IsActive = true,
            SharedAtUtc = now
        };
        db.GroupDocumentShares.Add(share);
        try
        {
            await db.SaveChangesAsync(ct);
            if (sessionized)
                db.GroupAnnotationSessions.AddRange(versions.Select(version => new GroupAnnotationSession
                {
                    GroupDocumentShareId = share.GroupDocumentShareId,
                    DocumentVersionId = version.DocumentVersionId,
                    PayloadJson = "[]",
                    UpdatedAtUtc = now
                }));
            else
                db.GroupAnnotationSets.AddRange(missingVersions.Select(version => new GroupAnnotationSet
                {
                    StudyGroupId = groupId,
                    DocumentVersionId = version.DocumentVersionId,
                    PayloadJson = "[]",
                    UpdatedAtUtc = now
                }));
            db.AuditLogs.Add(CreateAudit("PdfShare.Created", "GroupDocumentShare", share.GroupDocumentShareId.ToString(),
                $"document={documentId}; group={groupId}"));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            throw new GroupNotesException(409, "PDF vừa được chia sẻ hoặc nhóm/quyền truy cập đã đổi. Tải lại rồi thử lại.");
        }
        if (!shareEventQueued) await NotifyGroupAsync(groupId, "PdfShareChanged", new { documentId }, ct);
    }

    public async Task UnshareDocumentAsync(long documentId, long groupId, CancellationToken ct)
    {
        RequireOwnerPersonaForSharing(groupId);
        var share = await db.GroupDocumentShares.FirstOrDefaultAsync(item => item.DocumentId == documentId &&
            item.StudyGroupId == groupId && item.SharedByUserId == currentUser.UserId && item.IsActive, ct);
        if (share is null) throw new GroupNotesException(404, "Không tìm thấy lượt chia sẻ đang hoạt động.");
        share.IsActive = false;
        share.UnsharedAtUtc = DateTime.UtcNow;
        db.AuditLogs.Add(CreateAudit("PdfShare.Revoked", "GroupDocumentShare", share.GroupDocumentShareId.ToString(),
            $"document={documentId}; group={groupId}"));
        var revokeEventQueued = await StageEventAsync(groupId, "PdfShareChanged", new { documentId }, ct);
        await db.SaveChangesAsync(ct);
        if (!revokeEventQueued) await NotifyGroupAsync(groupId, "PdfShareChanged", new { documentId }, ct);
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

        var groupNote = new GroupNote
        {
            StudyGroupId = groupId,
            OwnerUserId = currentUser.UserId,
            SourceNoteId = noteId,
            Title = source.Title,
            ContentDeltaJson = NormalizeDelta(source.ContentDeltaJson),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.GroupNotes.Add(groupNote);
        var importedEventQueued = false;
        try
        {
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(CreateAudit("GroupNote.Imported", "GroupNote", groupNote.GroupNoteId.ToString(),
                $"sourceNote={noteId}; group={groupId}"));
            await StageRevisionAsync(groupNote, "Imported", ct);
            importedEventQueued = await StageEventAsync(groupId, "GroupNotesChanged", new { noteId = groupNote.GroupNoteId }, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            throw new GroupNotesException(409, "Ghi chú vừa được chia sẻ vào nhóm. Tải lại workspace.");
        }
        if (!importedEventQueued) await NotifyGroupAsync(groupId, "GroupNotesChanged", new { noteId = groupNote.GroupNoteId }, ct);
    }

    public async Task<GroupWorkspaceData> GetWorkspaceAsync(long groupId, CancellationToken ct)
    {
        var group = await RequireActiveMemberAsync(groupId, ct);
        var sharedRows = await db.GroupDocumentShares.AsNoTracking()
            .Where(share => share.StudyGroupId == groupId && share.IsActive &&
                db.Documents.Any(document => document.DocumentId == share.DocumentId &&
                    !document.IsDeleted && !document.IsArchived && document.OwnerUser.Status == "Active" &&
                    db.GroupMembers.Any(member => member.StudyGroupId == groupId &&
                        member.UserId == document.OwnerUserId && member.Status == "Active")))
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
        var sessionized = await HasSessionTableAsync(ct);
        var annotationTokens = new Dictionary<(long ShareId, long VersionId), string>();
        if (sessionized)
        {
            var shareIds = sharedRows.Select(item => item.share.GroupDocumentShareId).ToArray();
            var sessionRows = await db.GroupAnnotationSessions.AsNoTracking()
                .Where(set => shareIds.Contains(set.GroupDocumentShareId) && versionIds.Contains(set.DocumentVersionId))
                .ToListAsync(ct);
            foreach (var set in sessionRows)
                annotationTokens[(set.GroupDocumentShareId, set.DocumentVersionId)] = Convert.ToBase64String(set.RowVersion);
        }
        else
        {
            var legacyRows = await db.GroupAnnotationSets.AsNoTracking()
                .Where(set => set.StudyGroupId == groupId && versionIds.Contains(set.DocumentVersionId))
                .ToListAsync(ct);
            foreach (var item in sharedRows)
                foreach (var set in legacyRows.Where(set => latestVersions.TryGetValue(item.document.DocumentId, out var version) &&
                    set.DocumentVersionId == version.DocumentVersionId))
                    annotationTokens[(item.share.GroupDocumentShareId, set.DocumentVersionId)] = Convert.ToBase64String(set.RowVersion);
        }
        var documents = sharedRows
            .Where(item => latestVersions.ContainsKey(item.document.DocumentId))
            .Select(item =>
            {
                var version = latestVersions[item.document.DocumentId];
                return new GroupSharedDocument(
                    item.document.DocumentId, item.document.Title, version.OriginalFileName,
                    version.FileSizeBytes, version.DocumentVersionId, item.document.IsArchived,
                    IsContentOwner(groupId, item.document.OwnerUserId),
                    annotationTokens.GetValueOrDefault((item.share.GroupDocumentShareId, version.DocumentVersionId)));
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
        var recoveryCutoff = DateTime.UtcNow.AddDays(-30);
        var deletedRows = await db.GroupNotes.AsNoTracking()
            .Where(note => note.StudyGroupId == groupId && note.IsDeleted && note.UpdatedAtUtc >= recoveryCutoff)
            .OrderByDescending(note => note.UpdatedAtUtc)
            .Take(50)
            .ToListAsync(ct);
        var deletedNotes = deletedRows.Select(note => new GroupDeletedNote(note.GroupNoteId, note.Title,
            Convert.ToBase64String(note.RowVersion), note.UpdatedAtUtc)).ToList();
        return new GroupWorkspaceData(groupId, group.Name, group.OwnerUserId, documents, notes, [], deletedNotes,
            await HasRevisionTableAsync(ct));
    }

    public async Task<GroupPdfDetails?> GetSharedPdfAsync(long groupId, long documentId, long? versionId, CancellationToken ct)
    {
        var shareId = await RequireSharedPdfAsync(groupId, documentId, ct);
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(item => item.DocumentId == documentId && !item.IsDeleted, ct);
        if (document is null) return null;
        var version = versionId is long id
            ? await db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(item => item.DocumentId == documentId && item.DocumentVersionId == id, ct)
            : await db.DocumentVersions.AsNoTracking().Where(item => item.DocumentId == documentId)
                .OrderByDescending(item => item.DocumentVersionId).FirstOrDefaultAsync(ct);
        return version is null ? null : new GroupPdfDetails(
            document.DocumentId, document.Title, version.DocumentVersionId, version.OriginalFileName,
            version.FileSizeBytes, version.PageCount, document.IsArchived,
            IsContentOwner(groupId, document.OwnerUserId), shareId);
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
        if (await HasSessionTableAsync(ct))
        {
            var session = await GetOrCreateAnnotationSessionAsync(details.ShareId, versionId, ct);
            return new GroupAnnotationData(session.PayloadJson, Convert.ToBase64String(session.RowVersion),
                details.IsArchived, details.CanManage, details.PageCount, details.ShareId);
        }
        var legacy = await GetOrCreateAnnotationSetAsync(groupId, versionId, ct);
        return new GroupAnnotationData(legacy.PayloadJson, Convert.ToBase64String(legacy.RowVersion),
            details.IsArchived, details.CanManage, details.PageCount, details.ShareId);
    }

    public async Task<string> SaveGroupAnnotationsAsync(
        long groupId, long documentId, long versionId, long shareId, JsonElement payload, string? versionToken, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var details = await GetSharedPdfAsync(groupId, documentId, versionId, ct)
            ?? throw new GroupNotesException(404, "Không tìm thấy PDF hoặc phiên bản.");
        if (shareId != details.ShareId) throw new GroupNotesException(409, "Phiên chia sẻ PDF đã thay đổi. Tải lại trước khi lưu.");
        if (details.IsArchived) throw new GroupNotesException(409, "PDF đã lưu trữ, annotation chỉ đọc.");
        ValidateAnnotations(payload, details.PageCount);
        var annotationEventQueued = await StageEventAsync(groupId, "GroupAnnotationsChanged", new { documentId, versionId }, ct);
        string nextToken;
        if (await HasSessionTableAsync(ct))
        {
            var session = await GetOrCreateAnnotationSessionAsync(shareId, versionId, ct);
            RequireVersion(session.RowVersion, versionToken);
            session.PayloadJson = payload.GetRawText();
            session.UpdatedAtUtc = DateTime.UtcNow;
            db.AuditLogs.Add(CreateAudit("GroupAnnotation.Updated", "GroupAnnotationSession",
                $"{shareId}:{versionId}", $"group={groupId}"));
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { throw new GroupNotesException(409, "Annotation đã đổi ở nơi khác. Tải lại trước khi lưu."); }
            nextToken = Convert.ToBase64String(session.RowVersion);
        }
        else
        {
            var set = await GetOrCreateAnnotationSetAsync(groupId, versionId, ct);
            RequireVersion(set.RowVersion, versionToken);
            set.PayloadJson = payload.GetRawText();
            set.UpdatedAtUtc = DateTime.UtcNow;
            db.AuditLogs.Add(CreateAudit("GroupAnnotation.Updated", "GroupAnnotationSet", $"{groupId}:{versionId}",
                "Thành viên Active cập nhật annotation nhóm."));
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { throw new GroupNotesException(409, "Annotation đã đổi ở nơi khác. Tải lại trước khi lưu."); }
            nextToken = Convert.ToBase64String(set.RowVersion);
        }
        await transaction.CommitAsync(ct);
        if (!annotationEventQueued) await NotifyGroupAsync(groupId, "GroupAnnotationsChanged", new { documentId, versionId }, ct);
        return nextToken;
    }

    public async Task<GroupNoteMutationResult> CreateGroupNoteAsync(long groupId, GroupNoteMutation request, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        var title = ValidateTitle(request.Title);
        var delta = ValidateDelta(request.ContentDeltaJson);

        var note = new GroupNote
        {
            StudyGroupId = groupId, OwnerUserId = currentUser.UserId, Title = title,
            ContentDeltaJson = delta, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.GroupNotes.Add(note);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(CreateAudit("GroupNote.Created", "GroupNote", note.GroupNoteId.ToString(), $"group={groupId}"));
        await StageRevisionAsync(note, "Created", ct);
        var createdEventQueued = await StageEventAsync(groupId, "GroupNotesChanged", new { noteId = note.GroupNoteId }, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (!createdEventQueued) await NotifyGroupAsync(groupId, "GroupNotesChanged", new { noteId = note.GroupNoteId }, ct);
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
        var action = NormalizeOperation(operation, "UPDATE");
        var title = action == "DELETE" ? note.Title : ValidateTitle(request.Title);
        var delta = action == "DELETE" ? null : ValidateDelta(request.ContentDeltaJson);
        RequireVersion(note.RowVersion, request.BaseVersionToken);

        if (action == "DELETE") note.IsDeleted = true;
        else { note.Title = title; note.ContentDeltaJson = delta!; }
        note.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditLogs.Add(CreateAudit(action == "DELETE" ? "GroupNote.Deleted" : "GroupNote.Updated",
            "GroupNote", note.GroupNoteId.ToString(), $"group={groupId}; version={request.BaseVersionToken}"));
        await StageRevisionAsync(note, action == "DELETE" ? "Deleted" : "Updated", ct);
        var savedEventQueued = await StageEventAsync(groupId, "GroupNotesChanged", new { noteId }, ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new GroupNotesException(409, "Ghi chú đã đổi ở nơi khác. Tải lại trước khi lưu."); }
        if (!savedEventQueued) await NotifyGroupAsync(groupId, "GroupNotesChanged", new { noteId }, ct);
        return new(true, null, note.IsDeleted ? null : new GroupSharedNote(note.GroupNoteId, note.Title,
            note.ContentDeltaJson, note.OwnerUserId, note.IsArchived, Convert.ToBase64String(note.RowVersion), note.UpdatedAtUtc));
    }

    public async Task<GroupSharedNote> RestoreDeletedNoteAsCopyAsync(
        long groupId, long noteId, string? versionToken, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var deleted = await db.GroupNotes.AsNoTracking().FirstOrDefaultAsync(note =>
            note.GroupNoteId == noteId && note.StudyGroupId == groupId && note.IsDeleted &&
            note.UpdatedAtUtc >= cutoff, ct);
        if (deleted is null) throw new GroupNotesException(404, "Không tìm thấy GroupNote đã xóa trong thời hạn khôi phục.");
        RequireVersion(deleted.RowVersion, versionToken);
        var (_, delta) = MakeSafeTextCopy(deleted.ContentDeltaJson);
        var now = DateTime.UtcNow;
        var copy = new GroupNote
        {
            StudyGroupId = groupId,
            OwnerUserId = currentUser.UserId,
            Title = ValidateTitle($"{deleted.Title[..Math.Min(deleted.Title.Length, 188)]} (khôi phục)"),
            ContentDeltaJson = delta,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.GroupNotes.Add(copy);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(CreateAudit("GroupNote.RestoredAsCopy", "GroupNote", copy.GroupNoteId.ToString(),
            $"sourceGroupNote={noteId}; group={groupId}"));
        await StageRevisionAsync(copy, "RestoredAsCopy", ct);
        var restoredEventQueued = await StageEventAsync(groupId, "GroupNotesChanged", new { noteId = copy.GroupNoteId }, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (!restoredEventQueued) await NotifyGroupAsync(groupId, "GroupNotesChanged", new { noteId = copy.GroupNoteId }, ct);
        return new GroupSharedNote(copy.GroupNoteId, copy.Title, copy.ContentDeltaJson,
            copy.OwnerUserId, false, Convert.ToBase64String(copy.RowVersion), copy.UpdatedAtUtc);
    }

    public async Task<IReadOnlyList<GroupNoteRevisionItem>> GetNoteRevisionsAsync(
        long groupId, long noteId, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        if (!await db.GroupNotes.AsNoTracking().AnyAsync(note =>
                note.GroupNoteId == noteId && note.StudyGroupId == groupId, ct))
            throw new GroupNotesException(404, "Không tìm thấy GroupNote trong nhóm.");
        if (!await HasRevisionTableAsync(ct))
            throw new GroupNotesException(503, "Lịch sử phiên bản chưa được kích hoạt trên database.");
        var cutoff = DateTime.UtcNow.AddDays(-30);
        return await db.GroupNoteRevisions.AsNoTracking()
            .Where(revision => revision.GroupNoteId == noteId && revision.StudyGroupId == groupId &&
                revision.CreatedAtUtc >= cutoff)
            .OrderByDescending(revision => revision.GroupNoteRevisionId)
            .Take(50)
            .Select(revision => new GroupNoteRevisionItem(
                revision.GroupNoteRevisionId, revision.Title, revision.Operation,
                revision.ActorUserId, revision.IsDeleted, revision.IsArchived, revision.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<GroupSharedNote> RestoreRevisionAsCopyAsync(
        long groupId, long noteId, long revisionId, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        if (!await HasRevisionTableAsync(ct))
            throw new GroupNotesException(503, "Lịch sử phiên bản chưa được kích hoạt trên database.");
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var revision = await db.GroupNoteRevisions.AsNoTracking()
            .Where(item => item.GroupNoteRevisionId == revisionId && item.GroupNoteId == noteId &&
                item.StudyGroupId == groupId && item.CreatedAtUtc >= cutoff &&
                db.GroupNotes.Any(note => note.GroupNoteId == item.GroupNoteId && note.StudyGroupId == groupId))
            .FirstOrDefaultAsync(ct);
        if (revision is null) throw new GroupNotesException(404, "Không tìm thấy phiên bản trong thời hạn khôi phục.");
        var (_, delta) = MakeSafeTextCopy(revision.ContentDeltaJson);
        var now = DateTime.UtcNow;
        var copy = new GroupNote
        {
            StudyGroupId = groupId,
            OwnerUserId = currentUser.UserId,
            Title = ValidateTitle($"{revision.Title[..Math.Min(revision.Title.Length, 188)]} (khôi phục)"),
            ContentDeltaJson = delta,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.GroupNotes.Add(copy);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(CreateAudit("GroupNote.RevisionRestoredAsCopy", "GroupNote", copy.GroupNoteId.ToString(),
            $"sourceGroupNote={noteId}; revision={revisionId}; group={groupId}"));
        await StageRevisionAsync(copy, "RestoredAsCopy", ct);
        var eventQueued = await StageEventAsync(groupId, "GroupNotesChanged", new { noteId = copy.GroupNoteId }, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (!eventQueued) await NotifyGroupAsync(groupId, "GroupNotesChanged", new { noteId = copy.GroupNoteId }, ct);
        return new GroupSharedNote(copy.GroupNoteId, copy.Title, copy.ContentDeltaJson,
            copy.OwnerUserId, false, Convert.ToBase64String(copy.RowVersion), copy.UpdatedAtUtc);
    }

    public async Task<long> CopyGroupNoteToPersonalAsync(long groupId, long noteId, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        var source = await db.GroupNotes.AsNoTracking().FirstOrDefaultAsync(note =>
            note.GroupNoteId == noteId && note.StudyGroupId == groupId && !note.IsDeleted, ct);
        if (source is null) throw new GroupNotesException(404, "GroupNote không còn khả dụng.");
        var (body, delta) = MakeSafeTextCopy(source.ContentDeltaJson);
        var now = DateTime.UtcNow;
        var copy = new Note
        {
            OwnerUserId = currentUser.UserId,
            Title = source.Title,
            Body = body,
            BodyFormat = "PlainText",
            Visibility = "Private",
            ContentDeltaJson = delta,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Notes.Add(copy);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(CreateAudit("GroupNote.CopiedToPersonal", "GroupNote", noteId.ToString(),
            $"personalNote={copy.NoteId}; group={groupId}"));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return copy.NoteId;
    }

    private static (string Body, string Delta) MakeSafeTextCopy(string sourceDelta)
    {
        try
        {
            using var parsed = JsonDocument.Parse(sourceDelta);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
                !parsed.RootElement.TryGetProperty("ops", out var operations) ||
                operations.ValueKind != JsonValueKind.Array)
                throw new GroupNotesException(409, "Nội dung GroupNote không thể sao chép an toàn.");
            var body = new StringBuilder();
            foreach (var operation in operations.EnumerateArray())
            {
                if (operation.ValueKind != JsonValueKind.Object ||
                    !operation.TryGetProperty("insert", out var insert) || insert.ValueKind != JsonValueKind.String)
                    throw new GroupNotesException(409, "Ghi chú chứa nội dung nhúng; chưa thể sao chép an toàn.");
                body.Append(insert.GetString());
            }
            var text = body.ToString().TrimEnd('\n');
            var delta = JsonSerializer.Serialize(new { ops = new[] { new { insert = text + "\n" } } });
            if (Encoding.UTF8.GetByteCount(delta) > MaxDeltaBytes)
                throw new GroupNotesException(409, "Bản sao vượt quá giới hạn 2 MB.");
            return (text, delta);
        }
        catch (JsonException)
        {
            throw new GroupNotesException(409, "Nội dung GroupNote không thể sao chép an toàn.");
        }
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

    public async Task<bool> SetGroupNoteArchiveAsync(
        long groupId, long noteId, bool archived, string? versionToken, CancellationToken ct)
    {
        await RequireActiveMemberAsync(groupId, ct);
        var note = await db.GroupNotes.FirstOrDefaultAsync(item => item.GroupNoteId == noteId &&
            item.StudyGroupId == groupId && !item.IsDeleted, ct);
        if (note is null) return false;
        if (!IsContentOwner(groupId, note.OwnerUserId))
            throw new GroupNotesException(403, "Chỉ chủ sở hữu mới được lưu trữ ghi chú.");
        RequireVersion(note.RowVersion, versionToken);
        note.IsArchived = archived;
        note.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditLogs.Add(CreateAudit(archived ? "GroupNote.Archived" : "GroupNote.Unarchived",
            "GroupNote", noteId.ToString(), $"group={groupId}"));
        await StageRevisionAsync(note, archived ? "Archived" : "Unarchived", ct);
        var archiveEventQueued = await StageEventAsync(groupId, "GroupNotesChanged", new { noteId }, ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new GroupNotesException(409, "GroupNote đã đổi trạng thái ở nơi khác. Tải lại trước khi lưu.");
        }
        if (!archiveEventQueued) await NotifyGroupAsync(groupId, "GroupNotesChanged", new { noteId }, ct);
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
        var shareId = await db.GroupDocumentShares.AsNoTracking()
            .Where(item => item.DocumentId == documentId && item.StudyGroupId == groupId && item.IsActive)
            .Select(item => (long?)item.GroupDocumentShareId).FirstOrDefaultAsync(ct);
        var exists = await db.Documents.AsNoTracking().AnyAsync(item => item.DocumentId == documentId &&
            !item.IsDeleted && !item.IsArchived && item.OwnerUser.Status == "Active" &&
            db.GroupMembers.Any(member => member.StudyGroupId == groupId &&
                member.UserId == item.OwnerUserId && member.Status == "Active"), ct);
        if (shareId is null || !exists) throw new GroupNotesException(404, "PDF chưa được chia sẻ cho nhóm hoặc không còn khả dụng.");
        return shareId.Value;
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

    private async Task<GroupAnnotationSession> GetOrCreateAnnotationSessionAsync(long shareId, long versionId, CancellationToken ct)
    {
        var existing = await db.GroupAnnotationSessions.FirstOrDefaultAsync(item =>
            item.GroupDocumentShareId == shareId && item.DocumentVersionId == versionId, ct);
        if (existing is not null) return existing;
        var created = new GroupAnnotationSession
        {
            GroupDocumentShareId = shareId, DocumentVersionId = versionId,
            PayloadJson = "[]", UpdatedAtUtc = DateTime.UtcNow
        };
        db.GroupAnnotationSessions.Add(created);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(created).State = EntityState.Detached;
            return await db.GroupAnnotationSessions.FirstAsync(item =>
                item.GroupDocumentShareId == shareId && item.DocumentVersionId == versionId, ct);
        }
        return created;
    }

    private async Task<bool> HasSessionTableAsync(CancellationToken ct) =>
        await db.Database.SqlQueryRaw<int>(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.GroupAnnotationSessions', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
            .SingleAsync(ct) == 1;

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

    private async Task<bool> HasRevisionTableAsync(CancellationToken ct) =>
        await db.Database.SqlQueryRaw<int>(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.GroupNoteRevisions', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
            .SingleAsync(ct) == 1;

    private async Task StageRevisionAsync(GroupNote note, string operation, CancellationToken ct)
    {
        if (!await HasRevisionTableAsync(ct)) return;
        db.GroupNoteRevisions.Add(new GroupNoteRevision
        {
            GroupNoteId = note.GroupNoteId,
            StudyGroupId = note.StudyGroupId,
            ActorUserId = currentUser.UserId,
            Operation = operation,
            Title = note.Title,
            ContentDeltaJson = note.ContentDeltaJson,
            IsDeleted = note.IsDeleted,
            IsArchived = note.IsArchived,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private async Task<bool> StageEventAsync(long groupId, string eventName, object payload, CancellationToken ct)
    {
        var installed = await db.Database.SqlQueryRaw<int>(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.NotesOutboxEvents', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
            .SingleAsync(ct) == 1;
        if (!installed) return false;
        db.NotesOutboxEvents.Add(new NotesOutboxEvent
        {
            StudyGroupId = groupId,
            EventName = eventName,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAtUtc = DateTime.UtcNow
        });
        return true;
    }

    private async Task NotifyGroupAsync(long groupId, string eventName, object payload, CancellationToken ct)
    {
        try
        {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload));
            await NotesEventDelivery.SendToActiveMembersAsync(db, notesHub, groupId, eventName, json.RootElement, ct);
        }
        catch (Exception exception)
        {
            // The SQL commit already succeeded. A transport failure must not turn a saved write into an error.
            logger.LogWarning(exception, "Notes notification failed for group {GroupId}", groupId);
        }
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
    IReadOnlyList<GroupChangeRequestItem> PendingRequests, IReadOnlyList<GroupDeletedNote> DeletedNotes,
    bool RecoveryAvailable);
public sealed record GroupDeletedNote(long GroupNoteId, string Title, string VersionToken, DateTime DeletedAtUtc);
public sealed record DocumentShareStatus(long GroupId, string GroupName);
public sealed record GroupNoteRevisionItem(long RevisionId, string Title, string Operation,
    long? ActorUserId, bool IsDeleted, bool IsArchived, DateTime CreatedAtUtc);
public sealed record GroupSharedDocument(long DocumentId, string Title, string FileName, long SizeBytes,
    long VersionId, bool IsArchived, bool CanManage, string? AnnotationVersionToken);
public sealed record GroupSharedNote(long GroupNoteId, string Title, string ContentDeltaJson, long OwnerUserId,
    bool IsArchived, string VersionToken, DateTime UpdatedAtUtc);
public sealed record GroupChangeRequestItem(long Id, string TargetType, string Operation, long? TargetGroupNoteId,
    long? DocumentVersionId, string? ProposedTitle, long RequesterUserId, string Status, DateTime CreatedAtUtc,
    long? ReviewOwnerUserId = null, string? ProposedPreview = null);
public sealed record GroupPdfDetails(long DocumentId, string Title, long VersionId, string FileName,
    long SizeBytes, int? PageCount, bool IsArchived, bool CanManage, long ShareId);
public sealed record GroupAnnotationData(string PayloadJson, string VersionToken, bool IsArchived,
    bool CanManage, int? PageCount, long ShareId);
public sealed record GroupNoteMutation(string? Title, string? ContentDeltaJson, string? BaseVersionToken);
public sealed record GroupNoteMutationResult(bool Applied, long? RequestId, GroupSharedNote? Note);
public sealed record GroupChangeRequestInput(string? TargetType, string? Operation, long? TargetId,
    long? DocumentVersionId, string? ProposedTitle, string? ProposedPayloadJson, string? BaseVersionToken);
public sealed record ChangeDecisionResult(string Status, GroupSharedNote? CreatedNote);
