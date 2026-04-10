using ShiftScheduler.Shared;

namespace ShiftScheduler.Services;

public interface ICalendarService
{
    Task<List<CalendarInfo>> GetCalendarsAsync();
    Task SyncShiftsToCalendarAsync(string calendarId, List<ShiftWithTransport> shifts);
}

/// <summary>Marker interface for DI disambiguation.</summary>
public interface IGoogleCalendarService : ICalendarService { }

/// <summary>Marker interface for DI disambiguation.</summary>
public interface INextcloudCalendarService : ICalendarService { }
