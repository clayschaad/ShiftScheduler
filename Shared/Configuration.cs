namespace ShiftScheduler.Shared
{
    public class ApplicationConfiguration
    {
        public TransportConfiguration Transport { get; set; } = new();
        public List<Shift> Shifts { get; set; } = new();
    }
}