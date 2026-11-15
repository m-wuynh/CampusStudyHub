using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.StudyGroups.Models;

namespace StudyHub.Web.ViewModels.StudyGroups;

public sealed record StudyGroupDetailsViewModel(GroupDetailsResponse Group, GroupLearningData? Learning = null);
