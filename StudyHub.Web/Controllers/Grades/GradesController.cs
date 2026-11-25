using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Grades;
using StudyHub.Web.Authentication;
using StudyHub.Web.ViewModels.Grades;
using System;
using System.Collections.Generic;
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
    private readonly ILogger<GradesController> logger;

    public GradesController(
        IGradeService gradeService,
        IGoalService goalService,
        IGradeBookService gradeBookService,
        ICurrentUser currentUser,
        ILogger<GradesController> logger)
    {
        this.gradeService = gradeService;
        this.goalService = goalService;
        this.gradeBookService = gradeBookService;
        this.currentUser = currentUser;
        this.logger = logger;
    }

    /// <summary>
    /// Chỉ trả nguyên văn thông điệp của lỗi nghiệp vụ do BLL StudyHub ném ra;
    /// lỗi database/hệ thống được log và đổi thành thông điệp chung (không lộ chi tiết SQL).
    /// </summary>
    private string ToUserMessage(Exception ex)
    {
        var thrownByBusinessLayer = ex.TargetSite?.DeclaringType?.Namespace?.StartsWith("StudyHub.BLL", StringComparison.Ordinal) == true;

        switch (ex)
        {
            case ArgumentException or InvalidOperationException or UnauthorizedAccessException when thrownByBusinessLayer:
                return ex.Message;
            case DbUpdateConcurrencyException:
                return "Dữ liệu vừa được thay đổi ở nơi khác. Vui lòng tải lại trang và thử lại.";
            case DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }:
                return "Dữ liệu bị trùng với bản ghi đã có (ví dụ tên hoặc giá trị đã tồn tại).";
            case DbUpdateException { InnerException: SqlException { Number: 547 } }:
                return "Không thể lưu vì dữ liệu đang được tham chiếu hoặc không hợp lệ.";
        }

        logger.LogError(ex, "Grades/Goals request failed");
        return "Đã xảy ra lỗi, vui lòng thử lại.";
    }

    // ── Index ────────────────────────────────────────────────────

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpGet("")]
    public async Task<IActionResult> Index(long? gradeBookId, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
            return View(new GradesIndexViewModel(new GradesPageDto(
                [], null, null, [], new YearGradeSummaryDto(null, null, "", null, "", 0), [], [], [])));
        var userId = currentUser.UserId;
        var data = await gradeService.GetGradePageAsync(userId, gradeBookId, cancellationToken);
        return View(new GradesIndexViewModel(data));
    }

    // ── GradeBook ────────────────────────────────────────────────

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
            return Json(new { ok = false, message = ToUserMessage(ex) });
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
            TempData["ErrorMessage"] = ToUserMessage(ex);
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
            return Json(new { ok = false, message = ToUserMessage(ex) });
        }
    }

    [HttpPost("DeleteGradeBook")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGradeBook(long id, CancellationToken cancellationToken)
    {
        try
        {
            await gradeBookService.DeleteGradeBookAsync(currentUser.UserId, id, cancellationToken);
            TempData["SuccessMessage"] = "Đã xóa vĩnh viễn sổ điểm.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ToUserMessage(ex);
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
            TempData["ErrorMessage"] = ToUserMessage(ex);
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
            TempData["ErrorMessage"] = ToUserMessage(ex);
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
            return Json(new { ok = false, error = ToUserMessage(ex) });
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
            TempData["ErrorMessage"] = ToUserMessage(ex);
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
            return Json(new { ok = false, error = ToUserMessage(ex) });
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
            return Json(new { ok = false, error = ToUserMessage(ex) });
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
            return Json(new { ok = false, error = ToUserMessage(ex) });
        }
    }

    // ── Goals ────────────────────────────────────────────────────

    [HttpPost("AddGoal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGoal(CreateGoalDto dto, [FromForm] long? currentGradeBookId, CancellationToken cancellationToken)
    {
        try
        {
            await goalService.CreateGoalAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã thêm mục tiêu học tập!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ToUserMessage(ex);
        }
        return RedirectToAction("Index", currentGradeBookId.HasValue && currentGradeBookId.Value > 0 ? new { gradeBookId = currentGradeBookId.Value } : null);
    }

    [HttpPost("UpdateGoal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGoal(UpdateGoalDto dto, [FromForm] long? currentGradeBookId, CancellationToken cancellationToken)
    {
        try
        {
            await goalService.UpdateGoalAsync(currentUser.UserId, dto, cancellationToken);
            TempData["SuccessMessage"] = "Đã cập nhật mục tiêu thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ToUserMessage(ex);
        }
        return RedirectToAction("Index", currentGradeBookId.HasValue && currentGradeBookId.Value > 0 ? new { gradeBookId = currentGradeBookId.Value } : null);
    }

    [HttpPost("DeleteGoal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGoal([FromBody] DeleteGoalRequest req, CancellationToken cancellationToken)
    {
        try
        {
            await goalService.DeleteGoalAsync(currentUser.UserId, req.GoalId, cancellationToken);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ToUserMessage(ex) });
        }
    }

    // ── Goal Activities ──────────────────────────────────────────

    [HttpPost("AddGoalActivity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGoalActivity([FromBody] CreateGoalActivityDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var activity = await goalService.AddGoalActivityAsync(currentUser.UserId, dto, cancellationToken);
            return Json(new { ok = true, data = activity });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ToUserMessage(ex) });
        }
    }

    [HttpPost("UpdateGoalActivity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGoalActivity([FromBody] UpdateGoalActivityDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await goalService.UpdateGoalActivityAsync(currentUser.UserId, dto, cancellationToken);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ToUserMessage(ex) });
        }
    }

    [HttpPost("DeleteGoalActivity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGoalActivity([FromBody] DeleteActivityRequest req, CancellationToken cancellationToken)
    {
        try
        {
            await goalService.DeleteGoalActivityAsync(currentUser.UserId, req.ActivityId, cancellationToken);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ToUserMessage(ex) });
        }
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
            TempData["ErrorMessage"] = ToUserMessage(ex);
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
            TempData["ErrorMessage"] = ToUserMessage(ex);
        }
        return RedirectToAction("Index", new { gradeBookId = currentGradeBookId, editMode = "structure" });
    }

    [HttpPost("DeleteUserSubject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserSubject(long id, [FromForm] long? currentGradeBookId, CancellationToken cancellationToken)
    {
        try
        {
            await gradeService.DeleteUserSubjectAsync(currentUser.UserId, id, cancellationToken);
            TempData["SuccessMessage"] = "Đã xóa vĩnh viễn môn học.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ToUserMessage(ex);
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
            return Json(new { ok = false, error = ToUserMessage(ex) });
        }
    }
}
