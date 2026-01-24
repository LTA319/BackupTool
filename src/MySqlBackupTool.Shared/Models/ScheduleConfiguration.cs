using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Configuration for backup scheduling
/// </summary>
public class ScheduleConfiguration : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Backup configuration ID is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Backup configuration ID must be greater than 0")]
    public int BackupConfigId { get; set; }

    [Required(ErrorMessage = "Schedule type is required")]
    public ScheduleType ScheduleType { get; set; }

    [Required(ErrorMessage = "Schedule time is required")]
    [StringLength(50, ErrorMessage = "Schedule time must not exceed 50 characters")]
    public string ScheduleTime { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastExecuted { get; set; }

    public DateTime? NextExecution { get; set; }

    // Navigation property
    public BackupConfiguration? BackupConfiguration { get; set; }

    /// <summary>
    /// Validates the schedule configuration
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Validate schedule time format based on schedule type
        if (!string.IsNullOrWhiteSpace(ScheduleTime))
        {
            switch (ScheduleType)
            {
                case ScheduleType.Daily:
                    if (!TimeSpan.TryParse(ScheduleTime, out var dailyTime))
                    {
                        results.Add(new ValidationResult(
                            "Daily schedule time must be in HH:mm format (e.g., '14:30')",
                            new[] { nameof(ScheduleTime) }));
                    }
                    else if (dailyTime.Days > 0)
                    {
                        results.Add(new ValidationResult(
                            "Daily schedule time must be within 24 hours",
                            new[] { nameof(ScheduleTime) }));
                    }
                    break;

                case ScheduleType.Weekly:
                    var weeklyParts = ScheduleTime.Split(' ');
                    if (weeklyParts.Length != 2)
                    {
                        results.Add(new ValidationResult(
                            "Weekly schedule time must be in 'DayOfWeek HH:mm' format (e.g., 'Monday 14:30')",
                            new[] { nameof(ScheduleTime) }));
                    }
                    else
                    {
                        if (!Enum.TryParse<DayOfWeek>(weeklyParts[0], true, out _))
                        {
                            results.Add(new ValidationResult(
                                "Invalid day of week in weekly schedule",
                                new[] { nameof(ScheduleTime) }));
                        }
                        if (!TimeSpan.TryParse(weeklyParts[1], out var weeklyTime) || weeklyTime.Days > 0)
                        {
                            results.Add(new ValidationResult(
                                "Invalid time format in weekly schedule",
                                new[] { nameof(ScheduleTime) }));
                        }
                    }
                    break;

                case ScheduleType.Monthly:
                    var monthlyParts = ScheduleTime.Split(' ');
                    if (monthlyParts.Length != 2)
                    {
                        results.Add(new ValidationResult(
                            "Monthly schedule time must be in 'DD HH:mm' format (e.g., '15 14:30')",
                            new[] { nameof(ScheduleTime) }));
                    }
                    else
                    {
                        if (!int.TryParse(monthlyParts[0], out var day) || day < 1 || day > 31)
                        {
                            results.Add(new ValidationResult(
                                "Day of month must be between 1 and 31",
                                new[] { nameof(ScheduleTime) }));
                        }
                        if (!TimeSpan.TryParse(monthlyParts[1], out var monthlyTime) || monthlyTime.Days > 0)
                        {
                            results.Add(new ValidationResult(
                                "Invalid time format in monthly schedule",
                                new[] { nameof(ScheduleTime) }));
                        }
                    }
                    break;
            }
        }

        return results;
    }

    /// <summary>
    /// Calculates the next execution time based on the schedule configuration
    /// </summary>
    /// <returns>The next execution time, or null if the schedule is disabled</returns>
    public DateTime? CalculateNextExecution()
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(ScheduleTime))
            return null;

        var now = DateTime.Now;

        try
        {
            switch (ScheduleType)
            {
                case ScheduleType.Daily:
                    if (TimeSpan.TryParse(ScheduleTime, out var dailyTime))
                    {
                        var nextDaily = now.Date.Add(dailyTime);
                        if (nextDaily <= now)
                            nextDaily = nextDaily.AddDays(1);
                        return nextDaily;
                    }
                    break;

                case ScheduleType.Weekly:
                    var weeklyParts = ScheduleTime.Split(' ');
                    if (weeklyParts.Length == 2 && 
                        Enum.TryParse<DayOfWeek>(weeklyParts[0], true, out var targetDayOfWeek) &&
                        TimeSpan.TryParse(weeklyParts[1], out var weeklyTime))
                    {
                        var daysUntilTarget = ((int)targetDayOfWeek - (int)now.DayOfWeek + 7) % 7;
                        if (daysUntilTarget == 0)
                        {
                            var todayAtTime = now.Date.Add(weeklyTime);
                            if (todayAtTime <= now)
                                daysUntilTarget = 7;
                        }
                        return now.Date.AddDays(daysUntilTarget).Add(weeklyTime);
                    }
                    break;

                case ScheduleType.Monthly:
                    var monthlyParts = ScheduleTime.Split(' ');
                    if (monthlyParts.Length == 2 && 
                        int.TryParse(monthlyParts[0], out var targetDayOfMonth) &&
                        TimeSpan.TryParse(monthlyParts[1], out var monthlyTime))
                    {
                        var nextMonthly = new DateTime(now.Year, now.Month, Math.Min(targetDayOfMonth, DateTime.DaysInMonth(now.Year, now.Month))).Add(monthlyTime);
                        if (nextMonthly <= now)
                        {
                            var nextMonth = now.AddMonths(1);
                            nextMonthly = new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(targetDayOfMonth, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month))).Add(monthlyTime);
                        }
                        return nextMonthly;
                    }
                    break;
            }
        }
        catch (Exception)
        {
            // Return null if calculation fails
            return null;
        }

        return null;
    }
}

/// <summary>
/// Types of backup schedules
/// </summary>
public enum ScheduleType
{
    Daily,
    Weekly,
    Monthly
}