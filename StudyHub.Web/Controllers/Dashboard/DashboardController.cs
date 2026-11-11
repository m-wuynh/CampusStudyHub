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

    [HttpGet("/")]
    [HttpGet("/Dashboard")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
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
        var allCandidateEvents = await calendarService.GetEventsAsync(userId, today, today.AddDays(7));

        var todayEvents = new List<ScheduleEventDto>();
        foreach (var ev in allCandidateEvents)
        {
            var starts = ScheduleOccurrenceService.GetStarts(ev, today, today);
            var duration = ev.EndAtLocal - ev.StartAtLocal;
            foreach (var startDt in starts)
            {
                todayEvents.Add(ev with { StartAtLocal = startDt, EndAtLocal = startDt.Add(duration) });
            }
        }
        todayEvents = todayEvents.OrderBy(e => e.StartAtLocal).ToList();

        // Sự kiện sắp diễn ra: mọi loại sự kiện chưa bắt đầu, trong 7 ngày tới.
        var upcomingSummary = new List<UpcomingEventSummaryDto>();

        foreach (var ev in allCandidateEvents)
        {
            var starts = ScheduleOccurrenceService.GetStarts(ev, today, today.AddDays(7));
            var duration = ev.EndAtLocal - ev.StartAtLocal;
            foreach (var startDt in starts)
            {
                if (startDt < now)
                {
                    continue;
                }

                var evDate = DateOnly.FromDateTime(startDt);
                int daysLeft = evDate.DayNumber - today.DayNumber;

                if (daysLeft > 7)
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
                    startDt.ToString("dd/MM/yyyy"),
                    startDt.ToString("HH:mm"),
                    daysLeft
                );

                upcomingSummary.Add(dto);
            }
        }

        upcomingSummary = upcomingSummary
            .OrderBy(e => e.DaysLeftText)
            .ThenBy(e => e.TimeText)
            .ToList();

        var nextEvent = upcomingSummary.FirstOrDefault();

        // ── 4. Weekly Performance Widget Calculation ──────────────────
        int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-1 * diff);
        var endOfWeek = startOfWeek.AddDays(6);

        var weeklyCandidateEvents = await calendarService.GetEventsAsync(userId, startOfWeek, endOfWeek);
        int weeklyTotal = 0;
        int weeklyCompleted = 0;
        int weeklyMissed = 0;

        foreach (var ev in weeklyCandidateEvents)
        {
            var starts = ScheduleOccurrenceService.GetStarts(ev, startOfWeek, endOfWeek);
            var duration = ev.EndAtLocal - ev.StartAtLocal;
            foreach (var startDt in starts)
            {
                weeklyTotal++;
                var endDt = startDt.Add(duration);
                if (ev.IsCompleted)
                {
                    weeklyCompleted++;
                }
                else if (endDt < now)
                {
                    weeklyMissed++;
                }
            }
        }

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
