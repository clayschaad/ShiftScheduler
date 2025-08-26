
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services
{
    public class ShiftService
    {
        private readonly IConfigurationService _configurationService;

        public ShiftService(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public List<Shift> GetShifts() => _configurationService.GetShifts();
    }
}
