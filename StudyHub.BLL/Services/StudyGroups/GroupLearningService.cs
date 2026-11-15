using StudyHub.DAL.Repositories.Common;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.BLL.Services.StudyGroups.Models;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed class GroupLearningService(IGroupLearningWorkflow workflow, ICurrentUser currentUser, IRepository data)
{
    public Task<GroupLearningData> GetAsync(long groupId, DateOnly? week, CancellationToken ct)
    {
        var date = week ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var monday = date.AddDays(-((int)date.DayOfWeek + 6) % 7);
        return Run(() => workflow.GetAsync(groupId, currentUser.UserId, monday, ct));
    }

    public Task SaveSessionAsync(long groupId, long id, SaveGroupSessionRequest r, CancellationToken ct)
    {
        if (r.EndsAtUtc <= r.StartsAtUtc || r.EndsAtUtc - r.StartsAtUtc > TimeSpan.FromHours(24))
            throw Invalid("Buổi học phải kết thúc sau lúc bắt đầu và không dài quá 24 giờ.");
        return Run(() => workflow.SaveSessionAsync(groupId, currentUser.UserId, new GroupStudySession
        {
            Id = id, Title = Text(r.Title, 200, true), Objective = Text(r.Objective, 1000, true),
            Preparation = Text(r.Preparation, 4000), Location = Text(r.Location, 300), MeetingUrl = WebUrl(r.MeetingUrl),
            StartsAtUtc = r.StartsAtUtc.ToUniversalTime(), EndsAtUtc = r.EndsAtUtc.ToUniversalTime(), Revision = r.Revision
        }, ct));
    }

    public Task ChangeSessionAsync(long groupId, long id, ChangeGroupSessionRequest r, CancellationToken ct) =>
        Run(() => workflow.ChangeSessionAsync(groupId, currentUser.UserId, id, r.Status,
            Text(r.Summary, 4000, r.Status == "Completed"), r.Revision, ct));
    public Task RegisterAsync(long groupId, long id, bool registered, CancellationToken ct) =>
        Run(() => workflow.RegisterAsync(groupId, currentUser.UserId, id, registered, ct));
    public Task MarkAttendanceAsync(long groupId, long id, long targetUserId, bool attended, CancellationToken ct) =>
        Run(() => workflow.MarkAttendanceAsync(groupId, currentUser.UserId, id, targetUserId, attended, ct));

    public Task SaveAssignmentAsync(long groupId, long id, SaveGroupAssignmentRequest r, CancellationToken ct)
    {
        if (r.AssigneeIds?.Count > 500) throw Invalid("Mỗi lần giao bài tối đa 500 người.");
        return Run(() => workflow.SaveAssignmentAsync(groupId, currentUser.UserId, new GroupAssignment
        {
            Id = id, Title = Text(r.Title, 200, true), Description = Text(r.Description, 8000, true),
            SessionId = r.SessionId, DueAtUtc = r.DueAtUtc.ToUniversalTime(),
            AllowLateSubmission = r.AllowLateSubmission, Revision = r.Revision
        }, r.AssigneeIds ?? [], ct));
    }
    public Task CloseAssignmentAsync(long groupId, long id, int revision, CancellationToken ct) =>
        Run(() => workflow.CloseAssignmentAsync(groupId, currentUser.UserId, id, revision, ct));
    public Task SubmitAsync(long groupId, long id, SubmitGroupAssignmentRequest r, CancellationToken ct)
    {
        var content = Text(r.Content, 12000);
        var url = WebUrl(r.ResourceUrl);
        if (content.Length == 0 && url == null) throw Invalid("Hãy nhập lời giải hoặc liên kết bài làm.");
        return Run(() => workflow.SubmitAsync(groupId, currentUser.UserId, id, content, url, r.ExpectedVersion, ct));
    }
    public Task ReviewAsync(long groupId, long id, long submissionId, ReviewGroupSubmissionRequest r, CancellationToken ct) =>
        Run(() => workflow.ReviewAsync(groupId, currentUser.UserId, id, submissionId, r.Status,
            Text(r.Feedback, 4000, r.Status == "NeedsRevision"), ct));
    public Task ShareAsync(long groupId, string kind, long id, bool shared, CancellationToken ct) =>
        Run(() => workflow.ShareAsync(groupId, currentUser.UserId, kind, id, shared, ct));
    public Task<SharedContentData> ReadResourceAsync(long groupId, string kind, long id, CancellationToken ct) =>
        Run(() => workflow.ReadResourceAsync(groupId, currentUser.UserId, kind, id, ct));
    public Task SaveGoalAsync(long groupId, SaveGroupGoalRequest r, CancellationToken ct)
    {
        if (r.WeekStart.DayOfWeek != DayOfWeek.Monday || r.TargetSessions is < 0 or > 100 ||
            r.TargetAssignments is < 0 or > 100 || r.TargetSessions + r.TargetAssignments == 0)
            throw Invalid("Tuần bắt đầu vào thứ Hai; mỗi chỉ tiêu từ 0–100 và cần ít nhất một chỉ tiêu lớn hơn 0.");
        return Run(() => workflow.SaveGoalAsync(groupId, currentUser.UserId, new GroupWeeklyGoal
        {
            WeekStart = r.WeekStart, Title = Text(r.Title, 200, true), TargetSessions = r.TargetSessions,
            TargetAssignments = r.TargetAssignments, Revision = r.Revision
        }, ct));
    }
    public Task AskAsync(long groupId, AskGroupQuestionRequest r, CancellationToken ct) =>
        Run(() => workflow.AskAsync(groupId, currentUser.UserId, new GroupQuestion
        {
            Title = Text(r.Title, 200, true), Subject = Text(r.Subject, 150, true), Body = Text(r.Body, 8000, true)
        }, ct));
    public Task AnswerAsync(long groupId, long id, AnswerGroupQuestionRequest r, CancellationToken ct) =>
        Run(() => workflow.AnswerAsync(groupId, currentUser.UserId, id, Text(r.Body, 8000, true), ct));
    public Task AcceptAnswerAsync(long groupId, long id, AcceptGroupAnswerRequest r, CancellationToken ct) =>
        Run(() => workflow.AcceptAnswerAsync(groupId, currentUser.UserId, id, r.AnswerId, r.Revision, ct));

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
    private async Task Run(Func<Task> action) => await Run(async () => { await action(); return true; });
    private async Task<T> Run<T>(Func<Task<T>> action)
    {
        try { return await data.ExecuteAsync(action); }
        catch (StudyGroupWorkflowException e)
        {
            throw new StudyGroupException(e.Code, e.Message, e.Code switch { "not_found" => 404, "forbidden" => 403, "update_conflict" => 409, _ => 400 });
        }
        catch (DataConflictException exception) { throw new StudyGroupException("update_conflict", exception.Message, 409); }
    }
}
