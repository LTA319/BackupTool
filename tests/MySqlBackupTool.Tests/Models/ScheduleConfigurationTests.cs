using MySqlBackupTool.Shared.Models;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace MySqlBackupTool.Tests.Models;

public class ScheduleConfigurationTests
{
    [Fact]
    public void CalculateNextExecution_ShouldReturnNull_WhenDisabled()
    {
        // Arrange
        var schedule = new ScheduleConfiguration
        {
            ScheduleType = ScheduleType.Daily,
            ScheduleTime = "14:30",
            IsEnabled = false
        };

        // Act
        var result = schedule.CalculateNextExecution();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CalculateNextExecution_ShouldReturnNull_WhenScheduleTimeIsEmpty()
    {
        // Arrange
        var schedule = new ScheduleConfiguration
        {
            ScheduleType = ScheduleType.Daily,
            ScheduleTime = "",
            IsEnabled = true
        };

        // Act
        var result = schedule.CalculateNextExecution();

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("14:30")]
    [InlineData("09:15")]
    [InlineData("23:59")]
    public void CalculateNextExecution_ShouldCalculateCorrectly_ForDailySchedule(string scheduleTime)
    {
        // Arrange
        var schedule = new ScheduleConfiguration
        {
            ScheduleType = ScheduleType.Daily,
            ScheduleTime = scheduleTime,
            IsEnabled = true
        };

        var expectedTime = TimeSpan.Parse(scheduleTime);
        var now = DateTime.Now;
        var expectedDate = now.Date.Add(expectedTime);
        if (expectedDate <= now)
            expectedDate = expectedDate.AddDays(1);

        // Act
        var result = schedule.CalculateNextExecution();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedDate.TimeOfDay, result.Value.TimeOfDay);
        Assert.True(result.Value > now);
    }

    [Theory]
    [InlineData("Monday 14:30")]
    [InlineData("Friday 09:15")]
    [InlineData("Sunday 23:59")]
    public void CalculateNextExecution_ShouldCalculateCorrectly_ForWeeklySchedule(string scheduleTime)
    {
        // Arrange
        var schedule = new ScheduleConfiguration
        {
            ScheduleType = ScheduleType.Weekly,
            ScheduleTime = scheduleTime,
            IsEnabled = true
        };

        var parts = scheduleTime.Split(' ');
        var targetDay = Enum.Parse<DayOfWeek>(parts[0]);
        var time = TimeSpan.Parse(parts[1]);

        // Act
        var result = schedule.CalculateNextExecution();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(targetDay, result.Value.DayOfWeek);
        Assert.Equal(time, result.Value.TimeOfDay);
        Assert.True(result.Value > DateTime.Now);
    }

    [Theory]
    [InlineData("15 14:30")]
    [InlineData("1 09:15")]
    [InlineData("31 23:59")]
    public void CalculateNextExecution_ShouldCalculateCorrectly_ForMonthlySchedule(string scheduleTime)
    {
        // Arrange
        var schedule = new ScheduleConfiguration
        {
            ScheduleType = ScheduleType.Monthly,
            ScheduleTime = scheduleTime,
            IsEnabled = true
        };

        var parts = scheduleTime.Split(' ');
        var targetDay = int.Parse(parts[0]);
        var time = TimeSpan.Parse(parts[1]);

        // Act
        var result = schedule.CalculateNextExecution();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(time, result.Value.TimeOfDay);
        Assert.True(result.Value > DateTime.Now);
        
        // Check that the day is correct (accounting for months with fewer days)
        var expectedDay = Math.Min(targetDay, DateTime.DaysInMonth(result.Value.Year, result.Value.Month));
        Assert.Equal(expectedDay, result.Value.Day);
    }

    [Fact]
    public void CalculateNextExecution_ShouldHandleEndOfMonth_ForMonthlySchedule()
    {
        // Arrange - Schedule for 31st of every month
        var schedule = new ScheduleConfiguration
        {
            ScheduleType = ScheduleType.Monthly,
            ScheduleTime = "31 14:30",
            IsEnabled = true
        };

        // Act
        var result = schedule.CalculateNextExecution();

        // Assert
        Assert.NotNull(result);
        
        // Should adjust to the last day of the month if the month has fewer than 31 days
        var expectedDay = Math.Min(31, DateTime.DaysInMonth(result.Value.Year, result.Value.Month));
        Assert.Equal(expectedDay, result.Value.Day);
    }

    [Theory]
    [InlineData(ScheduleType.Daily, "")]
    [InlineData(ScheduleType.Daily, "invalid")]
    [InlineData(ScheduleType.Weekly, "")]
    [InlineData(ScheduleType.Weekly, "InvalidDay 14:30")]
    [InlineData(ScheduleType.Weekly, "Monday invalid")]
    [InlineData(ScheduleType.Monthly, "")]
    [InlineData(ScheduleType.Monthly, "32 14:30")]
    [InlineData(ScheduleType.Monthly, "15 invalid")]
    public void Validate_ShouldReturnErrors_ForInvalidScheduleTime(ScheduleType scheduleType, string scheduleTime)
    {
        // Arrange
        var schedule = new ScheduleConfiguration
        {
            BackupConfigId = 1,
            ScheduleType = scheduleType,
            ScheduleTime = scheduleTime,
            IsEnabled = true
        };

        var validationContext = new ValidationContext(schedule);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(schedule, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
    }

    [Theory]
    [InlineData(ScheduleType.Daily, "14:30")]
    [InlineData(ScheduleType.Weekly, "Monday 14:30")]
    [InlineData(ScheduleType.Monthly, "15 14:30")]
    public void Validate_ShouldReturnNoErrors_ForValidScheduleTime(ScheduleType scheduleType, string scheduleTime)
    {
        // Arrange
        var schedule = new ScheduleConfiguration
        {
            BackupConfigId = 1,
            ScheduleType = scheduleType,
            ScheduleTime = scheduleTime,
            IsEnabled = true
        };

        var validationContext = new ValidationContext(schedule);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(schedule, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void Validate_ShouldRequireBackupConfigId()
    {
        // Arrange
        var schedule = new ScheduleConfiguration
        {
            BackupConfigId = 0, // Invalid
            ScheduleType = ScheduleType.Daily,
            ScheduleTime = "14:30",
            IsEnabled = true
        };

        var validationContext = new ValidationContext(schedule);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(schedule, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage!.Contains("Backup configuration ID must be greater than 0"));
    }

    [Fact]
    public void Validate_ShouldRequireScheduleTime()
    {
        // Arrange
        var schedule = new ScheduleConfiguration
        {
            BackupConfigId = 1,
            ScheduleType = ScheduleType.Daily,
            ScheduleTime = "", // Invalid
            IsEnabled = true
        };

        var validationContext = new ValidationContext(schedule);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(schedule, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage!.Contains("Schedule time is required"));
    }

    [Fact]
    public void DefaultValues_ShouldBeSetCorrectly()
    {
        // Arrange & Act
        var schedule = new ScheduleConfiguration();

        // Assert
        Assert.True(schedule.IsEnabled);
        Assert.True(schedule.CreatedAt > DateTime.MinValue);
        Assert.Equal(string.Empty, schedule.ScheduleTime);
        Assert.Null(schedule.LastExecuted);
        Assert.Null(schedule.NextExecution);
    }
}