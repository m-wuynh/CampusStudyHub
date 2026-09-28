using StudyHub.BLL.DTOs.Notes;

namespace StudyHub.Web.ViewModels.Notes;

public sealed record NotesWorkspaceViewModel(
    NotesUserDto User,
    NotesWorkspaceDto Data,
    string? Search);
