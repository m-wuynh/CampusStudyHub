namespace StudyHub.DAL.Entities.NotesV3;

public sealed class NotesV3Page
{
    public long PageId { get; set; }

    // NULL khi đây là ghi chú gắn với một trang PDF.
    public long? SectionId { get; set; }

    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    // Nguồn dữ liệu chính cho trang cá nhân.
    // Trang nhóm lấy nội dung chính từ CollabUpdates.
    public string ContentDeltaJson { get; set; } =
        "{\"ops\":[{\"insert\":\"\\n\"}]}";

    public bool IsDeleted { get; set; }

    public byte[] RowVersion { get; set; } = [];
}