using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using ShiftScheduler.Shared.Models;

namespace ShiftScheduler.Services
{
    public class IcsExportService
    {
        public string GenerateIcs(Dictionary<DateTime, string> schedule, List<Shift> shifts)
        {
            var calendar = new Calendar();

            foreach (var kvp in schedule)
            {
                var date = kvp.Key;
                var shiftName = kvp.Value;
                var shift = shifts.FirstOrDefault(s => s.Name == shiftName);

                if (shift == null || (string.IsNullOrEmpty(shift.MorningTime) && string.IsNullOrEmpty(shift.AfternoonTime)))
                    continue;

                if (!string.IsNullOrEmpty(shift.MorningTime))
                {
                    var times = shift.MorningTime.Split('-');
                    calendar.Events.Add(new CalendarEvent
                    {
                        Summary = $"{shift.Name} (Morning)",
                        Start = new CalDateTime(date.Add(TimeSpan.Parse(times[0]))),
                        End = new CalDateTime(date.Add(TimeSpan.Parse(times[1])))
                    });
                }

                if (!string.IsNullOrEmpty(shift.AfternoonTime))
                {
                    var times = shift.AfternoonTime.Split('-');
                    calendar.Events.Add(new CalendarEvent
                    {
                        Summary = $"{shift.Name} (Afternoon)",
                        Start = new CalDateTime(date.Add(TimeSpan.Parse(times[0]))),
                        End = new CalDateTime(date.Add(TimeSpan.Parse(times[1])))
                    });
                }
            }

            return new CalendarSerializer().SerializeToString(calendar);
        }
    }
}
