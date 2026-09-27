using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Calendar;
using StudyHub.Web.ViewModels.Calendar;

namespace StudyHub.Web.Controllers.Calendar;

[Route("Calendar")]
public sealed class CalendarController(ICalendarService calendarService) : Controller
{
    private const long CurrentUserId = 1;

    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfMonth = new DateOnly(selectedDate.Year, selectedDate.Month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
        
        // Fetch events for the month (and slightly before/after for the grid view if needed, but here we just get a wide range for simplicity, e.g., the whole month)
        var events = await calendarService.GetEventsAsync(CurrentUserId, firstDayOfMonth.AddDays(-7), lastDayOfMonth.AddDays(7));
        
        return View(new CalendarIndexViewModel(selectedDate, events));
    }

    [HttpGet("GetEvents")]
    public async Task<IActionResult> GetEvents(DateTime start, DateTime end)
    {
        var fromDate = DateOnly.FromDateTime(start);
        var toDate = DateOnly.FromDateTime(end);
        var events = await calendarService.GetEventsAsync(CurrentUserId, fromDate, toDate);
        
        var result = new List<object>();
        foreach (var ev in events)
        {
            var starts = ScheduleOccurrenceService.GetStarts(ev, fromDate, toDate);
            var duration = ev.EndAtLocal - ev.StartAtLocal;
            foreach (var startDt in starts)
            {
                result.Add(new
                {
                    id = ev.Id,
                    title = ev.Title,
                    start = startDt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = startDt.Add(duration).ToString("yyyy-MM-ddTHH:mm:ss"),
                    extendedProps = new { 
                        type = ev.EventType,
                        location = ev.Location,
                        description = ev.Description
                    }
                });
            }
        }
        return Json(result);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost("CreateEvent")]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        
        try 
        {
            var newEvent = await calendarService.CreateEventAsync(CurrentUserId, request);
            return Ok(newEvent);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    [HttpGet("Update/{id}")]
    public async Task<IActionResult> Update(long id)
    {
        var ev = await calendarService.GetEventAsync(CurrentUserId, id);
        if (ev == null) return NotFound();
        var model = new CalendarUpdateViewModel(
            ev.Id,
            ev.Title,
            ev.EventType,
            DateOnly.FromDateTime(ev.StartAtLocal),
            TimeOnly.FromDateTime(ev.StartAtLocal),
            TimeOnly.FromDateTime(ev.EndAtLocal),
            ev.Location,
            ev.Description);
        return View(model);
    }

    [HttpPost("Update/{id}")]
    public async Task<IActionResult> UpdateEvent(long id, [FromBody] UpdateEventDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            // Ensure the DTO has the correct Id from the route
            request.Id = id;
            await calendarService.UpdateEventAsync(CurrentUserId, request);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("GetUpcomingNotifications")]
    public async Task<IActionResult> GetUpcomingNotifications()
    {
        var now = DateTime.Now;
        var events = await calendarService.GetActiveRemindersAsync(CurrentUserId);

        // Sort by start time
        var upcoming = events
            .OrderBy(e => e.StartAtLocal)
            .Take(10)
            .Select(e =>
            {
                var diff = e.StartAtLocal - now;
                string timeLabel;
                if (diff.TotalMinutes < 60)
                    timeLabel = $"Còn {(int)diff.TotalMinutes} phút nữa";
                else if (diff.TotalHours < 24)
                    timeLabel = $"Hôm nay lúc {e.StartAtLocal:HH:mm}";
                else if (diff.TotalDays < 2)
                    timeLabel = $"Ngày mai lúc {e.StartAtLocal:HH:mm}";
                else
                    timeLabel = e.StartAtLocal.ToString("ddd, dd/MM 'lúc' HH:mm");

                string icon = e.EventType switch
                {
                    "Exam"      => "bi-alarm",
                    "Class"     => "bi-people",
                    "Other"     => "bi-journal-check",
                    _           => "bi-book"
                };
                string iconBg = e.EventType switch
                {
                    "Exam"  => "var(--sh-rose-50)",
                    "Class" => "var(--sh-indigo-50)",
                    "Other" => "var(--sh-amber-50)",
                    _       => "var(--sh-emerald-50)"
                };
                string iconColor = e.EventType switch
                {
                    "Exam"  => "var(--sh-rose-600)",
                    "Class" => "var(--sh-indigo-600)",
                    "Other" => "var(--sh-amber-600)",
                    _       => "var(--sh-emerald-600)"
                };
                bool isUrgent = diff.TotalHours < 24;

                return new
                {
                    id = e.Id,
                    title = e.Title,
                    timeLabel,
                    icon,
                    iconBg,
                    iconColor,
                    isUrgent,
                    eventType = e.EventType
                };
            }).ToList();

        return Json(new { count = upcoming.Count, items = upcoming });
    }

    [HttpDelete("DeleteEvent/{id}")]
    public async Task<IActionResult> DeleteEvent(long id)
    {
        try
        {
            await calendarService.DeleteEventAsync(CurrentUserId, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Duplicate GetEvents method removed (kept the first implementation)
}
