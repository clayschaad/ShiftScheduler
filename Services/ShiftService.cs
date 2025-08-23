
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services
{
    public class ShiftService
    {
        private readonly List<Shift> _shifts;

        public ShiftService(List<Shift> shifts)
        {
            _shifts = shifts;
        }

        public List<Shift> GetShifts() => _shifts;
    }
}
