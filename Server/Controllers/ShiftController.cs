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
        private readonly IGoogleCalendarService _googleCalendarService;
        private readonly INextcloudCalendarService _nextcloudCalendarService;

        public ShiftController(
            IcsExportService icsService,
            PdfExportService pdfExportService,
            ITransportService transportService,
            IConfigurationService configurationService,
            IGoogleCalendarService googleCalendarService,
            INextcloudCalendarService nextcloudCalendarService)
        {
            _icsService = icsService;
            _pdfExportService = pdfExportService;
            _transportService = transportService;
            _configurationService = configurationService;
            _googleCalendarService = googleCalendarService;
            _nextcloudCalendarService = nextcloudCalendarService;
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
                return NotFound($"Shift '{request.ShiftName}' not found");

            var transportConfig = _configurationService.GetTransportConfiguration();
            TransportConnection? morningTransport = null;
            TransportConnection? afternoonTransport = null;

            var shifTimes = _configurationService.ParseShiftTimes(request.Date, shift);

            if (!string.IsNullOrEmpty(shift.MorningTime) && shifTimes.MorningStart.HasValue)
                morningTransport = await _transportService.GetConnectionAsync(shifTimes.MorningStart.Value);

            if (!string.IsNullOrEmpty(shift.AfternoonTime))
            {
                var shouldLoad = true;

                if (!string.IsNullOrEmpty(shift.MorningTime) && shifTimes.MorningEnd.HasValue && shifTimes.AfternoonStart.HasValue)
                {
                    var breakMinutes = (shifTimes.AfternoonStart.Value - shifTimes.MorningEnd.Value).TotalMinutes;
                    shouldLoad = breakMinutes >= transportConfig.MinBreakMinutes;
                }

                if (shouldLoad && shifTimes.AfternoonStart.HasValue)
                    afternoonTransport = await _transportService.GetConnectionAsync(shifTimes.AfternoonStart.Value);
            }

            return Ok(new ShiftWithTransport
            {
                Date = request.Date,
                Shift = shift,
                MorningTransport = morningTransport,
                AfternoonTransport = afternoonTransport
            });
        }

        [HttpPost("export_ics")]
        public async Task<IActionResult> ExportIcs([FromBody] ExportRequest request)
        {
            var shiftsWithTransport = await BuildShiftsWithTransportAsync(request.Year, request.Month);
            var ics = _icsService.GenerateIcs(shiftsWithTransport);
            return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar", "schedule.ics");
        }

        [HttpPost("export_pdf")]
        public async Task<IActionResult> ExportPdf([FromBody] ExportRequest request)
        {
            var shiftsWithTransport = await BuildShiftsWithTransportAsync(request.Year, request.Month);
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

        [HttpGet("calendars")]
        public async Task<IActionResult> GetCalendars([FromQuery] string provider)
        {
            try
            {
                ICalendarService service = provider switch
                {
                    "google"    => (ICalendarService)_googleCalendarService,
                    "nextcloud" => _nextcloudCalendarService,
                    _           => null!
                };
                if (service is null)
                    return BadRequest($"Unknown provider '{provider}'. Supported values: google, nextcloud.");

                var calendars = await service.GetCalendarsAsync();
                return Ok(calendars);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving calendars: {ex.Message}");
            }
        }

        [HttpPost("sync_to_calendar")]
        public async Task<IActionResult> SyncToCalendar([FromBody] SyncToCalendarRequest request)
        {
            try
            {
                var shiftsWithTransport = await BuildShiftsWithTransportAsync(request.Year, request.Month);

                ICalendarService service = request.Provider switch
                {
                    "google"    => (ICalendarService)_googleCalendarService,
                    "nextcloud" => _nextcloudCalendarService,
                    _           => null!
                };
                if (service is null)
                    return BadRequest($"Unknown provider '{request.Provider}'. Supported values: google, nextcloud.");

                await service.SyncShiftsToCalendarAsync(request.CalendarId, shiftsWithTransport);
                return Ok(new { message = "Shifts synced successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error syncing to calendar: {ex.Message}");
            }
        }

        private async Task<List<ShiftWithTransport>> BuildShiftsWithTransportAsync(int year, int month)
        {
            var schedule = await _configurationService.LoadScheduleAsync(year, month);
            var shiftsWithTransport = new List<ShiftWithTransport>();
            var transportConfig = _configurationService.GetTransportConfiguration();

            foreach (var kvp in schedule)
            {
                var date = kvp.Key;
                var shiftName = kvp.Value;

                var shift = _configurationService.GetShifts().FirstOrDefault(s => s.Name == shiftName);
                if (shift == null) continue;

                var shiftTimes = _configurationService.ParseShiftTimes(date, shift);
                TransportConnection? morningTransport = null;
                TransportConnection? afternoonTransport = null;

                if (!string.IsNullOrEmpty(shift.MorningTime) && shiftTimes.MorningStart.HasValue)
                    morningTransport = await _transportService.GetConnectionAsync(shiftTimes.MorningStart.Value);

                if (!string.IsNullOrEmpty(shift.AfternoonTime))
                {
                    var shouldLoad = true;

                    if (!string.IsNullOrEmpty(shift.MorningTime) && shiftTimes.MorningEnd.HasValue && shiftTimes.AfternoonStart.HasValue)
                    {
                        var breakMinutes = (shiftTimes.AfternoonStart.Value - shiftTimes.MorningEnd.Value).TotalMinutes;
                        shouldLoad = breakMinutes >= transportConfig.MinBreakMinutes;
                    }

                    if (shouldLoad && shiftTimes.AfternoonStart.HasValue)
                        afternoonTransport = await _transportService.GetConnectionAsync(shiftTimes.AfternoonStart.Value);
                }

                shiftsWithTransport.Add(new ShiftWithTransport
                {
                    Date = date,
                    Shift = shift,
                    MorningTransport = morningTransport,
                    AfternoonTransport = afternoonTransport
                });
            }

            return shiftsWithTransport;
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

    public class ExportRequest
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class SyncToCalendarRequest
    {
        public string Provider { get; set; } = string.Empty;
        public string CalendarId { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
    }
}
