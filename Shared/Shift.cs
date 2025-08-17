namespace ShiftScheduler.Shared.Models
{
    public class Shift
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string MorningTime { get; set; } = string.Empty;
        public string AfternoonTime { get; set; } = string.Empty;
        public TransportConnection? MorningTransport { get; set; }
        public TransportConnection? AfternoonTransport { get; set; }
    }
}
