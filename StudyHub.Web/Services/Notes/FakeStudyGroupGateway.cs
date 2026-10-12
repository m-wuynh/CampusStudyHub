using StudyHub.BLL.Services.Notes;

namespace StudyHub.Web.Services.Notes;

/// <summary>Development-only membership fixture for the M0 boundary check.</summary>
public sealed class FakeStudyGroupGateway : IStudyGroupGateway
{
    private static readonly HashSet<long> ActiveGroups = [10, 20];
    private static readonly HashSet<(long UserId, long GroupId)> ActiveMemberships = [(1, 10)];

    public Task<bool> GroupExistsAndIsActiveAsync(long groupId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ActiveGroups.Contains(groupId));

    public Task<bool> IsActiveMemberAsync(long userId, long groupId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ActiveMemberships.Contains((userId, groupId)));

    public Task<IReadOnlyList<NoteGroupOption>> GetActiveGroupsAsync(long userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NoteGroupOption>>(ActiveMemberships
            .Where(item => item.UserId == userId)
            .Select(item => new NoteGroupOption(item.GroupId, $"M0 Test Group {item.GroupId}"))
            .ToList());

    public Task<NoteGroupDescriptor?> GetActiveGroupAsync(long groupId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ActiveGroups.Contains(groupId)
            ? new NoteGroupDescriptor(groupId, 1, $"M0 Test Group {groupId}")
            : null);

    public Task<NoteGroupDescriptor?> GetDevelopmentTestGroupAsync(
        long ownerUserId,
        CancellationToken cancellationToken = default) => Task.FromResult<NoteGroupDescriptor?>(null);

    public Task<NoteGroupDescriptor> CreateDevelopmentTestGroupAsync(
        long ownerUserId,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The M0 fake gateway cannot create database groups.");

    public Task<IReadOnlyList<NotesDevelopmentPersona>> GetDevelopmentPersonasAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NotesDevelopmentPersona>>([]);
}
