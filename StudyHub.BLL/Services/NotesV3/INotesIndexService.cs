using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3;

public interface INotesIndexService
{
    Task<NotesIndexDto> GetAsync(long? groupId, CancellationToken ct = default);

    Task<NotesTrashDto> GetTrashAsync(CancellationToken ct = default);
}
