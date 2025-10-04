using ShiftScheduler.Shared;
using Shouldly;

namespace ShiftScheduler.Services.Tests;

public class ConfigurationServiceTests
{
    [Fact] 
    public void ParseShiftTimes_WithMorningAndAfternoon_ShouldReturnValid()
    {
        var applicationConfig = new ApplicationConfiguration();
        var configurationService = new ConfigurationService(applicationConfig);
        var shift = new Shift
        {
            MorningTime = "06:00-12:30",
            AfternoonTime = "13:00-17:30"
        };
        
        var shiftTimes = configurationService.ParseShiftTimes(DateTime.Parse("2025-07-13T13:00:00"), shift);
        shiftTimes.MorningStart.ShouldBe(T("2025-07-13T06:00:00+02:00"));
        shiftTimes.MorningEnd.ShouldBe(T("2025-07-13T12:30:00+02:00"));
        shiftTimes.AfternoonStart.ShouldBe(T("2025-07-13T13:00:00+02:00"));
        shiftTimes.AfternoonEnd.ShouldBe(T("2025-07-13T17:30:00+02:00"));
    }
    
    [Fact] 
    public void ParseShiftTimes_WithMorning_ShouldReturnValid()
    {
        var applicationConfig = new ApplicationConfiguration();
        var configurationService = new ConfigurationService(applicationConfig);
        var shift = new Shift
        {
            MorningTime = "06:00-12:30"
        };
        
        var shiftTimes = configurationService.ParseShiftTimes(DateTime.Parse("2025-07-13T13:00:00"), shift);
        shiftTimes.MorningStart.ShouldBe(T("2025-07-13T06:00:00+02:00"));
        shiftTimes.MorningEnd.ShouldBe(T("2025-07-13T12:30:00+02:00"));
        shiftTimes.AfternoonStart.ShouldBeNull();
        shiftTimes.AfternoonEnd.ShouldBeNull();
    }
    
    [Fact] 
    public void ParseShiftTimes_WithWinterDate_ShouldUseCorrectOffset()
    {
        var applicationConfig = new ApplicationConfiguration();
        var configurationService = new ConfigurationService(applicationConfig);
        var shift = new Shift
        {
            MorningTime = "06:00-12:30",
            AfternoonTime = "13:00-17:30"
        };
        
        var shiftTimes = configurationService.ParseShiftTimes(DateTime.Parse("2025-11-15T13:00:00"), shift);
        shiftTimes.MorningStart.ShouldBe(T("2025-11-15T06:00:00+01:00"));
        shiftTimes.MorningEnd.ShouldBe(T("2025-11-15T12:30:00+01:00"));
        shiftTimes.AfternoonStart.ShouldBe(T("2025-11-15T13:00:00+01:00"));
        shiftTimes.AfternoonEnd.ShouldBe(T("2025-11-15T17:30:00+01:00"));
    }
    
    [Fact] 
    public void GetZurichTime_WithSummerDate_ShouldUseCorrectOffset()
    {
        var applicationConfig = new ApplicationConfiguration();
        var configurationService = new ConfigurationService(applicationConfig);
        var date = DateOnly.Parse("2025-07-13");
        var timeRange = "06:00-12:30";
        
        var (startTime, endTime) = configurationService.GetZurichTime(date, timeRange);
        startTime.ShouldBe(T("2025-07-13T06:00:00+02:00"));
        endTime.ShouldBe(T("2025-07-13T12:30:00+02:00"));
    }
    
    [Fact] 
    public void GetZurichTime_WithWinterDate_ShouldUseCorrectOffset()
    {
        var applicationConfig = new ApplicationConfiguration();
        var configurationService = new ConfigurationService(applicationConfig);
        var date = DateOnly.Parse("2025-11-15");
        var timeRange = "06:00-12:30";
        
        var (startTime, endTime) = configurationService.GetZurichTime(date, timeRange);
        startTime.ShouldBe(T("2025-11-15T06:00:00+01:00"));
        endTime.ShouldBe(T("2025-11-15T12:30:00+01:00"));
    }
    
    private DateTimeOffset T(string dateTimeString)
    {
        return DateTimeOffset.Parse(dateTimeString);
    }
}