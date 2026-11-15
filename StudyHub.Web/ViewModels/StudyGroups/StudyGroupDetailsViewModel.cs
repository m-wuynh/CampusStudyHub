using StudyHub.BLL.DTOs;
using StudyHub.BLL.DTOs.StudyGroups;

namespace StudyHub.Web.ViewModels.StudyGroups;

public sealed record StudyGroupDetailsViewModel(GroupDetailsResponse Group, GroupLearningData? Learning = null);
