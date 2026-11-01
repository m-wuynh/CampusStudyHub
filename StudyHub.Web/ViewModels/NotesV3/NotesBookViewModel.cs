namespace StudyHub.Web.ViewModels.NotesV3;
// Màn 4: gom các ghi chú của PDF thành một danh sách
public sealed record NotesBookViewModel(
    long DocumentId,
    string Title,
    long? ShareId,
    int ReturnPage,
    IReadOnlyList<BookPageViewModel> Pages);

public sealed record BookPageViewModel(
    int PageNumber,
    string Text);
