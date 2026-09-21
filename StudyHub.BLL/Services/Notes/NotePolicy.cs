using StudyHub.BLL.DTOs;
using StudyHub.DAL.Enums.Notes;

namespace StudyHub.BLL.Services.Notes;

public static class NotePolicy
{
    public static bool HasValidSharingTarget(SaveNoteRequestDto note)
    {
        if (!Enum.TryParse<ContentVisibility>(note.Visibility, true, out var visibility)) return false;
        return visibility == ContentVisibility.Group
            ? note.StudyGroupId.HasValue
            : !note.StudyGroupId.HasValue;
    }
}
