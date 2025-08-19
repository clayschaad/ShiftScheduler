using ShiftScheduler.Shared.Models;

namespace ShiftScheduler.Services
{
    public class ShiftEnrichmentService
    {
        private readonly TransportService _transportService;

        public ShiftEnrichmentService(TransportService transportService)
        {
            _transportService = transportService;
        }

        public async Task<List<ShiftTransport>> EnrichShiftsWithTransportAsync(List<Shift> shifts, Dictionary<DateTime, string> schedule)
        {
            var shiftTransports = new List<ShiftTransport>();

            foreach (var shift in shifts)
            {
                var shiftTransport = new ShiftTransport
                {
                    ShiftName = shift.Name
                };

                // Find dates where this shift is scheduled
                var scheduledDates = schedule.Where(kvp => kvp.Value == shift.Name).Select(kvp => kvp.Key).ToList();
                
                if (scheduledDates.Count > 0)
                {
                    // Use the first scheduled date to calculate transport times
                    var sampleDate = scheduledDates.First();

                    // Get transport for morning shift
                    if (!string.IsNullOrEmpty(shift.MorningTime))
                    {
                        var morningStartTime = ParseShiftTime(sampleDate, shift.MorningTime);
                        if (morningStartTime.HasValue)
                        {
                            shiftTransport.MorningTransport = await _transportService.GetConnectionAsync(morningStartTime.Value);
                        }
                    }

                    // Get transport for afternoon shift
                    if (!string.IsNullOrEmpty(shift.AfternoonTime))
                    {
                        var afternoonStartTime = ParseShiftTime(sampleDate, shift.AfternoonTime);
                        if (afternoonStartTime.HasValue)
                        {
                            shiftTransport.AfternoonTransport = await _transportService.GetConnectionAsync(afternoonStartTime.Value);
                        }
                    }
                }

                shiftTransports.Add(shiftTransport);
            }

            return shiftTransports;
        }

        private DateTime? ParseShiftTime(DateTime date, string timeRange)
        {
            try
            {
                var times = timeRange.Split('-');
                if (times.Length > 0 && TimeSpan.TryParse(times[0], out var startTime))
                {
                    return date.Add(startTime);
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return null;
        }
    }
}