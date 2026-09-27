using StudyHub.BLL.DTOs;

namespace StudyHub.Web.ViewModels.StudyGroups;

public sealed record StudyGroupDetailsViewModel(GroupDetailsResponse Group);

public sealed record StudyGroupInviteViewModel(GroupInviteResponse Invite, string? ErrorMessage = null);
