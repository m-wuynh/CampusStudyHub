using StudyHub.DAL.Entities.Pdf;
using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using StudyHub.BLL.Services.StudyGroups.Models;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed partial class StudyGroupWorkflow
{
    public async Task<IReadOnlyList<GroupSubjectOptionData>> GetSubjectOptionsAsync(
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var catalogNames = await data.Query<Subject>(false).AsNoTracking()
            .Where(subject => subject.IsActive)
            .Select(subject => subject.SubjectName)
            .ToListAsync(cancellationToken);
        var personalNames = await data.Query<UserSubject>(false).AsNoTracking()
            .Where(item => item.UserId == currentUserId && !item.IsArchived && item.CustomSubjectName != null)
            .Select(item => item.CustomSubjectName!)
            .ToListAsync(cancellationToken);
        var previousGroupNames = await data.Query<StudyGroupSubject>(false).AsNoTracking()
            .Where(item => item.AddedByUserId == currentUserId && item.CustomSubjectName != null)
            .Select(item => item.CustomSubjectName!)
            .ToListAsync(cancellationToken);

        var catalogSet = catalogNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return catalogNames
            .Concat(personalNames)
            .Concat(previousGroupNames)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .Select(name => new GroupSubjectOptionData(name, !catalogSet.Contains(name)))
            .ToList();
    }

    public async Task<IReadOnlyList<StudyGroupData>> GetVisibleAsync(
        long currentUserId,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        var query = data.Query<StudyGroup>(false)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(group => !group.IsArchived &&
                (group.Visibility == "Public" || group.GroupMembers.Any(member =>
                    member.UserId == currentUserId &&
                    (member.Status == "Active" || member.Status == "Pending" || member.Status == "Banned"))))
            .Include(group => group.StudyGroupSubjects)
                .ThenInclude(item => item.Subject)
            .Include(group => group.GroupMembers)
                .ThenInclude(member => member.User)
            .AsQueryable();

        var keyword = searchText?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(group =>
                group.GroupName.Contains(keyword) ||
                (group.Description != null && group.Description.Contains(keyword)) ||
                (group.Goal != null && group.Goal.Contains(keyword)) ||
                group.StudyGroupSubjects.Any(item =>
                    (item.Subject != null && item.Subject.SubjectName.Contains(keyword)) ||
                    (item.CustomSubjectName != null && item.CustomSubjectName.Contains(keyword))));
        }

        var groups = await query.OrderBy(group => group.GroupName).ToListAsync(cancellationToken);
        return groups.Select(group => ToDomain(group, currentUserId)).ToList();
    }

    public async Task<StudyGroupData?> GetAsync(
        long groupId,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var group = await data.Query<StudyGroup>(false)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(item => !item.IsArchived && item.StudyGroupId == groupId)
            .Include(item => item.StudyGroupSubjects)
                .ThenInclude(subject => subject.Subject)
            .Include(item => item.GroupMembers)
                .ThenInclude(member => member.User)
            .SingleOrDefaultAsync(cancellationToken);

        if (group is null) return null;

        var currentMembership = group.GroupMembers.SingleOrDefault(member => member.UserId == currentUserId);
        var canPreview = group.Visibility == "Public" || currentMembership?.Status is "Active" or "Pending" or "Banned";
        if (!canPreview) return null;

        var isActiveMember = currentMembership?.Status == "Active";
        var canManage = isActiveMember &&
            (group.OwnerUserId == currentUserId || currentMembership?.MemberRole == "Moderator");

        var result = ToDomain(group, currentUserId);
        if (!isActiveMember)
        {
            result.Members.Clear();
            return result;
        }

        var posts = await data.Query<GroupPost>(false)
            .AsNoTracking()
            .Where(post => post.StudyGroupId == groupId && !post.IsDeleted)
            .Include(post => post.GroupMember)
                .ThenInclude(member => member.User)
            .OrderByDescending(post => post.CreatedAtUtc)
            .Take(120)
            .ToListAsync(cancellationToken);

        result.Posts.AddRange(posts
            .OrderBy(post => post.CreatedAtUtc)
            .Select(ToPost));

        var decks = await data.Query<FlashcardDeck>(false).AsNoTracking()
            .Where(item => item.StudyGroupId == groupId && item.Visibility == "Group" && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(20)
            .Select(item => new GroupResourceData(
                $"deck-{item.DeckId}", item.Title, "Flashcard", $"/Groups/{groupId}/Resources/Flashcard/{item.DeckId}"))
            .ToListAsync(cancellationToken);
        result.Resources.AddRange(decks);

        if (!canManage)
            result.Members.RemoveAll(member => member.Status != "Active");

        return result;
    }

    public Task<bool> IsActiveMemberAsync(
        long groupId,
        long userId,
        CancellationToken cancellationToken = default) =>
        data.Query<GroupMember>(false).AsNoTracking().AnyAsync(
            item => item.StudyGroupId == groupId && item.UserId == userId && item.Status == "Active" && !item.StudyGroup.IsArchived,
            cancellationToken);

}
