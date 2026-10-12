namespace StudyHub.DAL.Entities;

public sealed class GroupChangeRequest
{
    public long GroupChangeRequestId { get; set; }
    public long StudyGroupId { get; set; }
    public long RequesterUserId { get; set; }
    public long? ReviewerUserId { get; set; }
    public long? TargetGroupNoteId { get; set; }
    public long? DocumentVersionId { get; set; }
    public string TargetType { get; set; } = null!;
    public string Operation { get; set; } = null!;
    public string? ProposedTitle { get; set; }
    public string? ProposedPayloadJson { get; set; }
    public string? BaseVersionToken { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ResolutionNote { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
