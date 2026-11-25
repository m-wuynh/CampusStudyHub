using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.DTOs.StudyGroups;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using System.Data;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed class GroupLearningService(ICurrentUser currentUser, IRepository data) : IGroupLearningService
{
    public Task<GroupLearningData> GetAsync(long groupId, DateOnly? week, CancellationToken ct)
    {
        var date = week ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var monday = date.AddDays(-((int)date.DayOfWeek + 6) % 7);
        return Run(() => GetAsyncCore(groupId, currentUser.UserId, monday, ct));
    }

    public Task CreateSessionAsync(long groupId, CreateGroupSessionRequest r, CancellationToken ct)
    {
        if (r.EndsAtUtc <= r.StartsAtUtc || r.EndsAtUtc - r.StartsAtUtc > TimeSpan.FromHours(24))
            throw Invalid("Buổi học phải kết thúc sau lúc bắt đầu và không dài quá 24 giờ.");
        return Run(() => CreateSessionAsyncCore(groupId, currentUser.UserId, new GroupStudySession
        {
            Title = Text(r.Title, 200, true), Objective = Text(r.Objective, 1000, true),
            Preparation = Text(r.Preparation, 4000), Location = Text(r.Location, 300), MeetingUrl = WebUrl(r.MeetingUrl),
            StartsAtUtc = r.StartsAtUtc.ToUniversalTime(), EndsAtUtc = r.EndsAtUtc.ToUniversalTime()
        }, ct));
    }

    public Task ChangeSessionAsync(long groupId, long id, ChangeGroupSessionRequest r, CancellationToken ct) =>
        Run(() => ChangeSessionAsyncCore(groupId, currentUser.UserId, id, r.Status,
            Text(r.Summary, 4000, r.Status == "Completed"), r.Revision, ct));
    public Task RegisterAsync(long groupId, long id, bool registered, CancellationToken ct) =>
        Run(() => RegisterAsyncCore(groupId, currentUser.UserId, id, registered, ct));
    public Task MarkAttendanceAsync(long groupId, long id, MarkGroupAttendanceRequest request, CancellationToken ct)
    {
        var entries = request.Entries;
        if (entries is null || entries.Count == 0 ||
            entries.Any(entry => entry.UserId <= 0 || entry.Attended is null) ||
            entries.Select(entry => entry.UserId).Distinct().Count() != entries.Count)
            throw Invalid("Danh sách điểm danh không hợp lệ. Hãy chọn Có mặt hoặc Vắng mặt cho từng người.");
        return Run(() => MarkAttendanceAsyncCore(groupId, currentUser.UserId, id, entries, ct));
    }

    public Task SaveAssignmentAsync(long groupId, long id, SaveGroupAssignmentRequest r, CancellationToken ct)
    {
        if (r.AssigneeIds?.Count > 500) throw Invalid("Mỗi lần giao bài tối đa 500 người.");
        return Run(() => SaveAssignmentAsyncCore(groupId, currentUser.UserId, new GroupAssignment
        {
            Id = id, Title = Text(r.Title, 200, true), Description = Text(r.Description, 8000, true),
            SessionId = r.SessionId, DueAtUtc = r.DueAtUtc.ToUniversalTime(),
            AllowLateSubmission = r.AllowLateSubmission, Revision = r.Revision
        }, r.AssigneeIds ?? [], ct));
    }
    public Task CloseAssignmentAsync(long groupId, long id, int revision, CancellationToken ct) =>
        Run(() => CloseAssignmentAsyncCore(groupId, currentUser.UserId, id, revision, ct));
    public Task SubmitAsync(long groupId, long id, SubmitGroupAssignmentRequest r, CancellationToken ct)
    {
        var content = Text(r.Content, 12000);
        var url = WebUrl(r.ResourceUrl);
        if (content.Length == 0 && url == null) throw Invalid("Hãy nhập lời giải hoặc liên kết bài làm.");
        return Run(() => SubmitAsyncCore(groupId, currentUser.UserId, id, content, url, r.ExpectedVersion, ct));
    }
    public Task ReviewAsync(long groupId, long id, long submissionId, ReviewGroupSubmissionRequest r, CancellationToken ct) =>
        Run(() => ReviewAsyncCore(groupId, currentUser.UserId, id, submissionId, r.Status,
            Text(r.Feedback, 4000, r.Status == "NeedsRevision"), ct));
    public Task ShareAsync(long groupId, string kind, long id, bool shared, CancellationToken ct) =>
        Run(() => ShareAsyncCore(groupId, currentUser.UserId, kind, id, shared, ct));
    public Task<SharedContentData> ReadResourceAsync(long groupId, string kind, long id, CancellationToken ct) =>
        Run(() => ReadResourceAsyncCore(groupId, currentUser.UserId, kind, id, ct));
    public Task AskAsync(long groupId, AskGroupQuestionRequest r, CancellationToken ct) =>
        Run(() => AskAsyncCore(groupId, currentUser.UserId, new GroupQuestion
        {
            Title = Text(r.Title, 200, true), Subject = Text(r.Subject, 150, true), Body = Text(r.Body, 8000, true)
        }, ct));
    public Task AnswerAsync(long groupId, long id, AnswerGroupQuestionRequest r, CancellationToken ct) =>
        Run(() => AnswerAsyncCore(groupId, currentUser.UserId, id, Text(r.Body, 8000, true), ct));
    public Task AcceptAnswerAsync(long groupId, long id, AcceptGroupAnswerRequest r, CancellationToken ct) =>
        Run(() => AcceptAnswerAsyncCore(groupId, currentUser.UserId, id, r.AnswerId, r.Revision, ct));

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

    private static StudyGroupWorkflowException Missing() => new("not_found", "Không tìm thấy dữ liệu trong nhóm này.");
    private static StudyGroupWorkflowException Conflict(string message) => new("update_conflict", message);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new StudyGroupWorkflowException("invalid_action", message);
    }

    private async Task<bool> AccessAsync(long groupId, long userId, bool manage, CancellationToken ct)
    {
        var member = await data.Query<GroupMember>(false).Include(x => x.StudyGroup)
            .SingleOrDefaultAsync(x => x.StudyGroupId == groupId && x.UserId == userId, ct);
        if (member?.Status != "Active" || member.StudyGroup.IsArchived)
            throw new StudyGroupWorkflowException("forbidden", "Chỉ thành viên đang hoạt động được truy cập nội dung nhóm.");
        var canManage = member.StudyGroup.OwnerUserId == userId || member.MemberRole == "Moderator";
        if (manage && !canManage)
            throw new StudyGroupWorkflowException("forbidden", "Chỉ Owner hoặc Moderator được thực hiện thao tác này.");
        return canManage;
    }

    // Authorization, state checks and writes share one transaction to prevent stale membership/state races.
    private async Task WriteAsync(long groupId, long userId, bool manage, Func<bool, Task> action, CancellationToken ct)
    {
        await using var tx = await data.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var canManage = await AccessAsync(groupId, userId, manage, ct);
        await action(canManage);
        await data.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static void CheckRevision(int actual, int expected)
    {
        if (actual != expected) throw Conflict("Dữ liệu đã thay đổi. Hãy tải lại trước khi lưu.");
    }

    private Task CreateSessionAsyncCore(long groupId, long userId, GroupStudySession input, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            var entity = new GroupStudySession { StudyGroupId = groupId, CreatedByUserId = userId };
            Require(input.StartsAtUtc > DateTimeOffset.UtcNow, "Thời gian bắt đầu phải ở tương lai.");
            entity.Title = input.Title; entity.Objective = input.Objective; entity.Preparation = input.Preparation;
            entity.Location = input.Location; entity.MeetingUrl = input.MeetingUrl;
            entity.StartsAtUtc = input.StartsAtUtc; entity.EndsAtUtc = input.EndsAtUtc;
            await data.AddAsync(entity);
            await data.SaveChangesAsync(ct);
            await SyncSessionCalendarAsync(entity, userId, true, ct);
        }, ct);

    private Task ChangeSessionAsyncCore(long groupId, long userId, long sessionId, string status, string summary, int revision, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            var session = await data.Query<GroupStudySession>(false).SingleOrDefaultAsync(x => x.Id == sessionId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            CheckRevision(session.Revision, revision);
            Require(session.Status == "Scheduled", "Buổi học đã kết thúc hoặc bị hủy.");
            Require(status == "Completed", "Không được hủy buổi học đã tạo.");
            Require(session.StartsAtUtc <= DateTimeOffset.UtcNow, "Buổi học chưa bắt đầu.");
            session.Status = status; session.Summary = summary; session.Revision++;
        }, ct);

    private Task RegisterAsyncCore(long groupId, long userId, long sessionId, bool registered, CancellationToken ct) =>
        WriteAsync(groupId, userId, false, async _ =>
        {
            var session = await data.Query<GroupStudySession>(false).Include(x => x.Attendees)
                .SingleOrDefaultAsync(x => x.Id == sessionId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            Require(session.Status == "Scheduled" && session.StartsAtUtc > DateTimeOffset.UtcNow, "Đã đóng đăng ký cho buổi học này.");
            Require(session.CreatedByUserId != userId, "Người tạo là giảng viên của buổi học, không đăng ký với vai trò người học.");
            RemoveLegacyCreatorAttendance(session);
            var attendee = session.Attendees.SingleOrDefault(x => x.UserId == userId);
            if (registered && attendee is null) session.Attendees.Add(new GroupSessionAttendance { UserId = userId });
            if (!registered && attendee is not null) data.Remove(attendee);
            await SyncSessionCalendarAsync(session, userId, registered, ct);
        }, ct);

    private async Task SyncSessionCalendarAsync(GroupStudySession session, long userId, bool registered, CancellationToken ct)
    {
        var calendarEvent = await data.Query<ScheduleEvent>(false)
            .SingleOrDefaultAsync(item => item.UserId == userId && item.GroupStudySessionId == session.Id, ct);
        var now = DateTime.UtcNow;
        if (!registered)
        {
            if (calendarEvent is null) return;
            calendarEvent.IsDeleted = true;
            calendarEvent.UpdatedAtUtc = now;
            var reminders = await data.Query<Reminder>(false)
                .Where(item => item.ScheduleEventId == calendarEvent.ScheduleEventId &&
                    (item.Status == "Pending" || item.Status == "Processing")).ToListAsync(ct);
            foreach (var reminder in reminders) reminder.Status = "Cancelled";
            return;
        }

        if (calendarEvent is null)
        {
            calendarEvent = new ScheduleEvent { UserId = userId, GroupStudySessionId = session.Id, CreatedAtUtc = now };
            await data.AddAsync(calendarEvent, ct);
        }
        calendarEvent.Title = session.Title;
        calendarEvent.EventType = "Class";
        calendarEvent.StartAtLocal = session.StartsAtUtc.ToOffset(TimeSpan.FromHours(7)).DateTime;
        calendarEvent.EndAtLocal = session.EndsAtUtc.ToOffset(TimeSpan.FromHours(7)).DateTime;
        calendarEvent.TimeZoneId = "SE Asia Standard Time";
        calendarEvent.RepeatMode = "None";
        calendarEvent.RepeatUntilDate = null;
        calendarEvent.Location = session.Location.Length <= 250 ? session.Location : session.Location[..250];
        calendarEvent.Description = $"Buổi học nhóm: {session.Title}\nMục tiêu: {session.Objective}\nChi tiết: /Groups/{session.StudyGroupId}?panel=sessions";
        calendarEvent.IsDeleted = false;
        calendarEvent.IsCompleted = false;
        calendarEvent.UpdatedAtUtc = now;
    }

    private Task MarkAttendanceAsyncCore(long groupId, long userId, long sessionId, IReadOnlyList<GroupAttendanceEntry> entries, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            var session = await data.Query<GroupStudySession>(false).Include(x => x.Attendees)
                .SingleOrDefaultAsync(x => x.Id == sessionId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            Require(session.Status != "Cancelled" && session.StartsAtUtc <= DateTimeOffset.UtcNow, "Chỉ điểm danh buổi học đã bắt đầu và không bị hủy.");
            var roster = session.Attendees.Where(x => x.UserId != session.CreatedByUserId)
                .Select(x => x.UserId).ToHashSet();
            Require(roster.SetEquals(entries.Select(entry => entry.UserId)),
                "Danh sách đăng ký đã thay đổi. Hãy tải lại trang trước khi lưu điểm danh.");
            RemoveLegacyCreatorAttendance(session);
            var attendanceByUser = session.Attendees.ToDictionary(x => x.UserId);
            foreach (var entry in entries)
            {
                var row = attendanceByUser[entry.UserId];
                row.Attended = entry.Attended!.Value;
            }
        }, ct);

    // Older versions enrolled the instructor as a learner. Remove that legacy row on writes.
    private void RemoveLegacyCreatorAttendance(GroupStudySession session)
    {
        var creatorAttendance = session.Attendees.SingleOrDefault(attendee => attendee.UserId == session.CreatedByUserId);
        if (creatorAttendance is null) return;
        session.Attendees.Remove(creatorAttendance);
        data.Remove(creatorAttendance);
    }

    private Task SaveAssignmentAsyncCore(long groupId, long userId, GroupAssignment input, IReadOnlyList<long> assignees, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            if (input.SessionId is not null)
                Require(await data.Query<GroupStudySession>(false).AnyAsync(x => x.Id == input.SessionId && x.StudyGroupId == groupId && x.Status != "Cancelled", ct), "Buổi học không thuộc nhóm hoặc đã bị hủy.");
            Require(input.DueAtUtc > DateTimeOffset.UtcNow, "Hạn nộp phải ở tương lai.");
            var entity = input.Id == 0 ? new GroupAssignment { StudyGroupId = groupId, CreatedByUserId = userId }
                : await data.Query<GroupAssignment>(false).SingleOrDefaultAsync(x => x.Id == input.Id && x.StudyGroupId == groupId, ct) ?? throw Missing();
            if (input.Id != 0) CheckRevision(entity.Revision, input.Revision);
            Require(entity.Status == "Open", "Bài tập đã đóng.");
            entity.Title = input.Title; entity.Description = input.Description; entity.SessionId = input.SessionId;
            entity.DueAtUtc = input.DueAtUtc; entity.AllowLateSubmission = input.AllowLateSubmission;
            if (input.Id == 0)
            {
                var members = await data.Query<GroupMember>(false).Where(x => x.StudyGroupId == groupId && x.Status == "Active").Select(x => x.UserId).ToListAsync(ct);
                Require(!assignees.Contains(userId), "Người tạo không thể tự giao bài tập cho bản thân.");
                var targets = assignees.Count == 0 ? members.Where(id => id != userId).ToList() : assignees.Distinct().ToList();
                Require(targets.Count > 0, "Chưa có thành viên khác để nhận bài tập.");
                Require(targets.All(members.Contains), "Chỉ được giao bài cho thành viên đang hoạt động.");
                foreach (var id in targets) entity.Assignees.Add(new GroupAssignmentAssignee { UserId = id });
                await data.AddAsync(entity);
            }
            else entity.Revision++;
        }, ct);

    private Task CloseAssignmentAsyncCore(long groupId, long userId, long assignmentId, int revision, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            var assignment = await data.Query<GroupAssignment>(false).SingleOrDefaultAsync(x => x.Id == assignmentId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            CheckRevision(assignment.Revision, revision);
            assignment.Status = "Closed"; assignment.Revision++;
        }, ct);

    private Task SubmitAsyncCore(long groupId, long userId, long assignmentId, string content, string? url, int expectedVersion, CancellationToken ct) =>
        WriteAsync(groupId, userId, false, async _ =>
        {
            var assignment = await data.Query<GroupAssignment>(false).SingleOrDefaultAsync(x => x.Id == assignmentId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            Require(assignment.CreatedByUserId != userId, "Người tạo bài tập không nộp bài với vai trò người học.");
            Require(assignment.Status == "Open", "Bài tập đã đóng.");
            Require(assignment.AllowLateSubmission || DateTimeOffset.UtcNow <= assignment.DueAtUtc, "Đã hết hạn nộp bài.");
            Require(await data.Query<GroupAssignmentAssignee>(false).AnyAsync(x => x.AssignmentId == assignmentId && x.UserId == userId, ct), "Bạn không được giao bài tập này.");
            var latest = await data.Query<GroupSubmission>(false).Where(x => x.AssignmentId == assignmentId && x.UserId == userId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            CheckRevision(latest?.Version ?? 0, expectedVersion);
            Require(latest?.Status != "Completed", "Bài đã hoàn thành; người quản lý phải yêu cầu sửa trước khi nộp lại.");
            await data.AddAsync(new GroupSubmission { AssignmentId = assignmentId, UserId = userId,
                Version = (latest?.Version ?? 0) + 1, Content = content, ResourceUrl = url, SubmittedAtUtc = DateTimeOffset.UtcNow });
        }, ct);

    private Task ReviewAsyncCore(long groupId, long userId, long assignmentId, long submissionId, string status, string feedback, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            Require(status is "Completed" or "NeedsRevision", "Trạng thái phản hồi không hợp lệ.");
            if (!await data.Query<GroupAssignment>(false).AnyAsync(x => x.Id == assignmentId && x.StudyGroupId == groupId, ct)) throw Missing();
            var submission = await data.Query<GroupSubmission>(false).SingleOrDefaultAsync(x => x.Id == submissionId && x.AssignmentId == assignmentId, ct) ?? throw Missing();
            if (await data.Query<GroupSubmission>(false).AnyAsync(x => x.AssignmentId == assignmentId && x.UserId == submission.UserId && x.Version > submission.Version, ct))
                throw Conflict("Đã có bài nộp mới hơn. Hãy tải lại để nhận xét phiên bản mới nhất.");
            submission.Status = status; submission.Feedback = feedback;
            submission.ReviewedByUserId = userId; submission.ReviewedAtUtc = DateTimeOffset.UtcNow;
        }, ct);

    private Task AskAsyncCore(long groupId, long userId, GroupQuestion question, CancellationToken ct) =>
        WriteAsync(groupId, userId, false, async _ =>
        {
            question.StudyGroupId = groupId; question.AuthorUserId = userId; question.CreatedAtUtc = DateTimeOffset.UtcNow;
            await data.AddAsync(question);

        }, ct);

    private Task AnswerAsyncCore(long groupId, long userId, long questionId, string body, CancellationToken ct) =>
        WriteAsync(groupId, userId, false, async _ =>
        {
            if (!await data.Query<GroupQuestion>(false).AnyAsync(x => x.Id == questionId && x.StudyGroupId == groupId, ct)) throw Missing();
            await data.AddAsync(new GroupAnswer { QuestionId = questionId, AuthorUserId = userId, Body = body, CreatedAtUtc = DateTimeOffset.UtcNow });
        }, ct);

    private Task AcceptAnswerAsyncCore(long groupId, long userId, long questionId, long? answerId, int revision, CancellationToken ct) =>
        WriteAsync(groupId, userId, false, async canManage =>
        {
            var question = await data.Query<GroupQuestion>(false).SingleOrDefaultAsync(x => x.Id == questionId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            if (!canManage && question.AuthorUserId != userId) throw new StudyGroupWorkflowException("forbidden", "Chỉ người hỏi hoặc quản lý được chọn câu trả lời.");
            CheckRevision(question.Revision, revision);
            if (answerId is not null && !await data.Query<GroupAnswer>(false).AnyAsync(x => x.Id == answerId && x.QuestionId == questionId, ct)) throw Missing();
            question.AcceptedAnswerId = answerId; question.Revision++;
        }, ct);


    private async Task<GroupLearningData> GetAsyncCore(long groupId, long userId, DateOnly weekStart, CancellationToken ct)
    {
        var manage = await AccessAsync(groupId, userId, false, ct);
        var members = await data.Query<GroupMember>(false).AsNoTracking().Where(x => x.StudyGroupId == groupId && x.Status == "Active")
            .OrderBy(x => x.User.DisplayName).Select(x => new LearningMember(x.UserId, x.User.DisplayName)).ToListAsync(ct);
        var sessions = await data.Query<GroupStudySession>(false).AsNoTracking().Where(x => x.StudyGroupId == groupId)
            .Include(x => x.Attendees).ThenInclude(x => x.User).AsSplitQuery().OrderByDescending(x => x.StartsAtUtc).ToListAsync(ct);
        foreach (var session in sessions)
        {
            // Detached projection also excludes instructors from legacy sessions and progress totals.
            session.Attendees = session.Attendees.Where(attendee => attendee.UserId != session.CreatedByUserId).ToList();
        }
        var assignments = await data.Query<GroupAssignment>(false).AsNoTracking().Where(x => x.StudyGroupId == groupId)
            .Include(x => x.Assignees).ThenInclude(x => x.User)
            .Include(x => x.Assignees).ThenInclude(x => x.Submissions).AsSplitQuery().OrderByDescending(x => x.DueAtUtc).ToListAsync(ct);
        foreach (var assignment in assignments)
            assignment.Assignees = assignment.Assignees.Where(assignee => assignee.UserId != assignment.CreatedByUserId).ToList();
        var reviewerIds = assignments.SelectMany(assignment => assignment.Assignees)
            .SelectMany(assignee => assignee.Submissions)
            .Where(submission => submission.ReviewedByUserId.HasValue)
            .Select(submission => submission.ReviewedByUserId!.Value).Distinct().ToList();
        var reviewerNames = await data.Query<User>().Where(user => reviewerIds.Contains(user.UserId))
            .ToDictionaryAsync(user => user.UserId, user => user.DisplayName, ct);
        var questions = await data.Query<GroupQuestion>(false).AsNoTracking().Where(x => x.StudyGroupId == groupId)
            .Include(x => x.Author).Include(x => x.Answers).ThenInclude(x => x.Author)
            .AsSplitQuery().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var resources = await ResourcesAsync(groupId, userId, false, ct);
        var available = await ResourcesAsync(groupId, userId, true, ct);

        // Weekly reporting uses Vietnam's calendar boundaries, not the server's local time.
        var start = new DateTimeOffset(weekStart.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
        var end = start.AddDays(7);
        var weekSessions = sessions.Where(x => x.StartsAtUtc >= start && x.StartsAtUtc < end && x.Status == "Completed").ToList();
        var weekAssignments = assignments.Where(x => x.DueAtUtc >= start && x.DueAtUtc < end).ToList();
        var latest = weekAssignments.SelectMany(x => x.Assignees).Select(x => x.Submissions.OrderByDescending(s => s.Version).FirstOrDefault()).Where(x => x != null).ToList();
        var progressMembers = members.Select(member =>
        {
            var assigned = weekAssignments.SelectMany(x => x.Assignees).Where(x => x.UserId == member.UserId).ToList();
            var submitted = assigned.Select(x => x.Submissions.OrderByDescending(s => s.Version).FirstOrDefault()).Where(x => x != null).ToList();
            return new MemberProgressData(member.UserId, member.Name,
                weekSessions.Count(x => x.Attendees.Any(a => a.UserId == member.UserId)),
                weekSessions.Count(x => x.Attendees.Any(a => a.UserId == member.UserId && a.Attended)),
                assigned.Count, submitted.Count);
        }).Where(x => manage || x.UserId == userId).ToList();
        var progress = new LearningProgressData(weekStart, weekSessions.Count,
            weekAssignments.Sum(x => x.Assignees.Count), latest.Count, progressMembers);
        return new GroupLearningData(userId, manage, members,
            sessions.Select(x => new SessionData(x.Id, x.Title, x.Objective, x.Preparation, x.Location,
                x.CreatedByUserId == userId || x.Attendees.Any(a => a.UserId == userId) ? x.MeetingUrl : null,
                x.StartsAtUtc, x.EndsAtUtc, x.Status, x.Summary, x.Revision,
                x.Attendees.Select(a => new AttendanceData(a.UserId, a.User.DisplayName, a.Attended)).ToList(), x.CreatedByUserId)).ToList(),
            assignments.Select(x => new AssignmentData(x.Id, x.Title, x.Description, x.SessionId, x.DueAtUtc,
                x.AllowLateSubmission, x.Status, x.Revision,
                x.Assignees.Select(a => new LearningMember(a.UserId, a.User.DisplayName)).ToList(),
                x.Assignees.SelectMany(a => a.Submissions.Select(s =>
                    new SubmissionData(s.Id, s.UserId, a.User.DisplayName, s.Version, s.Content, s.ResourceUrl,
                        s.SubmittedAtUtc, s.SubmittedAtUtc > x.DueAtUtc, s.Status, s.Feedback,
                        s.ReviewedByUserId is long reviewerId ? reviewerNames.GetValueOrDefault(reviewerId) : null,
                        s.ReviewedAtUtc))).OrderBy(s => s.SubmittedAtUtc).ToList(), x.CreatedByUserId)).ToList(),
            questions.Select(x => new QuestionData(x.Id, x.AuthorUserId, x.Author.DisplayName, x.Title, x.Subject,
                x.Body, x.CreatedAtUtc, x.AcceptedAnswerId, x.Revision,
                x.Answers.OrderBy(a => a.CreatedAtUtc).Select(a => new AnswerData(a.Id, a.AuthorUserId, a.Author.DisplayName, a.Body, a.CreatedAtUtc)).ToList())).ToList(),
            resources, available, progress);
    }

    private async Task<List<SharedResourceData>> ResourcesAsync(
    long groupId,
    long userId,
    bool available,
    CancellationToken ct)
{
    var decks = data.Query<FlashcardDeck>(false)
        .AsNoTracking()
        .Where(x => !x.IsDeleted);

    decks = available
        ? decks.Where(x =>
            x.OwnerUserId == userId &&
            x.StudyGroupId == null)
        : decks.Where(x =>
            x.StudyGroupId == groupId &&
            x.Visibility == "Group");

    var result = await decks
        .Select(x => new SharedResourceData(
            x.DeckId,
            "Flashcard",
            x.Title,
            x.OwnerUserId,
            x.OwnerUser.DisplayName))
        .ToListAsync(ct);

    return result
        .OrderBy(x => x.Title)
        .ToList();
}

    private Task ShareAsyncCore(
    long groupId,
    long userId,
    string kind,
    long resourceId,
    bool shared,
    CancellationToken ct) =>
    WriteAsync(
        groupId,
        userId,
        false,
        async manage =>
        {
            if (!string.Equals(kind, "Flashcard", StringComparison.OrdinalIgnoreCase))
            {
                throw Missing();
            }

            var deck = await data.Query<FlashcardDeck>(false)
                .SingleOrDefaultAsync(
                    x => x.DeckId == resourceId &&
                         !x.IsDeleted,
                    ct)
                ?? throw Missing();

            // Giữ nguyên điều kiện quyền của flashcard.
            if ((shared && deck.OwnerUserId != userId) ||
                (!shared &&
                 deck.OwnerUserId != userId &&
                 !manage))
            {
                throw new StudyGroupWorkflowException(
                    "forbidden",
                    "Bạn không có quyền thay đổi chia sẻ tài nguyên này.");
            }

            Require(
                shared
                    ? deck.StudyGroupId == null ||
                      deck.StudyGroupId == groupId
                    : deck.StudyGroupId == groupId,
                "Tài nguyên đã được chia sẻ ở nhóm khác hoặc không thuộc nhóm này.");

            deck.Visibility = shared ? "Group" : "Private";
            deck.StudyGroupId = shared ? groupId : null;
            deck.UpdatedAtUtc = DateTime.UtcNow;
        },
        ct);
    private static bool IsWebUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";

    private async Task<SharedContentData> ReadResourceAsyncCore(
    long groupId,
    long userId,
    string kind,
    long resourceId,
    CancellationToken ct)
{
    await AccessAsync(
        groupId,
        userId,
        false,
        ct);

    if (kind != "Flashcard")
    {
        throw Missing();
    }

    var deck = await data.Query<FlashcardDeck>(false)
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.DeckId == resourceId &&
                 x.StudyGroupId == groupId &&
                 x.Visibility == "Group" &&
                 !x.IsDeleted,
            ct)
        ?? throw Missing();

    var cards = await data.Query<Flashcard>(false)
        .AsNoTracking()
        .Where(x =>
            x.DeckId == resourceId &&
            !x.IsDeleted)
        .OrderBy(x => x.SortOrder)
        .Select(x => new ResourceCardData(
            x.FrontText,
            x.BackText))
        .ToListAsync(ct);

    return new SharedContentData(
        kind,
        deck.Title,
        deck.Description,
        null,
        null,
        cards);
}


}
