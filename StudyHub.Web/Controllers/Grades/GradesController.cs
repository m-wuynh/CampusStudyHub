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
    private readonly IGradeBookService gradeBookService;
    private readonly ICurrentUser currentUser;

    public GradesController(
        IGradeService gradeService,
        IGoalService goalService,
        IGradeBookService gradeBookService,
        ICurrentUser currentUser)
    {
        this.gradeService = gradeService;
        this.goalService = goalService;
        this.gradeBookService = gradeBookService;
        this.currentUser = currentUser;
    }

    // ── Index ────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(long? gradeBookId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var data = await gradeService.GetGradePageAsync(userId, gradeBookId, cancellationToken);
        return View(new GradesIndexViewModel(data));
    }

    // ── GradeBook ────────────────────────────────────────────────

    [HttpPost("CreateGradeBook")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGradeBook(CreateGradeBookDto dto, CancellationToken cancellationToken)
    {
        long? newId = null;
        try
        {
            var result = await gradeBookService.CreateGradeBookAsync(currentUser.UserId, dto, cancellationToken);
            newId = result.GradeBookId;
            TempData["SuccessMessage"] = "Đã tạo sổ điểm thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        }
        return RedirectToAction("Index", new { gradeBookId = newId });
    }

    [HttpPost("UpdateGradeBook")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGradeBook(UpdateGradeBookDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await gradeBookService.UpdateGradeBookAsync(currentUser.UserId, dto, cancellationToken);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, message = ex.InnerException != null ? ex.InnerException.Message : ex.Message });
        }
    }

    [HttpPost("CreateGradeBookWithColumns")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGradeBookWithColumns(CreateGradeBookWithColumnsDto dto, CancellationToken cancellationToken)
    {
        long? newId = null;
        try
        {
            var result = await gradeBookService.CreateGradeBookWithColumnsAsync(currentUser.UserId, dto, cancellationToken);
            newId = result.GradeBookId;
            TempData["SuccessMessage"] = "Đã tạo sổ điểm thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        }
        return RedirectToAction("Index", new { gradeBookId = newId });
    }

    [HttpPost("SaveSubjectGrades")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSubjectGrades([FromBody] SaveSubjectGradesDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.SaveSubjectGradesAsync(currentUser.UserId, dto, cancellationToken);
            return Json(new { ok = true, message = "Đã cập nhật điểm môn học thành công!" });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, message = ex.InnerException != null ? ex.InnerException.Message : ex.Message });
        }
    }

    [HttpPost("DeleteGradeBook")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGradeBook(long id, CancellationToken cancellationToken)
    {
        try
        {
            await gradeBookService.DeleteGradeBookAsync(currentUser.UserId, id, cancellationToken);
            TempData["SuccessMessage"] = "Đã xóa bảng điểm thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index");
    }

    // ── GradeColumn ──────────────────────────────────────────────

    [HttpPost("AddColumn")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddColumn(CreateGradeColumnDto dto, [FromForm] long currentGradeBookId, CancellationToken cancellationToken)
    {
        try
        {
            await gradeBookService.AddColumnAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã thêm cột đánh giá thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index", new { gradeBookId = currentGradeBookId, editMode = "structure" });
    }

    [HttpPost("UpdateColumn")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateColumn(UpdateGradeColumnDto dto, [FromForm] long currentGradeBookId, CancellationToken cancellationToken)
    {
        try
        {
            await gradeBookService.UpdateColumnAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã cập nhật cột đánh giá thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index", new { gradeBookId = currentGradeBookId, editMode = "structure" });
    }

    [HttpPost("ReorderColumns")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderColumns([FromBody] ReorderColumnsRequest req, CancellationToken cancellationToken)
    {
        try
        {
            await gradeBookService.ReorderColumnsAsync(currentUser.UserId, req.GradeBookId, req.Columns, cancellationToken);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ex.Message });
        }
    }

    [HttpPost("DeleteColumn")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteColumn(long id, [FromForm] long currentGradeBookId, CancellationToken cancellationToken)
    {
        try
        {
            await gradeBookService.DeleteColumnAsync(currentUser.UserId, id, cancellationToken);
            TempData["SuccessMessage"] = "Đã xóa cột đánh giá thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index", new { gradeBookId = currentGradeBookId, editMode = "structure" });
    }

    // ── Grade Entries ────────────────────────────────────────────

    [HttpPost("AddGrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGrade([FromBody] CreateGradeEntryDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.AddGradeEntryAsync(currentUser.UserId, dto, cancellationToken);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ex.Message });
        }
    }

    [HttpPost("UpdateGrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGrade([FromBody] UpdateGradeEntryDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.UpdateGradeEntryAsync(currentUser.UserId, dto, cancellationToken);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ex.Message });
        }
    }

    [HttpPost("DeleteGrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGrade([FromBody] DeleteGradeRequest req, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.DeleteGradeEntryAsync(currentUser.UserId, req.Id, cancellationToken);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ex.Message });
        }
    }

    // ── Goals ────────────────────────────────────────────────────

    [HttpPost("AddGoal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGoal(CreateGoalDto dto, [FromForm] long currentGradeBookId, CancellationToken cancellationToken)
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
        return RedirectToAction("Index", new { gradeBookId = currentGradeBookId });
    }

    [HttpPost("UpdateGoal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGoal(UpdateGoalDto dto, [FromForm] long currentGradeBookId, CancellationToken cancellationToken)
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
        return RedirectToAction("Index", new { gradeBookId = currentGradeBookId });
    }

    [HttpPost("CancelGoal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelGoal(long id, [FromForm] long currentGradeBookId, CancellationToken cancellationToken)
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
        return RedirectToAction("Index", new { gradeBookId = currentGradeBookId });
    }

    // ── Academic Term ────────────────────────────────────────────

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

    // ── User Subject ─────────────────────────────────────────────

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
        return RedirectToAction("Index", new { gradeBookId = dto.GradeBookId, editMode = "structure" });
    }

    [HttpPost("UpdateUserSubject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserSubject(UpdateUserSubjectDto dto, [FromForm] long currentGradeBookId, CancellationToken cancellationToken)
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
        return RedirectToAction("Index", new { gradeBookId = currentGradeBookId, editMode = "structure" });
    }

    [HttpPost("DeleteUserSubject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserSubject(long id, [FromForm] long currentGradeBookId, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.DeleteUserSubjectAsync(currentUser.UserId, id, cancellationToken);
            TempData["SuccessMessage"] = "Đã xóa môn học thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction("Index", new { gradeBookId = currentGradeBookId, editMode = "structure" });
    }

    [HttpPost("ReorderSubjects")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderSubjects([FromBody] ReorderSubjectsRequest req, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.ReorderSubjectsAsync(currentUser.UserId, req.Subjects, cancellationToken);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ex.Message });
        }
    }
}

public class ReorderColumnsRequest
{
    public long GradeBookId { get; set; }
    public System.Collections.Generic.List<ReorderColumnDto> Columns { get; set; } = new();
}

public class DeleteGradeRequest
{
    public long Id { get; set; }
}

public class ReorderSubjectsRequest
{
    public System.Collections.Generic.List<ReorderSubjectDto> Subjects { get; set; } = new();
}


