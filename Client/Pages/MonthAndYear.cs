using System.Globalization;

namespace ShiftScheduler.Client.Pages;

public record MonthAndYear(int Month, int Year)
{
    public static MonthAndYear Current() => new (DateTime.Now.Month, DateTime.Now.Year);
    
    public static MonthAndYear Next()
    {
        var today = DateTime.Today;
        var nextMonth = today.Month == 12 ? 1 : today.Month + 1;
        var year = today.Month == 12 ? today.Year + 1 : today.Year;
        return new MonthAndYear(nextMonth, year);
    }
    
    public static string CurrentMonthName => Current().ToDateTime.ToString("MMMM");
    public static string NextMonthName => Next().ToDateTime.ToString("MMMM");

    public string Title => ToDateTime.ToString("MMMM yyyy");

    private DateTime ToDateTime => new DateTime(Year, Month, 1);
    
    public IReadOnlyList<DateTime> DaysInMonth()
    {
        return Enumerable.Range(1, DateTime.DaysInMonth(Year, Month))
            .Select(d => new DateTime(Year, Month, d))
            .ToList();
    }
    
    public IReadOnlyDictionary<int, List<DateTime>> GetWeeksInMonth()
    {
        var days = DaysInMonth();
        var weeks = days
            .GroupBy(d => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(d, CalendarWeekRule.FirstDay, DayOfWeek.Monday))
            .ToDictionary(d => d.Key, d => d.ToList());
        return weeks;
    }
}