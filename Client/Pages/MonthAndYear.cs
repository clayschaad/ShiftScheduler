namespace ShiftScheduler.Client.Pages;

public record MonthAndYear(int Month, int Year)
{
    public static MonthAndYear Current() => new MonthAndYear(DateTime.Now.Month, DateTime.Now.Year);
    public static MonthAndYear Next()
    {
        var today = DateTime.Today;
        var nextMonth = today.Month == 12 ? 1 : today.Month + 1;
        var year = today.Month == 12 ? today.Year + 1 : today.Year;
        return new MonthAndYear(nextMonth, year);
    }

    public string Title => new DateTime(Year, Month, 1).ToString("MMMM yyyy");

    public IReadOnlyList<DateTime> DaysInMonth()
    {
        return Enumerable.Range(1, DateTime.DaysInMonth(Year, Month))
            .Select(d => new DateTime(Year, Month, d))
            .ToList();
    }
}