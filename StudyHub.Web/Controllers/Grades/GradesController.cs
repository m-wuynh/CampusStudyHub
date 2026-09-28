using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Grades;
using StudyHub.Web.Authentication;
using StudyHub.Web.ViewModels.Grades;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyHub.Web.Controllers.Grades;

[Route("Grades")]
public sealed class GradesController : Controller
{
    private readonly IGradeService gradeService;
    private readonly IGoalService goalService;
    private readonly ICurrentUser currentUser;

    public GradesController(IGradeService gradeService, IGoalService goalService, ICurrentUser currentUser)
    {
        this.gradeService = gradeService;
        this.goalService = goalService;
        this.currentUser = currentUser;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? academicTermId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var data = await gradeService.GetGradePageAsync(userId, academicTermId, cancellationToken);
        var goals = await goalService.GetGoalsAsync(userId, data.SelectedTermId, cancellationToken);
        var finalData = data with { Goals = goals };
        
        return View(new GradesIndexViewModel(finalData));
    }

    [HttpPost("AddGrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGrade(CreateGradeEntryDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.AddGradeEntryAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã lưu điểm thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index");
    }

    [HttpPost("DeleteGrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGrade(long id, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.DeleteGradeEntryAsync(currentUser.UserId, id, cancellationToken);
            TempData["SuccessMessage"] = "Đã xóa điểm thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index");
    }

    [HttpPost("AddGoal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGoal(CreateGoalDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await goalService.CreateGoalAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã thêm mục tiêu học tập!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index");
    }

    [HttpPost("CancelGoal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelGoal(long id, CancellationToken cancellationToken)
    {
        try
        {
            await goalService.CancelGoalAsync(currentUser.UserId, id, cancellationToken);
            TempData["SuccessMessage"] = "Đã hủy mục tiêu thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index");
    }
    [HttpPost("AddTerm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTerm(CreateTermDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.CreateTermAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã thêm học kỳ thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index");
    }

    [HttpPost("ArchiveTerm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveTerm(long id, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.ArchiveTermAsync(currentUser.UserId, id, cancellationToken);
            TempData["SuccessMessage"] = "Đã lưu trữ học kỳ thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index");
    }

    [HttpPost("AddUserSubject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUserSubject(CreateUserSubjectDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.CreateUserSubjectAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã thêm môn học thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index", new { academicTermId = dto.AcademicTermId });
    }

    [HttpPost("ArchiveUserSubject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveUserSubject(long id, [FromForm] long currentTermId, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.ArchiveUserSubjectAsync(currentUser.UserId, id, cancellationToken);
            TempData["SuccessMessage"] = "Đã xóa môn học thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index", new { academicTermId = currentTermId });
    }
    [HttpPost("UpdateGrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGrade(UpdateGradeEntryDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.UpdateGradeEntryAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã cập nhật điểm thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index");
    }

    [HttpPost("UpdateTerm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTerm(UpdateTermDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.UpdateTermAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã cập nhật học kỳ thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index");
    }

    [HttpPost("UpdateUserSubject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserSubject(UpdateUserSubjectDto dto, [FromForm] long currentTermId, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.UpdateUserSubjectAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã cập nhật môn học thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index", new { academicTermId = currentTermId });
    }
    [HttpPost("UpdateGoal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGoal(UpdateGoalDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await goalService.UpdateGoalAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã cập nhật mục tiêu thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index");
    }
}
