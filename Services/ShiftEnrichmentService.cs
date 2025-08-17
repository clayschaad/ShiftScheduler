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

        public async Task<List<Shift>> EnrichShiftsWithTransportAsync(List<Shift> shifts, Dictionary<DateTime, string> schedule)
        {
            var enrichedShifts = new List<Shift>();

            foreach (var shift in shifts)
            {
                var enrichedShift = new Shift
                {
                    Name = shift.Name,
                    Icon = shift.Icon,
                    MorningTime = shift.MorningTime,
                    AfternoonTime = shift.AfternoonTime
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
                            enrichedShift.MorningTransport = await _transportService.GetConnectionAsync(morningStartTime.Value);
                        }
                    }

                    // Get transport for afternoon shift
                    if (!string.IsNullOrEmpty(shift.AfternoonTime))
                    {
                        var afternoonStartTime = ParseShiftTime(sampleDate, shift.AfternoonTime);
                        if (afternoonStartTime.HasValue)
                        {
                            enrichedShift.AfternoonTransport = await _transportService.GetConnectionAsync(afternoonStartTime.Value);
                        }
                    }
                }

                enrichedShifts.Add(enrichedShift);
            }

            return enrichedShifts;
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