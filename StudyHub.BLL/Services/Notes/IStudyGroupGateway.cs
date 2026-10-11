namespace StudyHub.BLL.Services.Notes;

/// <summary>Only boundary Notes uses to ask the Study Groups module about access.</summary>
public interface IStudyGroupGateway
{
    Task<bool> GroupExistsAndIsActiveAsync(long groupId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveMemberAsync(long userId, long groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteGroupOption>> GetActiveGroupsAsync(long userId, CancellationToken cancellationToken = default);
    Task<NoteGroupDescriptor?> GetActiveGroupAsync(long groupId, CancellationToken cancellationToken = default);
    Task<NoteGroupDescriptor?> GetDevelopmentTestGroupAsync(long ownerUserId, CancellationToken cancellationToken = default);
    Task<NoteGroupDescriptor> CreateDevelopmentTestGroupAsync(long ownerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotesDevelopmentPersona>> GetDevelopmentPersonasAsync(CancellationToken cancellationToken = default);
}

public sealed record NoteGroupDescriptor(long GroupId, long OwnerUserId, string Name);
public sealed record NoteGroupOption(long GroupId, string Name);
public sealed record NotesDevelopmentPersona(string PersonaKey, long UserId, string DisplayName, string? Email,
    string RoleCode, string RoleLabel, long GroupId, string GroupName, bool IsSimulated = false);
