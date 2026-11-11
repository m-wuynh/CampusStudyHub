using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Calendar;
using StudyHub.BLL.Services.Dashboard;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.BLL.Services.Grades;
using StudyHub.Web.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace StudyHub.Web.Controllers.Dashboard;

[Authorize]
public sealed class DashboardController(
    ICurrentUser currentUser,
    ICalendarService calendarService,
    IGradeService gradeService,
    IFlashcardDeckService flashcardDeckService,
    IDashboardNotesService dashboardNotesService) : Controller
{
    private long CurrentUserId => currentUser.UserId > 0 ? currentUser.UserId : (long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 1);

    [AllowAnonymous]
    [HttpGet("/")]
    [HttpGet("/Dashboard")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
            return View(new DashboardIndexViewModel(
                "Khách", DateTime.Today.ToString("dd/MM/yyyy"), null,
                "Chào mừng đến với Study Hub", 0, 0, null, null, null, null,
                [], [], new FlashcardSummaryDto(0, 0, [])));

        var userId = currentUser.UserId > 0 ? currentUser.UserId : CurrentUserId;
        var displayName = !string.IsNullOrWhiteSpace(currentUser.DisplayName) ? currentUser.DisplayName : (User.Identity?.Name ?? "Sinh viên");

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);

        // ── 1. Date & Greeting ─────────────────────────────────────────
        var dayOfWeek = now.DayOfWeek switch
        {
            DayOfWeek.Sunday => "Chủ Nhật",
            DayOfWeek.Monday => "Thứ Hai",
            DayOfWeek.Tuesday => "Thứ Ba",
            DayOfWeek.Wednesday => "Thứ Tư",
            DayOfWeek.Thursday => "Thứ Năm",
            DayOfWeek.Friday => "Thứ Sáu",
            DayOfWeek.Saturday => "Thứ Bảy",
            _ => ""
        };

        var formattedDateText = $"{dayOfWeek}, {now:dd/MM/yyyy}";

        var hour = now.Hour;
        string timeGreeting = hour switch
        {
            >= 5 and < 12 => $"Chào buổi sáng, {displayName} 👋",
            >= 12 and < 18 => $"Chào buổi chiều, {displayName} 👋",
            _ => $"Chào buổi tối, {displayName} 👋"
        };

        // ── 2. Grades & Goals Data ─────────────────────────────────────
        var gradesPage = await gradeService.GetGradePageAsync(userId, null, cancellationToken, includeSubjectOptions: false);
        var currentAvg = gradesPage.Summary.CurrentAverage10;

        string? academicTermLabel = gradesPage.GradeBook?.Name;

        // Goals – Q15 = A: tính trong BLL (GoalOverviewCalculator).
        GoalSummaryDto? goalSummary = null;
        var overview = GoalOverviewCalculator.Build(gradesPage.GoalLadders);
        if (overview != null)
        {
            goalSummary = new GoalSummaryDto(
                currentAvg,
                overview.TargetAverage,
                overview.OverallProgressPercent,
                overview.Highlights.Select(h => new HighlightedGoalDto(h.SubjectName, h.TargetValue, h.CurrentValue, h.ProgressPercent)).ToList());
        }

        // ── 3. Schedule & Events Data ──────────────────────────────────
        var todayEvents = await calendarService.GetEventsAsync(userId, today, today);
        var upcomingEventsRaw = await calendarService.GetEventsAsync(userId, today, today.AddDays(7));

        // Sự kiện sắp diễn ra: mọi loại sự kiện chưa bắt đầu, trong 7 ngày tới.
        var upcomingSummary = new List<UpcomingEventSummaryDto>();

        foreach (var ev in upcomingEventsRaw.OrderBy(e => e.StartAtLocal))
        {
            var evDate = DateOnly.FromDateTime(ev.StartAtLocal);
            int daysLeft = evDate.DayNumber - today.DayNumber;

            if (ev.StartAtLocal < now || daysLeft > 7)
            {
                continue;
            }

            string typeLabel = ev.EventType switch
            {
                "Exam" => "Kiểm tra",
                "Class" => "Lịch học",
                "Other" => "Bài tập",
                _ => "Tự học"
            };

            string badgeCss = ev.EventType switch
            {
                "Exam" => "exam",
                "Class" => "group",
                "Other" => "homework",
                _ => "study"
            };

            var dto = new UpcomingEventSummaryDto(
                ev.Id,
                ev.Title,
                ev.EventType,
                typeLabel,
                badgeCss,
                ev.StartAtLocal.ToString("dd/MM/yyyy"),
                ev.StartAtLocal.ToString("HH:mm"),
                daysLeft
            );

            upcomingSummary.Add(dto);
        }

        var nextEvent = upcomingSummary.FirstOrDefault();

        // ── 4. Weekly Performance Widget Calculation ──────────────────
        int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-1 * diff);
        var endOfWeek = startOfWeek.AddDays(6);

        var weeklyEvents = await calendarService.GetEventsAsync(userId, startOfWeek, endOfWeek);
        int weeklyTotal = weeklyEvents.Count;
        int weeklyCompleted = weeklyEvents.Count(e => e.IsCompleted);
        int weeklyMissed = weeklyEvents.Count(e => !e.IsCompleted && e.EndAtLocal < now);

        // ── 5. Flashcards Data ────────────────────────────────────────
        var decks = await flashcardDeckService.GetMyDecksAsync(userId, cancellationToken);
        int totalDecks = decks.Count;

        var flashcardSummary = new FlashcardSummaryDto(totalDecks, totalDecks, decks.Take(3).ToList());

        // ── 6. Recent Notes (NotesV3) ─────────────────────────────────
        var recentNotes = await dashboardNotesService.GetRecentNotesAsync(userId, 4, cancellationToken);

        // Build ViewModel
        var model = new DashboardIndexViewModel(
            displayName,
            formattedDateText,
            academicTermLabel,
            timeGreeting,
            todayEvents.Count,
            todayEvents.Count(e => e.IsCompleted),
            nextEvent,
            currentAvg,
            goalSummary,
            todayEvents.OrderBy(e => e.StartAtLocal).ToList(),
            upcomingSummary,
            flashcardSummary,
            weeklyTotal,
            weeklyCompleted,
            weeklyMissed,
            RecentNotes: recentNotes.Notes,
            NotesAvailable: recentNotes.IsAvailable
        );

        return View(model);
    }
}
