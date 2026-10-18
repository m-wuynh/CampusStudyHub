namespace StudyHub.BLL.DTOs;

public sealed record SaveGroupSessionRequest(string? Title, string? Objective, string? Preparation, string? Location,
    string? MeetingUrl, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, int Revision = 0);
public sealed record ChangeGroupSessionRequest(string Status, string? Summary, int Revision);
public sealed record RegisterGroupSessionRequest(bool Registered);
public sealed record MarkGroupAttendanceRequest(bool Attended);
public sealed record SaveGroupAssignmentRequest(string? Title, string? Description, long? SessionId,
    DateTimeOffset DueAtUtc, bool AllowLateSubmission, IReadOnlyList<long>? AssigneeIds, int Revision = 0);
public sealed record RevisionRequest(int Revision);
public sealed record SubmitGroupAssignmentRequest(string? Content, string? ResourceUrl, int ExpectedVersion = 0);
public sealed record ReviewGroupSubmissionRequest(string Status, string? Feedback);
public sealed record SaveGroupGoalRequest(DateOnly WeekStart, string? Title, int TargetSessions, int TargetAssignments, int Revision = 0);
public sealed record AskGroupQuestionRequest(string? Title, string? Subject, string? Body);
public sealed record AnswerGroupQuestionRequest(string? Body);
public sealed record AcceptGroupAnswerRequest(long? AnswerId, int Revision);
