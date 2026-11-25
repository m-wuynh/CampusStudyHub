using StudyHub.BLL.DTOs;
using System;
using System.Collections.Generic;

namespace StudyHub.Web.ViewModels.Calendar;

public sealed record CalendarIndexViewModel(
    DateOnly SelectedDate,
    List<ScheduleEventDto> Events,
    WeeklyProgressDto? WeeklyProgress = null
);
