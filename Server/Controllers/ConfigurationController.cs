using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.Services;
using ShiftScheduler.Shared;
using System.Text;

namespace ShiftScheduler.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IConfigurationService _configurationService;

        public ConfigurationController(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        [HttpGet]
        public IActionResult GetConfiguration()
        {
            return Ok(_configurationService.GetConfiguration());
        }

        [HttpPut]
        public IActionResult UpdateConfiguration([FromBody] ApplicationConfiguration configuration)
        {
            try
            {
                _configurationService.UpdateConfiguration(configuration);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to update configuration: {ex.Message}");
            }
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportConfiguration()
        {
            try
            {
                var json = await _configurationService.ExportConfigurationAsync();
                var bytes = Encoding.UTF8.GetBytes(json);
                return File(bytes, "application/json", "ShiftScheduler-Configuration.json");
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to export configuration: {ex.Message}");
            }
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportConfiguration([FromBody] ImportConfigurationRequest request)
        {
            try
            {
                await _configurationService.ImportConfigurationAsync(request.JsonContent);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to import configuration: {ex.Message}");
            }
        }
    }

    public class ImportConfigurationRequest
    {
        public string JsonContent { get; set; } = string.Empty;
    }
}