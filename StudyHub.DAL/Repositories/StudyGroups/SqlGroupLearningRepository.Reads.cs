using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.StudyGroups.Models;

namespace StudyHub.DAL.Repositories.StudyGroups;

public sealed partial class SqlGroupLearningRepository
{
    public async Task<GroupLearningData> GetAsync(long groupId, long userId, DateOnly weekStart, CancellationToken ct)
    {
        var manage = await AccessAsync(groupId, userId, false, ct);
        var members = await db.GroupMembers.AsNoTracking().Where(x => x.StudyGroupId == groupId && x.Status == "Active")
            .OrderBy(x => x.User.DisplayName).Select(x => new LearningMember(x.UserId, x.User.DisplayName)).ToListAsync(ct);
        var sessions = await db.GroupStudySessions.AsNoTracking().Where(x => x.StudyGroupId == groupId)
            .Include(x => x.Attendees).ThenInclude(x => x.User).AsSplitQuery().OrderByDescending(x => x.StartsAtUtc).ToListAsync(ct);
        var assignments = await db.GroupAssignments.AsNoTracking().Where(x => x.StudyGroupId == groupId)
            .Include(x => x.Assignees).ThenInclude(x => x.User)
            .Include(x => x.Assignees).ThenInclude(x => x.Submissions).AsSplitQuery().OrderByDescending(x => x.DueAtUtc).ToListAsync(ct);
        var questions = await db.GroupQuestions.AsNoTracking().Where(x => x.StudyGroupId == groupId)
            .Include(x => x.Author).Include(x => x.Answers).ThenInclude(x => x.Author)
            .AsSplitQuery().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var resources = await ResourcesAsync(groupId, userId, false, ct);
        var available = await ResourcesAsync(groupId, userId, true, ct);
        var goal = await db.GroupWeeklyGoals.AsNoTracking().SingleOrDefaultAsync(x => x.StudyGroupId == groupId && x.WeekStart == weekStart, ct);

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
                assigned.Count, submitted.Count, submitted.Count(x => x!.Status == "Completed"));
        }).Where(x => manage || x.UserId == userId).ToList();
        var progress = new LearningProgressData(weekStart,
            goal is null ? null : new GoalData(goal.Title, goal.TargetSessions, goal.TargetAssignments, goal.Revision),
            weekSessions.Count,
            weekAssignments.Count(x => x.Assignees.Count > 0 && x.Assignees.All(a => a.Submissions.OrderByDescending(s => s.Version).FirstOrDefault()?.Status == "Completed")),
            weekAssignments.Sum(x => x.Assignees.Count), latest.Count, latest.Count(x => x!.Status == "Completed"), progressMembers);
        return new GroupLearningData(userId, manage, members,
            sessions.Select(x => new SessionData(x.Id, x.Title, x.Objective, x.Preparation, x.Location, x.MeetingUrl,
                x.StartsAtUtc, x.EndsAtUtc, x.Status, x.Summary, x.Revision,
                x.Attendees.Select(a => new AttendanceData(a.UserId, a.User.DisplayName, a.Attended)).ToList())).ToList(),
            assignments.Select(x => new AssignmentData(x.Id, x.Title, x.Description, x.SessionId, x.DueAtUtc,
                x.AllowLateSubmission, x.Status, x.Revision,
                x.Assignees.Select(a => new LearningMember(a.UserId, a.User.DisplayName)).ToList(),
                x.Assignees.Where(a => manage || a.UserId == userId).SelectMany(a => a.Submissions.Select(s =>
                    new SubmissionData(s.Id, s.UserId, a.User.DisplayName, s.Version, s.Content, s.ResourceUrl,
                        s.SubmittedAtUtc, s.SubmittedAtUtc > x.DueAtUtc, s.Status, s.Feedback))).OrderByDescending(s => s.Version).ToList())).ToList(),
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
    var decks = db.FlashcardDecks
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

    public Task ShareAsync(
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
            // Notes/PDF dùng API NotesV3.
            if (kind != "Flashcard")
            {
                throw Missing();
            }

            var deck = await db.FlashcardDecks
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
                throw new StudyGroupRepositoryException(
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

    public async Task<SharedContentData> ReadResourceAsync(
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

    var deck = await db.FlashcardDecks
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.DeckId == resourceId &&
                 x.StudyGroupId == groupId &&
                 x.Visibility == "Group" &&
                 !x.IsDeleted,
            ct)
        ?? throw Missing();

    var cards = await db.Flashcards
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
