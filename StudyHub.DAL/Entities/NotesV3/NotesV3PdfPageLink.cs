namespace StudyHub.DAL.Entities.NotesV3;

public sealed class NotesV3PdfPageLink
{
    public long PageId { get; set; }
    public long DocumentVersionId { get; set; }

    // Cá nhân: OwnerUserId có giá trị, GroupDocumentShareId NULL.
    // Nhóm: GroupDocumentShareId có giá trị, OwnerUserId NULL.
    public long? OwnerUserId { get; set; }
    public long? GroupDocumentShareId { get; set; }

    // Trang vật lý trong PDF, bắt đầu từ 1.
    public int PhysicalPageNumber { get; set; }
}