using System.ComponentModel.DataAnnotations;
using StudyHub.BLL.DTOs;
namespace StudyHub.Web.ViewModels.StudyGroups;

public sealed class StudyGroupFormViewModel
{
    public string? GroupId { get; set; }
    [Required(ErrorMessage = "Hãy nhập tên nhóm."), StringLength(150)]
    public string Name { get; set; } = "";
    [MinLength(1, ErrorMessage = "Hãy chọn ít nhất một môn học."), MaxLength(10)]
    public List<string> Subjects { get; set; } = [];
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(500)] public string? Goal { get; set; }
    [Required, RegularExpression("Online|Offline|Hybrid")]
    public string MeetingFormat { get; set; } = "Online";
    [StringLength(250)] public string? MeetingSchedule { get; set; }
    [StringLength(2000)] public string? Rules { get; set; }
    [Required, RegularExpression("Approval|Open|InviteOnly")]
    public string JoinMode { get; set; } = "Approval";
    [Range(2, 500)] public short? MaxMembers { get; set; }
    public IReadOnlyList<GroupSubjectOptionResponse> SubjectOptions { get; set; } = [];

    public CreateGroupRequest ToCreateRequest() => new(Name, null, Subjects, Description, Goal,
        MeetingFormat, MeetingSchedule, Rules, JoinMode != "InviteOnly", JoinMode, MaxMembers);
    public UpdateGroupRequest ToUpdateRequest() => new(Name, null, Subjects, Description, Goal,
        MeetingFormat, MeetingSchedule, Rules, JoinMode != "InviteOnly", JoinMode, MaxMembers);
}
