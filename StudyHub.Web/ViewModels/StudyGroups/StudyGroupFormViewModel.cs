using StudyHub.BLL.DTOs.StudyGroups;
using System.ComponentModel.DataAnnotations;
using StudyHub.BLL.DTOs;
namespace StudyHub.Web.ViewModels.StudyGroups;

public sealed class StudyGroupFormViewModel : IValidatableObject
{
    public string? GroupId { get; set; }
    [Required(ErrorMessage = "Hãy nhập tên nhóm."), StringLength(150, ErrorMessage = "Tên nhóm không được dài quá 150 ký tự.")]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "Hãy chọn ít nhất một môn học."), MinLength(1, ErrorMessage = "Hãy chọn ít nhất một môn học.")]
    public List<string> Subjects { get; set; } = [];
    [StringLength(2000, ErrorMessage = "Mô tả không được dài quá 2000 ký tự.")] public string? Description { get; set; }
    [StringLength(500, ErrorMessage = "Mục tiêu không được dài quá 500 ký tự.")] public string? Goal { get; set; }
    [Required(ErrorMessage = "Hãy chọn hình thức học."), RegularExpression("Online|Offline|Hybrid", ErrorMessage = "Hình thức học không hợp lệ.")]
    public string MeetingFormat { get; set; } = "Online";
    [StringLength(250, ErrorMessage = "Lịch dự kiến không được dài quá 250 ký tự.")] public string? MeetingSchedule { get; set; }
    [StringLength(2000, ErrorMessage = "Nội quy không được dài quá 2000 ký tự.")] public string? Rules { get; set; }
    [Required(ErrorMessage = "Hãy chọn cách tham gia."), RegularExpression("Approval|Open|InviteOnly", ErrorMessage = "Cách tham gia không hợp lệ.")]
    public string JoinMode { get; set; } = "Approval";
    [Range(2, 500, ErrorMessage = "Giới hạn người phải là số nguyên từ 2 đến 500.")] public short? MaxMembers { get; set; }
    public IReadOnlyList<GroupSubjectOptionResponse> SubjectOptions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Subjects.Any(string.IsNullOrWhiteSpace))
            yield return new ValidationResult("Tên môn học không được để trống.", [nameof(Subjects)]);
        if (Subjects.Any(subject => subject?.Trim().Length > 150))
            yield return new ValidationResult("Tên môn học không được dài quá 150 ký tự.", [nameof(Subjects)]);
    }

    public CreateGroupRequest ToCreateRequest() => new(Name, null, Subjects, Description, Goal,
        MeetingFormat, MeetingSchedule, Rules, JoinMode != "InviteOnly", JoinMode, MaxMembers);
    public UpdateGroupRequest ToUpdateRequest() => new(Name, null, Subjects, Description, Goal,
        MeetingFormat, MeetingSchedule, Rules, JoinMode != "InviteOnly", JoinMode, MaxMembers);
}
