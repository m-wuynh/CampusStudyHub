using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Calendar;
using StudyHub.Web.ViewModels.Calendar;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace StudyHub.Web.Controllers.Calendar;

[Authorize]
[Route("Calendar")]
public sealed class CalendarController(ICalendarService calendarService, ICalendarStatisticsService statisticsService) : Controller
{
    private long CurrentUserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfMonth = new DateOnly(selectedDate.Year, selectedDate.Month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
        
        // Fetch events for the month (and slightly before/after for the grid view if needed, but here we just get a wide range for simplicity, e.g., the whole month)
        var events = await calendarService.GetEventsAsync(CurrentUserId, firstDayOfMonth.AddDays(-7), lastDayOfMonth.AddDays(7));
        var weeklyProgress = await statisticsService.GetWeeklyProgressAsync(CurrentUserId, selectedDate);
        
        return View(new CalendarIndexViewModel(selectedDate, events, weeklyProgress));
    }

    [HttpGet("GetWeeklyProgress")]
    public async Task<IActionResult> GetWeeklyProgress([FromQuery] DateOnly? date, [FromQuery] string? category = null)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var progress = await statisticsService.GetWeeklyProgressAsync(CurrentUserId, targetDate, category);
        return Json(progress);
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
                        description = ev.Description,
                        goalId = ev.GoalId,
                        isCompleted = ev.IsCompleted
                    }
                });
            }
        }
        return Json(result);
    }

    [HttpGet("SearchEvents")]
    public async Task<IActionResult> SearchEvents([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Json(new List<object>());

        var events = await calendarService.SearchEventsAsync(CurrentUserId, q.Trim());
        var now = DateTime.Now;

        var result = events.Select(ev =>
        {
            var isPast = ev.EndAtLocal < now;
            var isCompleted = ev.IsCompleted;
            var status = isCompleted ? "completed" : (isPast ? "missed" : "pending");
            var statusText = isCompleted ? "Đã hoàn thành" : (isPast ? "Đã quá hạn" : "Sắp tới");

            return new
            {
                id = ev.Id,
                title = ev.Title,
                eventType = ev.EventType,
                start = ev.StartAtLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = ev.EndAtLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
                startDateStr = ev.StartAtLocal.ToString("dd/MM/yyyy"),
                startTimeStr = ev.StartAtLocal.ToString("HH:mm"),
                endTimeStr = ev.EndAtLocal.ToString("HH:mm"),
                dateFormatted = ev.StartAtLocal.ToString("ddd, dd 'thg' MM, yyyy", new System.Globalization.CultureInfo("vi-VN")),
                location = ev.Location,
                description = ev.Description,
                isCompleted = ev.IsCompleted,
                isPast = isPast,
                status = status,
                statusText = statusText
            };
        });

        return Json(result);
    }

    [HttpPost("ToggleCompletion/{id}")]
    public async Task<IActionResult> ToggleCompletion(long id)
    {
        try
        {
            var isCompleted = await calendarService.ToggleEventCompletionAsync(CurrentUserId, id);
            return Ok(new { ok = true, isCompleted });
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost("CreateEvent")]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            Console.WriteLine("ModelState is invalid: " + string.Join(", ", errors));
            return BadRequest(ModelState);
        }
        
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

        if (ev.EndAtLocal < DateTime.Now || ev.IsCompleted)
        {
            TempData["ErrorMessage"] = "Sự kiện đã kết thúc hoặc đã hoàn thành, chỉ được phép xóa chứ không thể chỉnh sửa.";
            return RedirectToAction("Index");
        }

        var reminders = await calendarService.GetEventRemindersAsync(CurrentUserId, id);
        var model = new CalendarUpdateViewModel(
            ev.Id,
            ev.Title,
            ev.EventType,
            DateOnly.FromDateTime(ev.StartAtLocal),
            TimeOnly.FromDateTime(ev.StartAtLocal),
            TimeOnly.FromDateTime(ev.EndAtLocal),
            ev.Location,
            ev.Description,
            reminders.Select(r => r.Minutes).ToList(),
            reminders);
        return View(model);
    }

    [HttpPost("Update/{id}")]
    [HttpPost("UpdateEvent/{id}")]
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

    [HttpGet("GetEventReminders/{id}")]
    public async Task<IActionResult> GetEventReminders(long id)
    {
        var reminders = await calendarService.GetEventRemindersAsync(CurrentUserId, id);
        return Json(reminders);
    }

    [HttpPost("ToggleEventCompletion/{id}")]
    public async Task<IActionResult> ToggleEventCompletion(long id, [FromBody] bool isCompleted)
    {
        try
        {
            await calendarService.ToggleEventCompletionAsync(CurrentUserId, id, isCompleted);
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

        // Chỉ lấy các sự kiện chưa kết thúc và không bắt đầu quá 24h trước
        var upcoming = events
            .Where(e => e.EndAtLocal >= now && (now - e.StartAtLocal).TotalHours <= 24)
            .OrderBy(e => e.StartAtLocal)
            .Take(10)
            .Select(e =>
            {
                var diff = e.StartAtLocal - now;
                string timeLabel;
                if (diff.TotalMinutes <= 0)
                {
                    if (now <= e.EndAtLocal)
                        timeLabel = $"Đang diễn ra (từ {e.StartAtLocal:HH:mm})";
                    else
                        timeLabel = $"Đã diễn ra ({e.StartAtLocal:dd/MM HH:mm})";
                }
                else if (diff.TotalMinutes < 60)
                {
                    int mins = (int)Math.Ceiling(diff.TotalMinutes);
                    timeLabel = $"Còn {Math.Max(1, mins)} phút nữa";
                }
                else if (diff.TotalHours < 24)
                    timeLabel = $"Hôm nay lúc {e.StartAtLocal:HH:mm}";
                else if (diff.TotalDays < 2)
                    timeLabel = $"Ngày mai lúc {e.StartAtLocal:HH:mm}";
                else
                    timeLabel = e.StartAtLocal.ToString("ddd, dd/MM 'lúc' HH:mm");

                string icon = e.EventType switch
                {
                    "Kiểm tra / Thi"    => "bi-alarm",
                    "Học nhóm"          => "bi-people",
                    "Bài tập / Nộp bài" => "bi-journal-check",
                    _                   => "bi-book"
                };
                string iconBg = e.EventType switch
                {
                    "Kiểm tra / Thi"    => "var(--sh-rose-50)",
                    "Học nhóm"          => "var(--sh-indigo-50)",
                    "Bài tập / Nộp bài" => "var(--sh-amber-50)",
                    _                   => "var(--sh-emerald-50)"
                };
                string iconColor = e.EventType switch
                {
                    "Kiểm tra / Thi"    => "var(--sh-rose-600)",
                    "Học nhóm"          => "var(--sh-indigo-600)",
                    "Bài tập / Nộp bài" => "var(--sh-amber-600)",
                    _                   => "var(--sh-emerald-600)"
                };
                bool isUrgent = diff.TotalHours < 24 && diff.TotalMinutes > 0;

                return new
                {
                    id = e.Id,
                    startAt = e.StartAtLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
                    endAt = e.EndAtLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
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

    [HttpGet("Categories")]
    public async Task<IActionResult> Categories()
    {
        var categories = await calendarService.GetCategoriesAsync(CurrentUserId);
        return View(categories);
    }

    [HttpGet("Categories/Create")]
    public IActionResult CreateCategory()
    {
        return View();
    }

    [HttpGet("Categories/Edit/{id}")]
    public async Task<IActionResult> EditCategory(long id)
    {
        var categories = await calendarService.GetCategoriesAsync(CurrentUserId);
        var cat = categories.FirstOrDefault(c => c.Id == id);
        if (cat == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy loại sự kiện cần chỉnh sửa.";
            return RedirectToAction(nameof(Categories));
        }
        return View(cat);
    }

    [HttpGet("GetCategories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await calendarService.GetCategoriesAsync(CurrentUserId);
        return Json(categories);
    }

    [HttpPost("Categories/Create")]
    public async Task<IActionResult> CreateCategory([FromBody] SaveCalendarCategoryDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage 
                             ?? "Dữ liệu không hợp lệ.";
            return BadRequest(new { error = firstError });
        }
        try
        {
            var created = await calendarService.CreateCategoryAsync(CurrentUserId, request);
            return Ok(created);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("Categories/Update/{id}")]
    [HttpPost("Categories/Update/{id}")]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] SaveCalendarCategoryDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage 
                             ?? "Dữ liệu không hợp lệ.";
            return BadRequest(new { error = firstError });
        }
        try
        {
            await calendarService.UpdateCategoryAsync(CurrentUserId, id, request);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("Categories/Delete/{id}")]
    public async Task<IActionResult> DeleteCategory(long id)
    {
        try
        {
            await calendarService.DeleteCategoryAsync(CurrentUserId, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("Reports")]
    public async Task<IActionResult> Reports([FromQuery] string range = "this-week")
    {
        var reportsDto = await statisticsService.GetReportsAsync(CurrentUserId, range);
        return View(new CalendarReportsViewModel(reportsDto));
    }

    [HttpGet("GetReportsData")]
    public async Task<IActionResult> GetReportsData([FromQuery] string range = "this-week")
    {
        var reportsDto = await statisticsService.GetReportsAsync(CurrentUserId, range);
        return Json(reportsDto);
    }
}
