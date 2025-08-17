using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.Services;

namespace ShiftScheduler.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShiftController : ControllerBase
    {
        private readonly ShiftService _shiftService;
        private readonly IcsExportService _icsService;
        private readonly PdfExportService _pdfExportService;

        public ShiftController(ShiftService shiftService, IcsExportService icsService, PdfExportService pdfExportService)
        {
            _shiftService = shiftService;
            _icsService = icsService;
            _pdfExportService = pdfExportService;
        }

        [HttpGet("shifts")]
        public IActionResult GetShifts()
        {
            return Ok(_shiftService.GetShifts());
        }

        [HttpPost("export_ics")]
        public IActionResult ExportIcs([FromBody] Dictionary<DateTime, string> schedule)
        {
            var ics = _icsService.GenerateIcs(schedule, _shiftService.GetShifts());
            return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar", "schedule.ics");
        }

         [HttpPost("export_pdf")]
        public IActionResult ExportPdf([FromBody] Dictionary<DateTime, string> schedule)
        {
            var pdf = _pdfExportService.GenerateMonthlySchedulePdf(schedule, _shiftService.GetShifts());
            return File(pdf, "application/pdf", "schedule.pdf");
        }
    }
}
