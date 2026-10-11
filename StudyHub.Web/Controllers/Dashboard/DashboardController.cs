using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Calendar;
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
    IFlashcardDeckService flashcardDeckService) : Controller
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
        var gradesPage = await gradeService.GetGradePageAsync(userId, null, cancellationToken);
        var currentAvg = gradesPage.Summary.CurrentAverage10;

        string? currentRankText = currentAvg.HasValue
            ? (currentAvg >= 8.0m ? "Học sinh Giỏi" : currentAvg >= 6.5m ? "Khá" : "Trung bình")
            : null;

        string? academicTermLabel = gradesPage.GradeBook?.Name;

        // Goals logic
        GoalSummaryDto? goalSummary = null;
        var allGoals = gradesPage.GoalLadders.SelectMany(l => l.Goals.Where(g => g.Status != GoalStatus.Cancelled)).ToList();

        if (allGoals.Any())
        {
            var activeGoal = allGoals.FirstOrDefault(g => g.Status == GoalStatus.InProgress)
                             ?? allGoals.OrderByDescending(g => g.TargetValue).FirstOrDefault();

            decimal targetAvg = activeGoal?.TargetValue ?? 8.0m;
            int overallProgress = currentAvg.HasValue && targetAvg > 0
                ? Math.Min(100, (int)Math.Floor(currentAvg.Value / targetAvg * 100m))
                : 0;

            var highlightedGoals = new List<HighlightedGoalDto>();
            foreach (var ladder in gradesPage.GoalLadders)
            {
                var curGoal = ladder.Goals.FirstOrDefault(g => g.Status == GoalStatus.InProgress || g.Status == GoalStatus.Achieved);
                if (curGoal != null)
                {
                    int pct = ladder.CurrentAverage10.HasValue && curGoal.TargetValue > 0
                        ? Math.Min(100, (int)Math.Floor(ladder.CurrentAverage10.Value / curGoal.TargetValue * 100m))
                        : 0;

                    highlightedGoals.Add(new HighlightedGoalDto(
                        ladder.SubjectName,
                        curGoal.TargetValue,
                        ladder.CurrentAverage10,
                        pct
                    ));
                }
            }

            goalSummary = new GoalSummaryDto(currentAvg, targetAvg, overallProgress, highlightedGoals.Take(3).ToList());
        }

        // ── 3. Schedule & Events Data ──────────────────────────────────
        var todayEvents = await calendarService.GetEventsAsync(userId, today, today);
        var upcomingEventsRaw = await calendarService.GetEventsAsync(userId, today, today.AddDays(7));

        var upcomingSummary = new List<UpcomingEventSummaryDto>();
        UpcomingEventSummaryDto? nextExam = null;

        foreach (var ev in upcomingEventsRaw.OrderBy(e => e.StartAtLocal))
        {
            var evDate = DateOnly.FromDateTime(ev.StartAtLocal);
            int daysLeft = evDate.DayNumber - today.DayNumber;

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
                daysLeft >= 0 ? daysLeft : null
            );

            if (ev.EventType == "Exam" && nextExam == null && daysLeft >= 0)
            {
                nextExam = dto;
            }

            if (daysLeft >= 0)
            {
                upcomingSummary.Add(dto);
            }
        }

        if (nextExam == null && upcomingSummary.Any())
        {
            nextExam = upcomingSummary.First();
        }

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

        // Build ViewModel
        var model = new DashboardIndexViewModel(
            displayName,
            formattedDateText,
            academicTermLabel,
            timeGreeting,
            todayEvents.Count,
            todayEvents.Count(e => e.IsCompleted),
            nextExam,
            currentAvg,
            currentRankText,
            goalSummary,
            todayEvents.OrderBy(e => e.StartAtLocal).ToList(),
            upcomingSummary.Take(4).ToList(),
            flashcardSummary,
            weeklyTotal,
            weeklyCompleted,
            weeklyMissed
        );

        return View(model);
    }
}
