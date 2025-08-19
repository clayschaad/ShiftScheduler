using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using ShiftScheduler.Shared.Models;

namespace ShiftScheduler.Services
{
    public class IcsExportService
    {
        public string GenerateIcs(Dictionary<DateTime, string> schedule, List<Shift> shifts, List<ShiftTransport>? shiftTransports = null)
        {
            var calendar = new Calendar();

            foreach (var kvp in schedule)
            {
                var date = kvp.Key;
                var shiftName = kvp.Value;
                var shift = shifts.FirstOrDefault(s => s.Name == shiftName);
                var transport = shiftTransports?.FirstOrDefault(t => t.ShiftName == shiftName);

                if (shift == null || (string.IsNullOrEmpty(shift.MorningTime) && string.IsNullOrEmpty(shift.AfternoonTime)))
                    continue;

                if (!string.IsNullOrEmpty(shift.MorningTime))
                {
                    var times = shift.MorningTime.Split('-');
                    var summary = $"{shift.Name} (Morning)";
                    var description = "";

                    if (transport?.MorningTransport != null && !string.IsNullOrEmpty(transport.MorningTransport.DepartureTime))
                    {
                        var transportSummary = FormatTransportInfo(transport.MorningTransport);
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

                    if (transport?.AfternoonTransport != null && !string.IsNullOrEmpty(transport.AfternoonTransport.DepartureTime))
                    {
                        var transportSummary = FormatTransportInfo(transport.AfternoonTransport);
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
