using StudyHub.BLL.DTOs;
using StudyHub.BLL.DTOs.StudyGroups;

namespace StudyHub.BLL.Services.StudyGroups;

public interface IGroupLearningService
{
    Task<GroupLearningData> GetAsync(long groupId, DateOnly? week, CancellationToken ct);

    Task CreateSessionAsync(long groupId, CreateGroupSessionRequest request, CancellationToken ct);

    Task ChangeSessionAsync(long groupId, long id, ChangeGroupSessionRequest r, CancellationToken ct);

    Task RegisterAsync(long groupId, long id, bool registered, CancellationToken ct);

    Task MarkAttendanceAsync(long groupId, long id, MarkGroupAttendanceRequest request, CancellationToken ct);

    Task SaveAssignmentAsync(long groupId, long id, SaveGroupAssignmentRequest r, CancellationToken ct);

    Task CloseAssignmentAsync(long groupId, long id, int revision, CancellationToken ct);

    Task SubmitAsync(long groupId, long id, SubmitGroupAssignmentRequest r, CancellationToken ct);

    Task ReviewAsync(long groupId, long id, long submissionId, ReviewGroupSubmissionRequest r, CancellationToken ct);

    Task ShareAsync(long groupId, string kind, long id, bool shared, CancellationToken ct);

    Task<SharedContentData> ReadResourceAsync(long groupId, string kind, long id, CancellationToken ct);

    Task AskAsync(long groupId, AskGroupQuestionRequest r, CancellationToken ct);

    Task AnswerAsync(long groupId, long id, AnswerGroupQuestionRequest r, CancellationToken ct);

    Task AcceptAnswerAsync(long groupId, long id, AcceptGroupAnswerRequest r, CancellationToken ct);
}
