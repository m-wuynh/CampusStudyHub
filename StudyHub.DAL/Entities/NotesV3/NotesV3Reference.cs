namespace StudyHub.DAL.Entities.NotesV3;

public sealed class NotesV3Reference
{
    public long ReferenceId { get; set; }
    public long PageId { get; set; }
    public long DocumentVersionId { get; set; }

    // Lưu nguồn chia sẻ nếu capture từ PDF nhóm.
    // Giúp kiểm tra đúng session khi mở lại reference.
    public long? GroupDocumentShareId { get; set; }

    public int PhysicalPageNumber { get; set; }
    public string? AnchorJson { get; set; }
    public string CapturedText { get; set; } = string.Empty;
}