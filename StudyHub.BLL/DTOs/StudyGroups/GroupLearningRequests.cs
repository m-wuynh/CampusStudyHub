namespace StudyHub.BLL.DTOs.StudyGroups;

public sealed record CreateGroupSessionRequest(string? Title, string? Objective, string? Preparation, string? Location,
    string? MeetingUrl, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);
public sealed record ChangeGroupSessionRequest(string Status, string? Summary, int Revision);
public sealed record RegisterGroupSessionRequest(bool Registered);
public sealed record GroupAttendanceEntry(long UserId, bool? Attended);
public sealed record MarkGroupAttendanceRequest(IReadOnlyList<GroupAttendanceEntry>? Entries);
public sealed record SaveGroupAssignmentRequest(string? Title, string? Description, long? SessionId,
    DateTimeOffset DueAtUtc, bool AllowLateSubmission, IReadOnlyList<long>? AssigneeIds, int Revision = 0);
public sealed record RevisionRequest(int Revision);
public sealed record SubmitGroupAssignmentRequest(string? Content, string? ResourceUrl, int ExpectedVersion = 0);
public sealed record ReviewGroupSubmissionRequest(string Status, string? Feedback);
public sealed record AskGroupQuestionRequest(string? Title, string? Subject, string? Body);
public sealed record AnswerGroupQuestionRequest(string? Body);
public sealed record AcceptGroupAnswerRequest(long? AnswerId, int Revision);
