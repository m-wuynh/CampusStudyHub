namespace StudyHub.DAL.Repositories.StudyGroups.Models;

public sealed record LearningMember(long UserId, string Name);
public sealed record AttendanceData(long UserId, string Name, bool Attended);
public sealed record SessionData(long Id, string Title, string Objective, string Preparation, string Location,
    string? MeetingUrl, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string Status, string Summary,
    int Revision, IReadOnlyList<AttendanceData> Attendees);
public sealed record SubmissionData(long Id, long UserId, string Name, int Version, string Content,
    string? ResourceUrl, DateTimeOffset SubmittedAtUtc, bool IsLate, string Status, string Feedback);
public sealed record AssignmentData(long Id, string Title, string Description, long? SessionId,
    DateTimeOffset DueAtUtc, bool AllowLateSubmission, string Status, int Revision,
    IReadOnlyList<LearningMember> Assignees, IReadOnlyList<SubmissionData> Submissions);
public sealed record AnswerData(long Id, long AuthorUserId, string AuthorName, string Body, DateTimeOffset CreatedAtUtc);
public sealed record QuestionData(long Id, long AuthorUserId, string AuthorName, string Title, string Subject,
    string Body, DateTimeOffset CreatedAtUtc, long? AcceptedAnswerId, int Revision, IReadOnlyList<AnswerData> Answers);
public sealed record SharedResourceData(long Id, string Kind, string Title, long OwnerUserId, string OwnerName);
public sealed record ResourceCardData(string Front, string Back);
public sealed record SharedContentData(string Kind, string Title, string? Body, string? ExternalUrl,
    string? StorageKey, IReadOnlyList<ResourceCardData> Cards);
public sealed record GoalData(string Title, int TargetSessions, int TargetAssignments, int Revision);
public sealed record MemberProgressData(long UserId, string Name, int RegisteredSessions, int AttendedSessions,
    int Assigned, int Submitted, int Completed);
public sealed record LearningProgressData(DateOnly WeekStart, GoalData? Goal, int CompletedSessions,
    int FullyCompletedAssignments, int Assigned, int Submitted, int Completed,
    IReadOnlyList<MemberProgressData> Members);
public sealed record GroupLearningData(long CurrentUserId, bool CanManage, IReadOnlyList<LearningMember> Members,
    IReadOnlyList<SessionData> Sessions, IReadOnlyList<AssignmentData> Assignments,
    IReadOnlyList<QuestionData> Questions, IReadOnlyList<SharedResourceData> Resources,
    IReadOnlyList<SharedResourceData> AvailableResources, LearningProgressData Progress);
