using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.Services;
using ShiftScheduler.Shared.Models;

namespace ShiftScheduler.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShiftController : ControllerBase
    {
        private readonly ShiftService _shiftService;
        private readonly IcsExportService _icsService;
        private readonly PdfExportService _pdfExportService;
        private readonly TransportService _transportService;
        private readonly ShiftEnrichmentService _enrichmentService;
        private readonly TransportConfiguration _transportConfig;

        public ShiftController(
            ShiftService shiftService, 
            IcsExportService icsService, 
            PdfExportService pdfExportService, 
            TransportService transportService,
            ShiftEnrichmentService enrichmentService,
            TransportConfiguration transportConfig)
        {
            _shiftService = shiftService;
            _icsService = icsService;
            _pdfExportService = pdfExportService;
            _transportService = transportService;
            _enrichmentService = enrichmentService;
            _transportConfig = transportConfig;
        }

        [HttpGet("shifts")]
        public IActionResult GetShifts()
        {
            return Ok(_shiftService.GetShifts());
        }

        [HttpPost("transport_connection")]
        public async Task<IActionResult> GetTransportConnection([FromBody] TransportConnectionRequest request)
        {
            var connection = await _transportService.GetConnectionAsync(request.ArrivalTime, request.EndStation);
            return Ok(connection);
        }

        [HttpPost("shift_transport")]
        public async Task<IActionResult> GetShiftTransport([FromBody] ShiftTransportRequest request)
        {
            var shift = _shiftService.GetShifts().FirstOrDefault(s => s.Name == request.ShiftName);
            if (shift == null)
            {
                return NotFound($"Shift '{request.ShiftName}' not found");
            }

            TransportConnection? morningTransport = null;
            TransportConnection? afternoonTransport = null;

            // Get transport for morning shift if it has morning time
            if (!string.IsNullOrEmpty(shift.MorningTime))
            {
                var morningStartTime = ParseShiftTime(request.Date, shift.MorningTime);
                if (morningStartTime.HasValue)
                {
                    morningTransport = await _transportService.GetConnectionAsync(morningStartTime.Value);
                }
            }

            // Get transport for afternoon shift if it has afternoon time
            // But only if the break between morning and afternoon is long enough
            if (!string.IsNullOrEmpty(shift.AfternoonTime))
            {
                var shouldLoadAfternoonTransport = true;
                
                // If both morning and afternoon shifts exist, check break duration
                if (!string.IsNullOrEmpty(shift.MorningTime) && !string.IsNullOrEmpty(shift.AfternoonTime))
                {
                    var morningEndTime = ParseShiftEndTime(request.Date, shift.MorningTime);
                    var afternoonStartTime = ParseShiftTime(request.Date, shift.AfternoonTime);
                    
                    if (morningEndTime.HasValue && afternoonStartTime.HasValue)
                    {
                        var breakDurationMinutes = (afternoonStartTime.Value - morningEndTime.Value).TotalMinutes;
                        shouldLoadAfternoonTransport = breakDurationMinutes >= _transportConfig.MinBreakMinutes;
                    }
                }
                
                if (shouldLoadAfternoonTransport)
                {
                    var afternoonStartTime = ParseShiftTime(request.Date, shift.AfternoonTime);
                    if (afternoonStartTime.HasValue)
                    {
                        afternoonTransport = await _transportService.GetConnectionAsync(afternoonStartTime.Value);
                    }
                }
            }

            var shiftWithTransport = new ShiftWithTransport
            {
                Date = request.Date,
                Shift = shift,
                MorningTransport = morningTransport,
                AfternoonTransport = afternoonTransport,
                DepartureStation = _transportConfig.StartStation
            };

            return Ok(shiftWithTransport);
        }

        private static DateTime? ParseShiftTime(DateTime date, string timeRange)
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

        private static DateTime? ParseShiftEndTime(DateTime date, string timeRange)
        {
            try
            {
                var times = timeRange.Split('-');
                if (times.Length > 1 && TimeSpan.TryParse(times[1], out var endTime))
                {
                    return date.Add(endTime);
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return null;
        }

        [HttpPost("export_ics")]
        public IActionResult ExportIcsWithTransport([FromBody] List<ShiftWithTransport> shiftsWithTransport)
        {
            var ics = _icsService.GenerateIcs(shiftsWithTransport);
            return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar", "schedule.ics");
        }

        [HttpPost("export_pdf")]
        public IActionResult ExportPdfWithTransport([FromBody] List<ShiftWithTransport> shiftsWithTransport)
        {
            var pdf = _pdfExportService.GenerateMonthlySchedulePdf(shiftsWithTransport);
            return File(pdf, "application/pdf", "schedule.pdf");
        }
    }

    public class TransportConnectionRequest
    {
        public DateTime ArrivalTime { get; set; }
        public string? EndStation { get; set; }
    }

    public class ShiftTransportRequest
    {
        public string ShiftName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}
