using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.StudyGroups.Models;

namespace StudyHub.DAL.Repositories.StudyGroups;

public interface IGroupLearningRepository
{
    Task<GroupLearningData> GetAsync(long groupId, long userId, DateOnly weekStart, CancellationToken ct);
    Task SaveSessionAsync(long groupId, long userId, GroupStudySession session, CancellationToken ct);
    Task ChangeSessionAsync(long groupId, long userId, long sessionId, string status, string summary, int revision, CancellationToken ct);
    Task RegisterAsync(long groupId, long userId, long sessionId, bool registered, CancellationToken ct);
    Task MarkAttendanceAsync(long groupId, long userId, long sessionId, long targetUserId, bool attended, CancellationToken ct);
    Task SaveAssignmentAsync(long groupId, long userId, GroupAssignment assignment, IReadOnlyList<long> assignees, CancellationToken ct);
    Task CloseAssignmentAsync(long groupId, long userId, long assignmentId, int revision, CancellationToken ct);
    Task SubmitAsync(long groupId, long userId, long assignmentId, string content, string? url, int expectedVersion, CancellationToken ct);
    Task ReviewAsync(long groupId, long userId, long assignmentId, long submissionId, string status, string feedback, CancellationToken ct);
    Task ShareAsync(long groupId, long userId, string kind, long resourceId, bool shared, CancellationToken ct);
    Task<SharedContentData> ReadResourceAsync(long groupId, long userId, string kind, long resourceId, CancellationToken ct);
    Task SaveGoalAsync(long groupId, long userId, GroupWeeklyGoal goal, CancellationToken ct);
    Task AskAsync(long groupId, long userId, GroupQuestion question, CancellationToken ct);
    Task AnswerAsync(long groupId, long userId, long questionId, string body, CancellationToken ct);
    Task AcceptAnswerAsync(long groupId, long userId, long questionId, long? answerId, int revision, CancellationToken ct);
}
