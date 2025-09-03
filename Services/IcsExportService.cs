using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using ShiftScheduler.Shared;

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

                    if (shiftWithTransport.MorningTransport != null)
                    {
                        var transportSummary = FormatTransportInfo(shiftWithTransport.MorningTransport);
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

                    if (shiftWithTransport.AfternoonTransport != null)
                    {
                        var transportSummary = FormatTransportInfo(shiftWithTransport.AfternoonTransport);
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
            var departure =transport.DepartureTime.ToString("HH:mm");
            var arrival = transport.ArrivalTime.ToString("HH:mm");
            return $"{departure} → {arrival}";
        }
    }
}
