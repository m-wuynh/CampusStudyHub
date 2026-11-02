using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Grades;

public interface IGoalService
{
    Task CreateGoalAsync(long userId, CreateGoalDto dto, CancellationToken cancellationToken = default);
    Task UpdateGoalAsync(long userId, UpdateGoalDto dto, CancellationToken cancellationToken = default);
    Task DeleteGoalAsync(long userId, long goalId, CancellationToken cancellationToken = default);
    Task ReorderGoalsAsync(long userId, List<ReorderGoalDto> items, CancellationToken cancellationToken = default);

    // Goal Activities (2-way sync with ScheduleEvents)
    Task<ScheduleEventDto> AddGoalActivityAsync(long userId, CreateGoalActivityDto dto, CancellationToken cancellationToken = default);
    Task UpdateGoalActivityAsync(long userId, UpdateGoalActivityDto dto, CancellationToken cancellationToken = default);
    Task DeleteGoalActivityAsync(long userId, long activityId, CancellationToken cancellationToken = default);
    Task<bool> ToggleGoalActivityCompletionAsync(long userId, long activityId, CancellationToken cancellationToken = default);
}

