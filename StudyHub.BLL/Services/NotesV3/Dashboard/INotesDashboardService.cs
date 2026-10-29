using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.Dashboard;

public interface INotesDashboardService
{
    Task<NotesDashboardDto> GetAsync(
        CancellationToken ct = default);
}