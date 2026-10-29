namespace StudyHub.DAL.Entities.NotesV3;

public sealed class NotesV3DocumentShare
{
    // Giữ tên ID đang dùng trong PageLink/Annotation/Reference.
    // Đây là khóa của NotesV3DocumentShares, không phải bảng cũ.
    public long GroupDocumentShareId { get; set; }

    public long DocumentId { get; set; }
    public long StudyGroupId { get; set; }
    public long SharedByUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime SharedAtUtc { get; set; }
    public DateTime? UnsharedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}