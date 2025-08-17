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
                    var summary = $"{shift.Name} (Morning)";
                    var description = "";

                    if (shift.MorningTransport != null && !string.IsNullOrEmpty(shift.MorningTransport.DepartureTime))
                    {
                        var transportSummary = FormatTransportInfo(shift.MorningTransport);
                        description = $"Transport: {transportSummary}";
                    }

                    calendar.Events.Add(new CalendarEvent
                    {
                        Summary = summary,
                        Description = description,
                        Start = new CalDateTime(date.Add(TimeSpan.Parse(times[0]))),
                        End = new CalDateTime(date.Add(TimeSpan.Parse(times[1])))
                    });
                }

                if (!string.IsNullOrEmpty(shift.AfternoonTime))
                {
                    var times = shift.AfternoonTime.Split('-');
                    var summary = $"{shift.Name} (Afternoon)";
                    var description = "";

                    if (shift.AfternoonTransport != null && !string.IsNullOrEmpty(shift.AfternoonTransport.DepartureTime))
                    {
                        var transportSummary = FormatTransportInfo(shift.AfternoonTransport);
                        description = $"Transport: {transportSummary}";
                    }

                    calendar.Events.Add(new CalendarEvent
                    {
                        Summary = summary,
                        Description = description,
                        Start = new CalDateTime(date.Add(TimeSpan.Parse(times[0]))),
                        End = new CalDateTime(date.Add(TimeSpan.Parse(times[1])))
                    });
                }
            }

            return new CalendarSerializer().SerializeToString(calendar);
        }

        private string FormatTransportInfo(TransportConnection transport)
        {
            var departure = DateTime.TryParse(transport.DepartureTime, out var dep) ? dep.ToString("HH:mm") : transport.DepartureTime;
            var arrival = DateTime.TryParse(transport.ArrivalTime, out var arr) ? arr.ToString("HH:mm") : transport.ArrivalTime;
            
            var mainJourney = transport.Sections?.FirstOrDefault()?.Journey;
            if (mainJourney != null)
            {
                return $"{mainJourney.Category} {mainJourney.Number}: {departure} → {arrival}";
            }
            
            return $"{departure} → {arrival}";
        }
    }
}
