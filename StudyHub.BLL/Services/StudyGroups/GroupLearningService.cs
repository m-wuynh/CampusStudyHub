using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.StudyGroups;
using StudyHub.DAL.Repositories.StudyGroups.Models;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed class GroupLearningService(IGroupLearningRepository repository, ICurrentUser currentUser)
{
    public Task<GroupLearningData> GetAsync(long groupId, DateOnly? week, CancellationToken ct)
    {
        var date = week ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var monday = date.AddDays(-((int)date.DayOfWeek + 6) % 7);
        return Run(() => repository.GetAsync(groupId, currentUser.UserId, monday, ct));
    }

    public Task SaveSessionAsync(long groupId, long id, SaveGroupSessionRequest r, CancellationToken ct)
    {
        if (r.EndsAtUtc <= r.StartsAtUtc || r.EndsAtUtc - r.StartsAtUtc > TimeSpan.FromHours(24))
            throw Invalid("Buổi học phải kết thúc sau lúc bắt đầu và không dài quá 24 giờ.");
        return Run(() => repository.SaveSessionAsync(groupId, currentUser.UserId, new GroupStudySession
        {
            Id = id, Title = Text(r.Title, 200, true), Objective = Text(r.Objective, 1000, true),
            Preparation = Text(r.Preparation, 4000), Location = Text(r.Location, 300), MeetingUrl = WebUrl(r.MeetingUrl),
            StartsAtUtc = r.StartsAtUtc.ToUniversalTime(), EndsAtUtc = r.EndsAtUtc.ToUniversalTime(), Revision = r.Revision
        }, ct));
    }

    public Task ChangeSessionAsync(long groupId, long id, ChangeGroupSessionRequest r, CancellationToken ct) =>
        Run(() => repository.ChangeSessionAsync(groupId, currentUser.UserId, id, r.Status,
            Text(r.Summary, 4000, r.Status == "Completed"), r.Revision, ct));
    public Task RegisterAsync(long groupId, long id, bool registered, CancellationToken ct) =>
        Run(() => repository.RegisterAsync(groupId, currentUser.UserId, id, registered, ct));
    public Task MarkAttendanceAsync(long groupId, long id, long targetUserId, bool attended, CancellationToken ct) =>
        Run(() => repository.MarkAttendanceAsync(groupId, currentUser.UserId, id, targetUserId, attended, ct));

    public Task SaveAssignmentAsync(long groupId, long id, SaveGroupAssignmentRequest r, CancellationToken ct)
    {
        if (r.AssigneeIds?.Count > 500) throw Invalid("Mỗi lần giao bài tối đa 500 người.");
        return Run(() => repository.SaveAssignmentAsync(groupId, currentUser.UserId, new GroupAssignment
        {
            Id = id, Title = Text(r.Title, 200, true), Description = Text(r.Description, 8000, true),
            SessionId = r.SessionId, DueAtUtc = r.DueAtUtc.ToUniversalTime(),
            AllowLateSubmission = r.AllowLateSubmission, Revision = r.Revision
        }, r.AssigneeIds ?? [], ct));
    }
    public Task CloseAssignmentAsync(long groupId, long id, int revision, CancellationToken ct) =>
        Run(() => repository.CloseAssignmentAsync(groupId, currentUser.UserId, id, revision, ct));
    public Task SubmitAsync(long groupId, long id, SubmitGroupAssignmentRequest r, CancellationToken ct)
    {
        var content = Text(r.Content, 12000);
        var url = WebUrl(r.ResourceUrl);
        if (content.Length == 0 && url == null) throw Invalid("Hãy nhập lời giải hoặc liên kết bài làm.");
        return Run(() => repository.SubmitAsync(groupId, currentUser.UserId, id, content, url, r.ExpectedVersion, ct));
    }
    public Task ReviewAsync(long groupId, long id, long submissionId, ReviewGroupSubmissionRequest r, CancellationToken ct) =>
        Run(() => repository.ReviewAsync(groupId, currentUser.UserId, id, submissionId, r.Status,
            Text(r.Feedback, 4000, r.Status == "NeedsRevision"), ct));
    public Task ShareAsync(long groupId, string kind, long id, bool shared, CancellationToken ct) =>
        Run(() => repository.ShareAsync(groupId, currentUser.UserId, kind, id, shared, ct));
    public Task<SharedContentData> ReadResourceAsync(long groupId, string kind, long id, CancellationToken ct) =>
        Run(() => repository.ReadResourceAsync(groupId, currentUser.UserId, kind, id, ct));
    public Task SaveGoalAsync(long groupId, SaveGroupGoalRequest r, CancellationToken ct)
    {
        if (r.WeekStart.DayOfWeek != DayOfWeek.Monday || r.TargetSessions is < 0 or > 100 ||
            r.TargetAssignments is < 0 or > 100 || r.TargetSessions + r.TargetAssignments == 0)
            throw Invalid("Tuần bắt đầu vào thứ Hai; mỗi chỉ tiêu từ 0–100 và cần ít nhất một chỉ tiêu lớn hơn 0.");
        return Run(() => repository.SaveGoalAsync(groupId, currentUser.UserId, new GroupWeeklyGoal
        {
            WeekStart = r.WeekStart, Title = Text(r.Title, 200, true), TargetSessions = r.TargetSessions,
            TargetAssignments = r.TargetAssignments, Revision = r.Revision
        }, ct));
    }
    public Task AskAsync(long groupId, AskGroupQuestionRequest r, CancellationToken ct) =>
        Run(() => repository.AskAsync(groupId, currentUser.UserId, new GroupQuestion
        {
            Title = Text(r.Title, 200, true), Subject = Text(r.Subject, 150, true), Body = Text(r.Body, 8000, true)
        }, ct));
    public Task AnswerAsync(long groupId, long id, AnswerGroupQuestionRequest r, CancellationToken ct) =>
        Run(() => repository.AnswerAsync(groupId, currentUser.UserId, id, Text(r.Body, 8000, true), ct));
    public Task AcceptAnswerAsync(long groupId, long id, AcceptGroupAnswerRequest r, CancellationToken ct) =>
        Run(() => repository.AcceptAnswerAsync(groupId, currentUser.UserId, id, r.AnswerId, r.Revision, ct));

    private static StudyGroupException Invalid(string message) => new("validation", message);
    private static string Text(string? value, int max, bool required = false)
    {
        var text = value?.Trim() ?? "";
        if (required && text.Length == 0) throw Invalid("Vui lòng điền đầy đủ nội dung bắt buộc.");
        if (text.Length > max) throw Invalid($"Nội dung không được dài quá {max} ký tự.");
        return text;
    }
    private static string? WebUrl(string? value)
    {
        var url = Text(value, 2000);
        if (url.Length == 0) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw Invalid("Liên kết phải là URL HTTP hoặc HTTPS hợp lệ.");
        return url;
    }
    private static async Task Run(Func<Task> action) => await Run(async () => { await action(); return true; });
    private static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (StudyGroupRepositoryException e)
        {
            throw new StudyGroupException(e.Code, e.Message, e.Code switch { "not_found" => 404, "forbidden" => 403, "update_conflict" => 409, _ => 400 });
        }
        catch (DbUpdateConcurrencyException) { throw new StudyGroupException("update_conflict", "Dữ liệu vừa thay đổi. Hãy tải lại và thử lại.", 409); }
        catch (SqlException e) when (e.Number == 1205) { throw new StudyGroupException("update_conflict", "Có thao tác đồng thời. Hãy thử lại.", 409); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 or 1205 })
        { throw new StudyGroupException("update_conflict", "Dữ liệu vừa thay đổi hoặc đã tồn tại. Hãy tải lại.", 409); }
    }
}
