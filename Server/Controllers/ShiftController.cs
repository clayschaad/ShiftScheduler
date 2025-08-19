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

        public ShiftController(
            ShiftService shiftService, 
            IcsExportService icsService, 
            PdfExportService pdfExportService, 
            TransportService transportService,
            ShiftEnrichmentService enrichmentService)
        {
            _shiftService = shiftService;
            _icsService = icsService;
            _pdfExportService = pdfExportService;
            _transportService = transportService;
            _enrichmentService = enrichmentService;
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

        [HttpPost("export_ics")]
        public async Task<IActionResult> ExportIcs([FromBody] Dictionary<DateTime, string> schedule)
        {
            var shifts = _shiftService.GetShifts();
            var shiftTransports = await _enrichmentService.EnrichShiftsWithTransportAsync(shifts, schedule);
            var ics = _icsService.GenerateIcs(schedule, shifts, shiftTransports);
            return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar", "schedule.ics");
        }

         [HttpPost("export_pdf")]
        public async Task<IActionResult> ExportPdf([FromBody] Dictionary<DateTime, string> schedule)
        {
            var shifts = _shiftService.GetShifts();
            var shiftTransports = await _enrichmentService.EnrichShiftsWithTransportAsync(shifts, schedule);
            var pdf = _pdfExportService.GenerateMonthlySchedulePdf(schedule, shifts, shiftTransports);
            return File(pdf, "application/pdf", "schedule.pdf");
        }
    }

    public class TransportConnectionRequest
    {
        public DateTime ArrivalTime { get; set; }
        public string? EndStation { get; set; }
    }
}
