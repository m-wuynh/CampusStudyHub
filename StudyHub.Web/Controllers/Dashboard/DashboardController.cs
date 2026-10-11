using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Dashboard;

using System.Security.Claims;
using StudyHub.BLL.Services.Calendar;

namespace StudyHub.Web.Controllers.Dashboard;

public sealed class DashboardController(ICalendarService calendarService) : Controller
{
    private long CurrentUserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 1;

    [HttpGet("/")]
    [HttpGet("/Dashboard")]
    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        // Calculate the current week's Monday
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-1 * diff);
        var endOfWeek = startOfWeek.AddDays(6);

        var fromDate = DateOnly.FromDateTime(startOfWeek);
        var toDate = DateOnly.FromDateTime(endOfWeek);

        var weeklyEvents = await calendarService.GetEventsAsync(CurrentUserId, fromDate, toDate);

        int total = weeklyEvents.Count;
        int completed = weeklyEvents.Count(e => e.IsCompleted);
        int missed = weeklyEvents.Count(e => !e.IsCompleted && e.EndAtLocal < DateTime.Now);

        var vm = new DashboardIndexViewModel(
            User.Identity?.Name ?? "Sinh viên", 
            12, 
            total, 
            completed, 
            missed);

        return View(vm);
    }
}
