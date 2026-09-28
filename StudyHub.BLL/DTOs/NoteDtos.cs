namespace StudyHub.BLL.DTOs;

public sealed record SaveNoteRequestDto(
    long? NoteId,
    long? UserSubjectId,
    long? StudyGroupId,
    string Title,
    string Body,
    string BodyFormat,
    string Visibility,
    bool IsPinned);

public sealed record NoteDto(
    long Id,
    string Title,
    string Body,
    string Visibility,
    bool IsPinned,
    DateTime UpdatedAtUtc);
