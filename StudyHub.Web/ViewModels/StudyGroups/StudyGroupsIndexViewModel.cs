using StudyHub.BLL.DTOs;
namespace StudyHub.Web.ViewModels.StudyGroups;

public sealed record StudyGroupsIndexViewModel(IReadOnlyList<GroupSummaryResponse> Groups, string? SearchText = null);
