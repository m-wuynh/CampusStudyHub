namespace StudyHub.DAL.Entities;

public sealed class GroupInvite
{
    public string InviteCode { get; set; } = null!;
    public long StudyGroupId { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public int MaxUses { get; set; }
    public int UseCount { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public StudyGroup StudyGroup { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
