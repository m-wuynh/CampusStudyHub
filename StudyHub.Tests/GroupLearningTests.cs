using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.StudyGroups.Models;
using Xunit;

namespace StudyHub.Tests;

public sealed class GroupLearningTests(GroupLearningFixture f) : IClassFixture<GroupLearningFixture>
{
    private string Root(long? group = null) => $"/api/groups/{group ?? f.GroupId}/learning";
    private static async Task Ok(HttpResponseMessage response) => Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    private async Task<GroupLearningData> Data(HttpClient client, string suffix = "") =>
        (await client.GetFromJsonAsync<GroupLearningData>(Root() + suffix))!;
    private static object Session(string title) => new { title, objective = "Giải được bài tập", startsAtUtc = DateTimeOffset.UtcNow.AddDays(1), endsAtUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(1) };
    private object Assignment(string title, bool late = false, long? session = null) => new
    {
        title, description = "Trình bày lời giải", dueAtUtc = DateTimeOffset.UtcNow.AddDays(1), allowLateSubmission = late,
        assigneeIds = new[] { f.MemberId }, sessionId = session
    };

    [Fact]
    public async Task Authorization_and_antiforgery_are_enforced()
    {
        using var anonymous = await f.Client(null, false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Root())).StatusCode);
        foreach (var id in new[] { f.PendingId, f.BannedId, f.OutsiderId })
        {
            using var client = await f.Client(id);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Root())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Root() + "/questions", new { title = "Question", subject = "Math", body = "Text" })).StatusCode);
        }
        using var member = await f.Client(f.MemberId);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync(Root() + "/sessions", Session("Forbidden"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync(Root() + "/assignments", Assignment("Forbidden"))).StatusCode);
        using var noCsrf = await f.Client(f.OwnerId, false);
        Assert.Equal(HttpStatusCode.BadRequest, (await noCsrf.PostAsJsonAsync(Root() + "/sessions", Session("No token"))).StatusCode);
    }

    [Fact]
    public async Task Session_lifecycle_registration_attendance_and_stale_edits()
    {
        using var owner = await f.Client(f.OwnerId);
        using var moderator = await f.Client(f.ModeratorId);
        using var member = await f.Client(f.MemberId);
        var title = Guid.NewGuid().ToString();
        await Ok(await moderator.PostAsJsonAsync(Root() + "/sessions", Session(title)));
        var session = (await Data(owner)).Sessions.Single(x => x.Title == title);
        await Ok(await member.PutAsJsonAsync(Root() + $"/sessions/{session.Id}/registration", new { registered = true }));
        await Ok(await member.PutAsJsonAsync(Root() + $"/sessions/{session.Id}/registration", new { registered = true }));
        Assert.Single((await Data(owner)).Sessions.Single(x => x.Id == session.Id).Attendees);
        await Ok(await member.PutAsJsonAsync(Root() + $"/sessions/{session.Id}/registration", new { registered = false }));
        Assert.Empty((await Data(owner)).Sessions.Single(x => x.Id == session.Id).Attendees);
        await Ok(await member.PutAsJsonAsync(Root() + $"/sessions/{session.Id}/registration", new { registered = true }));
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(Root() + $"/sessions/{session.Id}/attendance/{f.MemberId}", new { attended = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(Root(f.OtherGroupId) + $"/sessions/{session.Id}/status", new { status = "Cancelled", revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync(Root() + $"/sessions/{session.Id}", new { title, objective = "Changed", startsAtUtc = session.StartsAtUtc, endsAtUtc = session.EndsAtUtc, revision = 0 })).StatusCode);
        await using (var db = f.NewDb())
        {
            await EntityFrameworkQueryableExtensions.ExecuteUpdateAsync(db.GroupStudySessions.Where(x => x.Id == session.Id), s => s.SetProperty(x => x.StartsAtUtc, DateTimeOffset.UtcNow.AddHours(-1)));
        }
        await Ok(await moderator.PutAsJsonAsync(Root() + $"/sessions/{session.Id}/attendance/{f.MemberId}", new { attended = true }));
        await Ok(await owner.PostAsJsonAsync(Root() + $"/sessions/{session.Id}/status", new { status = "Completed", summary = "Đã giải 5 bài", revision = 1 }));
        var completed = (await Data(owner)).Sessions.Single(x => x.Id == session.Id);
        Assert.Equal("Completed", completed.Status); Assert.True(completed.Attendees.Single().Attended);
        Assert.Equal(HttpStatusCode.BadRequest, (await member.PutAsJsonAsync(Root() + $"/sessions/{session.Id}/registration", new { registered = false })).StatusCode);
    }

    [Fact]
    public async Task Assignments_preserve_revisions_limit_review_and_hide_other_submissions()
    {
        using var owner = await f.Client(f.OwnerId);
        using var member = await f.Client(f.MemberId);
        using var peer = await f.Client(f.PeerId);
        var title = Guid.NewGuid().ToString();
        await Ok(await owner.PostAsJsonAsync(Root() + "/assignments", Assignment(title)));
        var a = (await Data(owner)).Assignments.Single(x => x.Title == title);
        await Ok(await member.PostAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions", new { content = "Version one", expectedVersion = 0 }));
        Assert.Equal(HttpStatusCode.Conflict, (await member.PostAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions", new { content = "Duplicate", expectedVersion = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await peer.PostAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions", new { content = "Not assigned" })).StatusCode);
        Assert.Empty((await Data(peer)).Assignments.Single(x => x.Id == a.Id).Submissions);
        var first = (await Data(owner)).Assignments.Single(x => x.Id == a.Id).Submissions.Single();
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions/{first.Id}/review", new { status = "Completed" })).StatusCode);
        await Ok(await owner.PutAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions/{first.Id}/review", new { status = "NeedsRevision", feedback = "Giải thích thêm" }));
        await Ok(await member.PostAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions", new { content = "Version two", expectedVersion = 1 }));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions/{first.Id}/review", new { status = "Completed" })).StatusCode);
        var rows = (await Data(owner)).Assignments.Single(x => x.Id == a.Id).Submissions;
        Assert.Equal(2, rows.Count); Assert.Equal("Giải thích thêm", rows.Single(x => x.Version == 1).Feedback);
        var latest = rows.Single(x => x.Version == 2);
        await Ok(await owner.PutAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions/{latest.Id}/review", new { status = "Completed", feedback = "Tốt" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await member.PostAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions", new { content = "Locked", expectedVersion = 2 })).StatusCode);
        await Ok(await owner.PostAsJsonAsync(Root() + $"/assignments/{a.Id}/close", new { revision = 1 }));
        Assert.Equal("Closed", (await Data(owner)).Assignments.Single(x => x.Id == a.Id).Status);
    }

    [Fact]
    public async Task Deadline_URL_validation_and_cross_group_assignment_are_enforced()
    {
        using var owner = await f.Client(f.OwnerId);
        using var member = await f.Client(f.MemberId);
        var title = Guid.NewGuid().ToString();
        await Ok(await owner.PostAsJsonAsync(Root() + "/assignments", Assignment(title)));
        var a = (await Data(owner)).Assignments.Single(x => x.Title == title);
        Assert.Equal(HttpStatusCode.BadRequest, (await member.PostAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions", new { resourceUrl = "javascript:alert(1)" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(Root(f.OtherGroupId) + $"/assignments/{a.Id}/close", new { revision = 1 })).StatusCode);
        await using (var db = f.NewDb()) { await EntityFrameworkQueryableExtensions.ExecuteUpdateAsync(db.GroupAssignments.Where(x => x.Id == a.Id), s => s.SetProperty(x => x.DueAtUtc, DateTimeOffset.UtcNow.AddHours(-1))); }
        Assert.Equal(HttpStatusCode.BadRequest, (await member.PostAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions", new { content = "Too late" })).StatusCode);
        await using (var db = f.NewDb()) { await EntityFrameworkQueryableExtensions.ExecuteUpdateAsync(db.GroupAssignments.Where(x => x.Id == a.Id), s => s.SetProperty(x => x.AllowLateSubmission, true)); }
        await Ok(await member.PostAsJsonAsync(Root() + $"/assignments/{a.Id}/submissions", new { content = "Late allowed" }));
        Assert.True((await Data(owner)).Assignments.Single(x => x.Id == a.Id).Submissions.Single().IsLate);
    }

    [Fact]
    public async Task Questions_accept_only_their_own_answers_and_encode_content()
    {
        using var owner = await f.Client(f.OwnerId);
        using var member = await f.Client(f.MemberId);
        using var peer = await f.Client(f.PeerId);
        var title = Guid.NewGuid().ToString();
        await Ok(await member.PostAsJsonAsync(Root() + "/questions", new { title, subject = "Toán", body = "<script>alert(1)</script>" }));
        await Ok(await owner.PostAsJsonAsync(Root() + "/questions", new { title = title + "other", subject = "Toán", body = "Other" }));
        var q = (await Data(owner)).Questions.Single(x => x.Title == title);
        var other = (await Data(owner)).Questions.Single(x => x.Title == title + "other");
        Assert.True(q.Id > 0);
        await Ok(await peer.PostAsJsonAsync(Root() + $"/questions/{q.Id}/answers", new { body = "Một lời giải" }));
        await Ok(await peer.PostAsJsonAsync(Root() + $"/questions/{other.Id}/answers", new { body = "Lời giải khác" }));
        var ownAnswer = (await Data(owner)).Questions.Single(x => x.Id == q.Id).Answers.Single();
        var otherAnswer = (await Data(owner)).Questions.Single(x => x.Id == other.Id).Answers.Single();
        Assert.Equal(HttpStatusCode.Forbidden, (await peer.PutAsJsonAsync(Root() + $"/questions/{q.Id}/accepted-answer", new { answerId = ownAnswer.Id, revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.PutAsJsonAsync(Root() + $"/questions/{q.Id}/accepted-answer", new { answerId = otherAnswer.Id, revision = 1 })).StatusCode);
        await Ok(await member.PutAsJsonAsync(Root() + $"/questions/{q.Id}/accepted-answer", new { answerId = ownAnswer.Id, revision = 1 }));
        Assert.Equal(ownAnswer.Id, (await Data(owner)).Questions.Single(x => x.Id == q.Id).AcceptedAnswerId);
        await Ok(await owner.PutAsJsonAsync(Root() + $"/questions/{q.Id}/accepted-answer", new { answerId = (long?)null, revision = 2 }));
    }

    [Fact]
    public async Task Shared_resources_have_read_only_access_and_are_revoked_on_departure()
    {
        using var owner = await f.Client(f.OwnerId);
        using var member = await f.Client(f.MemberId);
        long groupId, noteId, deckId, docId;
        await using (var db = f.NewDb())
        {
            var group = new StudyGroup { OwnerUserId = f.OwnerId, GroupName = "Resource group", Visibility = "Public", JoinMode = "Open", MeetingFormat = "Online" };
            db.StudyGroups.Add(group); await db.SaveChangesAsync(); groupId = group.StudyGroupId;
            foreach (var uid in new[] { f.OwnerId, f.MemberId }) db.GroupMembers.Add(new GroupMember { StudyGroupId = groupId, UserId = uid, Status = "Active", MemberRole = "Member", JoinedAtUtc = DateTime.UtcNow });
            var note = new Note { OwnerUserId = f.MemberId, Title = "Shared note", Body = "<script>alert(1)</script>", BodyFormat = "PlainText", Visibility = "Private" };
            var deck = new FlashcardDeck { OwnerUserId = f.MemberId, Title = "Shared deck", Visibility = "Private" };
            var doc = new Document { OwnerUserId = f.MemberId, Title = "Shared link", Visibility = "Private", ResourceType = "Link", ExternalUrl = "https://example.org/study" };
            db.AddRange(note, deck, doc); await db.SaveChangesAsync(); noteId = note.NoteId; deckId = deck.DeckId; docId = doc.DocumentId;
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PutAsync(Root(groupId) + $"/resources/Note/{noteId}", null)).StatusCode);
        foreach (var (kind, id) in new[] { ("Note", noteId), ("Flashcard", deckId), ("Document", docId) })
        {
            await Ok(await member.PutAsync(Root(groupId) + $"/resources/{kind}/{id}", null));
            await Ok(await owner.GetAsync($"/Groups/{groupId}/Resources/{kind}/{id}"));
        }
        var html = await owner.GetStringAsync($"/Groups/{groupId}/Resources/Note/{noteId}");
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>alert(1)</script>", html);
        await Ok(await owner.DeleteAsync(Root(groupId) + $"/resources/Note/{noteId}"));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Groups/{groupId}/Resources/Note/{noteId}")).StatusCode);
        await Ok(await member.PutAsync(Root(groupId) + $"/resources/Note/{noteId}", null));
        await Ok(await member.DeleteAsync($"/api/groups/{groupId}/join"));
        foreach (var (kind, id) in new[] { ("Note", noteId), ("Flashcard", deckId), ("Document", docId) })
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Groups/{groupId}/Resources/{kind}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync(Root(groupId))).StatusCode);
        await using var verify = f.NewDb();
        Assert.Equal("Private", (await verify.Notes.FindAsync(noteId))!.Visibility);
        Assert.Null((await verify.Documents.FindAsync(docId))!.StudyGroupId);
        Assert.Null((await verify.FlashcardDecks.FindAsync(deckId))!.StudyGroupId);
    }

    [Fact]
    public async Task Weekly_progress_uses_Vietnam_boundaries_and_latest_submission()
    {
        using var owner = await f.Client(f.OwnerId);
        using var member = await f.Client(f.MemberId);
        var monday = new DateOnly(2030, 1, 7);
        var start = new DateTimeOffset(2030, 1, 7, 0, 0, 0, TimeSpan.FromHours(7));
        await using (var db = f.NewDb())
        {
            db.GroupStudySessions.Add(new GroupStudySession { StudyGroupId = f.GroupId, CreatedByUserId = f.OwnerId,
                Title = "Boundary", Objective = "Test", StartsAtUtc = start.ToUniversalTime(), EndsAtUtc = start.AddHours(1), Status = "Completed",
                Attendees = [new GroupSessionAttendance { UserId = f.MemberId, Attended = true }] });
            var a = new GroupAssignment { StudyGroupId = f.GroupId, CreatedByUserId = f.OwnerId, Title = "Progress", Description = "Test", DueAtUtc = start.AddDays(1),
                Assignees = [new GroupAssignmentAssignee { UserId = f.MemberId, Submissions = [
                    new GroupSubmission { UserId = f.MemberId, Version = 1, Content = "Old", Status = "Completed", SubmittedAtUtc = start },
                    new GroupSubmission { UserId = f.MemberId, Version = 2, Content = "Latest", Status = "Submitted", SubmittedAtUtc = start.AddHours(1) }] }] };
            db.GroupAssignments.Add(a); await db.SaveChangesAsync();
        }
        await Ok(await owner.PutAsJsonAsync(Root() + "/goal", new { weekStart = monday, title = "Tuần mới", targetSessions = 1, targetAssignments = 1, revision = 0 }));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync(Root() + "/goal", new { weekStart = monday, title = "Stale", targetSessions = 1, targetAssignments = 1, revision = 0 })).StatusCode);
        var data = await Data(owner, "?week=2030-01-09");
        Assert.Equal(monday, data.Progress.WeekStart);
        Assert.Equal(1, data.Progress.CompletedSessions); Assert.Equal(1, data.Progress.Submitted);
        Assert.Equal(0, data.Progress.Completed); Assert.Equal(0, data.Progress.FullyCompletedAssignments);
        Assert.Equal(1, data.Progress.Members.Single(x => x.UserId == f.MemberId).AttendedSessions);
        Assert.Single((await Data(member, "?week=2030-01-07")).Progress.Members);
    }
}
