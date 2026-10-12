using System.Data;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.StudyGroups.Models;

namespace StudyHub.DAL.Repositories.StudyGroups;

public sealed partial class SqlGroupLearningRepository(StudyHubDbContext db) : IGroupLearningRepository
{
    private static StudyGroupRepositoryException Missing() => new("not_found", "Không tìm thấy dữ liệu trong nhóm này.");
    private static StudyGroupRepositoryException Conflict(string message) => new("update_conflict", message);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new StudyGroupRepositoryException("invalid_action", message);
    }

    private async Task<bool> AccessAsync(long groupId, long userId, bool manage, CancellationToken ct)
    {
        var member = await db.GroupMembers.Include(x => x.StudyGroup)
            .SingleOrDefaultAsync(x => x.StudyGroupId == groupId && x.UserId == userId, ct);
        if (member?.Status != "Active" || member.StudyGroup.IsArchived)
            throw new StudyGroupRepositoryException("forbidden", "Chỉ thành viên đang hoạt động được truy cập nội dung nhóm.");
        var canManage = member.StudyGroup.OwnerUserId == userId || member.MemberRole == "Moderator";
        if (manage && !canManage)
            throw new StudyGroupRepositoryException("forbidden", "Chỉ Owner hoặc Moderator được thực hiện thao tác này.");
        return canManage;
    }

    // Authorization, state checks and writes share one transaction to prevent stale membership/state races.
    private async Task WriteAsync(long groupId, long userId, bool manage, Func<bool, Task> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var canManage = await AccessAsync(groupId, userId, manage, ct);
        await action(canManage);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static void CheckRevision(int actual, int expected)
    {
        if (actual != expected) throw Conflict("Dữ liệu đã thay đổi. Hãy tải lại trước khi lưu.");
    }

    public Task SaveSessionAsync(long groupId, long userId, GroupStudySession input, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            var entity = input.Id == 0 ? new GroupStudySession { StudyGroupId = groupId, CreatedByUserId = userId }
                : await db.GroupStudySessions.SingleOrDefaultAsync(x => x.Id == input.Id && x.StudyGroupId == groupId, ct) ?? throw Missing();
            if (input.Id != 0) CheckRevision(entity.Revision, input.Revision);
            Require(entity.Status == "Scheduled", "Chỉ được sửa buổi học chưa kết thúc hoặc hủy.");
            Require(input.StartsAtUtc > DateTimeOffset.UtcNow, "Thời gian bắt đầu phải ở tương lai.");
            entity.Title = input.Title; entity.Objective = input.Objective; entity.Preparation = input.Preparation;
            entity.Location = input.Location; entity.MeetingUrl = input.MeetingUrl;
            entity.StartsAtUtc = input.StartsAtUtc; entity.EndsAtUtc = input.EndsAtUtc;
            if (input.Id == 0) db.GroupStudySessions.Add(entity); else entity.Revision++;
        }, ct);

    public Task ChangeSessionAsync(long groupId, long userId, long sessionId, string status, string summary, int revision, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            var session = await db.GroupStudySessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            CheckRevision(session.Revision, revision);
            Require(session.Status == "Scheduled", "Buổi học đã kết thúc hoặc bị hủy.");
            Require(status is "Completed" or "Cancelled", "Trạng thái không hợp lệ.");
            if (status == "Completed") Require(session.StartsAtUtc <= DateTimeOffset.UtcNow, "Buổi học chưa bắt đầu.");
            session.Status = status; session.Summary = summary; session.Revision++;
        }, ct);

    public Task RegisterAsync(long groupId, long userId, long sessionId, bool registered, CancellationToken ct) =>
        WriteAsync(groupId, userId, false, async _ =>
        {
            var session = await db.GroupStudySessions.Include(x => x.Attendees)
                .SingleOrDefaultAsync(x => x.Id == sessionId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            Require(session.Status == "Scheduled" && session.StartsAtUtc > DateTimeOffset.UtcNow, "Đã đóng đăng ký cho buổi học này.");
            var attendee = session.Attendees.SingleOrDefault(x => x.UserId == userId);
            if (registered && attendee is null) session.Attendees.Add(new GroupSessionAttendance { UserId = userId });
            if (!registered && attendee is not null) db.GroupSessionAttendances.Remove(attendee);
        }, ct);

    public Task MarkAttendanceAsync(long groupId, long userId, long sessionId, long targetUserId, bool attended, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            var session = await db.GroupStudySessions.Include(x => x.Attendees)
                .SingleOrDefaultAsync(x => x.Id == sessionId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            Require(session.Status != "Cancelled" && session.StartsAtUtc <= DateTimeOffset.UtcNow, "Chỉ điểm danh buổi học đã bắt đầu và không bị hủy.");
            var row = session.Attendees.SingleOrDefault(x => x.UserId == targetUserId);
            if (row is null)
            {
                Require(await db.GroupMembers.AnyAsync(x => x.StudyGroupId == groupId && x.UserId == targetUserId && x.Status == "Active", ct), "Người được điểm danh phải là thành viên nhóm.");
                row = new GroupSessionAttendance { UserId = targetUserId };
                session.Attendees.Add(row);
            }
            row.Attended = attended;
        }, ct);

    public Task SaveAssignmentAsync(long groupId, long userId, GroupAssignment input, IReadOnlyList<long> assignees, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            if (input.SessionId is not null)
                Require(await db.GroupStudySessions.AnyAsync(x => x.Id == input.SessionId && x.StudyGroupId == groupId && x.Status != "Cancelled", ct), "Buổi học không thuộc nhóm hoặc đã bị hủy.");
            Require(input.DueAtUtc > DateTimeOffset.UtcNow, "Hạn nộp phải ở tương lai.");
            var entity = input.Id == 0 ? new GroupAssignment { StudyGroupId = groupId, CreatedByUserId = userId }
                : await db.GroupAssignments.SingleOrDefaultAsync(x => x.Id == input.Id && x.StudyGroupId == groupId, ct) ?? throw Missing();
            if (input.Id != 0) CheckRevision(entity.Revision, input.Revision);
            Require(entity.Status == "Open", "Bài tập đã đóng.");
            entity.Title = input.Title; entity.Description = input.Description; entity.SessionId = input.SessionId;
            entity.DueAtUtc = input.DueAtUtc; entity.AllowLateSubmission = input.AllowLateSubmission;
            if (input.Id == 0)
            {
                var members = await db.GroupMembers.Where(x => x.StudyGroupId == groupId && x.Status == "Active").Select(x => x.UserId).ToListAsync(ct);
                var targets = assignees.Count == 0 ? members : assignees.Distinct().ToList();
                Require(targets.All(members.Contains), "Chỉ được giao bài cho thành viên đang hoạt động.");
                foreach (var id in targets) entity.Assignees.Add(new GroupAssignmentAssignee { UserId = id });
                db.GroupAssignments.Add(entity);
            }
            else entity.Revision++;
        }, ct);

    public Task CloseAssignmentAsync(long groupId, long userId, long assignmentId, int revision, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            var assignment = await db.GroupAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            CheckRevision(assignment.Revision, revision);
            assignment.Status = "Closed"; assignment.Revision++;
        }, ct);

    public Task SubmitAsync(long groupId, long userId, long assignmentId, string content, string? url, int expectedVersion, CancellationToken ct) =>
        WriteAsync(groupId, userId, false, async _ =>
        {
            var assignment = await db.GroupAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            Require(assignment.Status == "Open", "Bài tập đã đóng.");
            Require(assignment.AllowLateSubmission || DateTimeOffset.UtcNow <= assignment.DueAtUtc, "Đã hết hạn nộp bài.");
            Require(await db.GroupAssignmentAssignees.AnyAsync(x => x.AssignmentId == assignmentId && x.UserId == userId, ct), "Bạn không được giao bài tập này.");
            var latest = await db.GroupSubmissions.Where(x => x.AssignmentId == assignmentId && x.UserId == userId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            CheckRevision(latest?.Version ?? 0, expectedVersion);
            Require(latest?.Status != "Completed", "Bài đã hoàn thành; người quản lý phải yêu cầu sửa trước khi nộp lại.");
            db.GroupSubmissions.Add(new GroupSubmission { AssignmentId = assignmentId, UserId = userId,
                Version = (latest?.Version ?? 0) + 1, Content = content, ResourceUrl = url, SubmittedAtUtc = DateTimeOffset.UtcNow });
        }, ct);

    public Task ReviewAsync(long groupId, long userId, long assignmentId, long submissionId, string status, string feedback, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            Require(status is "Completed" or "NeedsRevision", "Trạng thái phản hồi không hợp lệ.");
            if (!await db.GroupAssignments.AnyAsync(x => x.Id == assignmentId && x.StudyGroupId == groupId, ct)) throw Missing();
            var submission = await db.GroupSubmissions.SingleOrDefaultAsync(x => x.Id == submissionId && x.AssignmentId == assignmentId, ct) ?? throw Missing();
            if (await db.GroupSubmissions.AnyAsync(x => x.AssignmentId == assignmentId && x.UserId == submission.UserId && x.Version > submission.Version, ct))
                throw Conflict("Đã có bài nộp mới hơn. Hãy tải lại để nhận xét phiên bản mới nhất.");
            submission.Status = status; submission.Feedback = feedback;
            submission.ReviewedByUserId = userId; submission.ReviewedAtUtc = DateTimeOffset.UtcNow;
        }, ct);

    public Task SaveGoalAsync(long groupId, long userId, GroupWeeklyGoal input, CancellationToken ct) =>
        WriteAsync(groupId, userId, true, async _ =>
        {
            var goal = await db.GroupWeeklyGoals.SingleOrDefaultAsync(x => x.StudyGroupId == groupId && x.WeekStart == input.WeekStart, ct);
            CheckRevision(goal?.Revision ?? 0, input.Revision);
            if (goal is null) { goal = new GroupWeeklyGoal { StudyGroupId = groupId, WeekStart = input.WeekStart }; db.GroupWeeklyGoals.Add(goal); }
            else goal.Revision++;
            goal.Title = input.Title; goal.TargetSessions = input.TargetSessions; goal.TargetAssignments = input.TargetAssignments;
        }, ct);

    public Task AskAsync(long groupId, long userId, GroupQuestion question, CancellationToken ct) =>
        WriteAsync(groupId, userId, false, _ =>
        {
            question.StudyGroupId = groupId; question.AuthorUserId = userId; question.CreatedAtUtc = DateTimeOffset.UtcNow;
            db.GroupQuestions.Add(question);
            return Task.CompletedTask;
        }, ct);

    public Task AnswerAsync(long groupId, long userId, long questionId, string body, CancellationToken ct) =>
        WriteAsync(groupId, userId, false, async _ =>
        {
            if (!await db.GroupQuestions.AnyAsync(x => x.Id == questionId && x.StudyGroupId == groupId, ct)) throw Missing();
            db.GroupAnswers.Add(new GroupAnswer { QuestionId = questionId, AuthorUserId = userId, Body = body, CreatedAtUtc = DateTimeOffset.UtcNow });
        }, ct);

    public Task AcceptAnswerAsync(long groupId, long userId, long questionId, long? answerId, int revision, CancellationToken ct) =>
        WriteAsync(groupId, userId, false, async canManage =>
        {
            var question = await db.GroupQuestions.SingleOrDefaultAsync(x => x.Id == questionId && x.StudyGroupId == groupId, ct) ?? throw Missing();
            if (!canManage && question.AuthorUserId != userId) throw new StudyGroupRepositoryException("forbidden", "Chỉ người hỏi hoặc quản lý được chọn câu trả lời.");
            CheckRevision(question.Revision, revision);
            if (answerId is not null && !await db.GroupAnswers.AnyAsync(x => x.Id == answerId && x.QuestionId == questionId, ct)) throw Missing();
            question.AcceptedAnswerId = answerId; question.Revision++;
        }, ct);
}
