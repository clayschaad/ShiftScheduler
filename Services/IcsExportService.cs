using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using ShiftScheduler.Shared.Models;

namespace ShiftScheduler.Services
{
    public class IcsExportService
    {
        public string GenerateIcs(List<ShiftWithTransport> shiftsWithTransport)
        {
            var calendar = new Calendar();

            foreach (var shiftWithTransport in shiftsWithTransport)
            {
                var date = shiftWithTransport.Date;
                var shift = shiftWithTransport.Shift;

                if (string.IsNullOrEmpty(shift.MorningTime) && string.IsNullOrEmpty(shift.AfternoonTime))
                    continue;

                if (!string.IsNullOrEmpty(shift.MorningTime))
                {
                    var times = shift.MorningTime.Split('-');
                    var summary = $"{shift.Name} (Morning)";
                    var description = "";

                    if (shiftWithTransport.MorningTransport != null && !string.IsNullOrEmpty(shiftWithTransport.MorningTransport.DepartureTime))
                    {
                        var transportSummary = FormatTransportInfo(shiftWithTransport.MorningTransport, shiftWithTransport.DepartureStation);
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

                    if (shiftWithTransport.AfternoonTransport != null && !string.IsNullOrEmpty(shiftWithTransport.AfternoonTransport.DepartureTime))
                    {
                        var transportSummary = FormatTransportInfo(shiftWithTransport.AfternoonTransport, shiftWithTransport.DepartureStation);
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

        private string FormatTransportInfo(TransportConnection transport, string departureStation)
        {
            var departure = DateTime.TryParse(transport.DepartureTime, out var dep) ? dep.ToString("HH:mm") : transport.DepartureTime;
            var arrival = DateTime.TryParse(transport.ArrivalTime, out var arr) ? arr.ToString("HH:mm") : transport.ArrivalTime;
            
            var departureStationInfo = !string.IsNullOrEmpty(departureStation) ? $"{departureStation} " : "";
            
            var mainJourney = transport.Sections?.FirstOrDefault()?.Journey;
            if (mainJourney != null)
            {
                return $"{mainJourney.Category} {mainJourney.Number}: {departureStationInfo}{departure} → {arrival}";
            }
            
            return $"{departureStationInfo}{departure} → {arrival}";
        }
    }
}
