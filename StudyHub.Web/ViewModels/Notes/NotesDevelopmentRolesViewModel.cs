using StudyHub.BLL.Services.Notes;

namespace StudyHub.Web.ViewModels.Notes;

public sealed record NotesDevelopmentRolesViewModel(
    IReadOnlyList<NotesDevelopmentPersona> Personas,
    string? ErrorMessage,
    bool CanCreateSimulationGroup);
