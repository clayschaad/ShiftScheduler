using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.Services;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AllowedEmails")]
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
            
            var shifTimes = _configurationService.ParseShiftTimes(request.Date, shift);

            // Get transport for morning shift if it has morning time
            if (!string.IsNullOrEmpty(shift.MorningTime))
            {
                if (shifTimes.MorningStart.HasValue)
                {
                    morningTransport = await _transportService.GetConnectionAsync(shifTimes.MorningStart.Value);
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
                    if (shifTimes.MorningEnd.HasValue && shifTimes.AfternoonStart.HasValue)
                    {
                        var breakDurationMinutes = (shifTimes.AfternoonStart.Value - shifTimes.MorningEnd.Value).TotalMinutes;
                        shouldLoadAfternoonTransport = breakDurationMinutes >= transportConfig.MinBreakMinutes;
                    }
                }
                
                if (shouldLoadAfternoonTransport)
                {
                    if (shifTimes.AfternoonStart.HasValue)
                    {
                        afternoonTransport = await _transportService.GetConnectionAsync(shifTimes.AfternoonStart.Value);
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

        [HttpPost("save_schedule")]
        public async Task<IActionResult> SaveSchedule([FromBody] SaveScheduleRequest request)
        {
            try
            {
                await _configurationService.SaveScheduleAsync(request.Year, request.Month, request.Schedule);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving schedule: {ex.Message}");
            }
        }

        [HttpGet("load_schedule/{year}/{month}")]
        public async Task<IActionResult> LoadSchedule(int year, int month)
        {
            try
            {
                var schedule = await _configurationService.LoadScheduleAsync(year, month);
                return Ok(schedule);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading schedule: {ex.Message}");
            }
        }

        [HttpDelete("delete_schedule/{year}/{month}")]
        public async Task<IActionResult> DeleteSchedule(int year, int month)
        {
            try
            {
                await _configurationService.DeleteScheduleAsync(year, month);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting schedule: {ex.Message}");
            }
        }
    }

    public class ShiftTransportRequest
    {
        public string ShiftName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class SaveScheduleRequest
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public Dictionary<DateTime, string> Schedule { get; set; } = new();
    }
}
