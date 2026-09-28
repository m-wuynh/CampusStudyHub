namespace StudyHub.BLL.DTOs;

public sealed record UserSettingDto(
    long UserId,
    string Theme,
    string LanguageCode,
    string TimeZoneId,
    bool EmailRemindersEnabled,
    bool InAppRemindersEnabled,
    short DefaultReminderMinutes,
    short DailyStudyGoalMinutes);
