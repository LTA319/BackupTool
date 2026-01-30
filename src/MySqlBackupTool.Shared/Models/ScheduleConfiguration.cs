using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 备份调度配置
/// 定义备份任务的执行时间和频率
/// </summary>
public class ScheduleConfiguration : IValidatableObject
{
    /// <summary>
    /// 调度配置的唯一标识符
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 关联的备份配置ID，必填项
    /// 必须大于0
    /// </summary>
    [Required(ErrorMessage = "Backup configuration ID is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Backup configuration ID must be greater than 0")]
    public int BackupConfigId { get; set; }

    /// <summary>
    /// 调度类型，必填项
    /// 支持每日、每周、每月调度
    /// </summary>
    [Required(ErrorMessage = "Schedule type is required")]
    public ScheduleType ScheduleType { get; set; }

    /// <summary>
    /// 调度时间，必填项
    /// 格式根据调度类型而定：
    /// - 每日：HH:mm（如 "14:30"）
    /// - 每周：DayOfWeek HH:mm（如 "Monday 14:30"）
    /// - 每月：DD HH:mm（如 "15 14:30"）
    /// </summary>
    [Required(ErrorMessage = "Schedule time is required")]
    [StringLength(50, ErrorMessage = "Schedule time must not exceed 50 characters")]
    public string ScheduleTime { get; set; } = string.Empty;

    /// <summary>
    /// 调度是否启用
    /// 禁用的调度不会执行
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 调度创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后执行时间
    /// </summary>
    public DateTime? LastExecuted { get; set; }

    /// <summary>
    /// 下次执行时间
    /// </summary>
    public DateTime? NextExecution { get; set; }

    // 导航属性
    
    /// <summary>
    /// 关联的备份配置
    /// </summary>
    public BackupConfiguration? BackupConfiguration { get; set; }

    /// <summary>
    /// 验证调度配置
    /// 根据调度类型验证时间格式的正确性
    /// </summary>
    /// <param name="validationContext">验证上下文</param>
    /// <returns>验证结果集合</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // 根据调度类型验证调度时间格式
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
    /// 根据调度配置计算下次执行时间
    /// </summary>
    /// <returns>下次执行时间，如果调度被禁用则返回null</returns>
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
            // 如果计算失败则返回null
            return null;
        }

        return null;
    }
}

/// <summary>
/// 备份调度的类型
/// </summary>
public enum ScheduleType
{
    /// <summary>
    /// 每日调度
    /// </summary>
    Daily,
    
    /// <summary>
    /// 每周调度
    /// </summary>
    Weekly,
    
    /// <summary>
    /// 每月调度
    /// </summary>
    Monthly
}