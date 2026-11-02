using System.Collections.Generic;
using StudyHub.BLL.DTOs;

namespace StudyHub.Web.ViewModels.Grades;

public class ReorderColumnsRequest
{
    public long GradeBookId { get; set; }
    public List<ReorderColumnDto> Columns { get; set; } = new();
}

public class DeleteGradeRequest
{
    public long Id { get; set; }
}

public class ReorderSubjectsRequest
{
    public List<ReorderSubjectDto> Subjects { get; set; } = new();
}

public class DeleteGoalRequest
{
    public long GoalId { get; set; }
}

public class DeleteActivityRequest
{
    public long ActivityId { get; set; }
}

public class ToggleActivityRequest
{
    public long ActivityId { get; set; }
}

