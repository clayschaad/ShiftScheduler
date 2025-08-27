using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.Services;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize(Policy = "AllowedEmails")] // Temporarily disabled for testing
    public class ShiftController : ControllerBase
    {
        private readonly IcsExportService _icsService;
        private readonly PdfExportService _pdfExportService;
        private readonly ITransportService _transportService;
        private readonly IConfigurationService _configurationService;

        public ShiftController(
            IcsExportService icsService, 
            PdfExportService pdfExportService, 
            ITransportService transportService,
            IConfigurationService configurationService)
        {
            _icsService = icsService;
            _pdfExportService = pdfExportService;
            _transportService = transportService;
            _configurationService = configurationService;
        }

        [HttpGet("shifts")]
        public IActionResult GetShifts()
        {
            return Ok(_configurationService.GetShifts());
        }

        [HttpPost("shift_transport")]
        public async Task<IActionResult> GetShiftTransport([FromBody] ShiftTransportRequest request)
        {
            var shift = _configurationService.GetShifts().FirstOrDefault(s => s.Name == request.ShiftName);
            if (shift == null)
            {
                return NotFound($"Shift '{request.ShiftName}' not found");
            }

            var transportConfig = _configurationService.GetTransportConfiguration();
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
                        shouldLoadAfternoonTransport = breakDurationMinutes >= transportConfig.MinBreakMinutes;
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

    public class ShiftTransportRequest
    {
        public string ShiftName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}
