namespace ShiftScheduler.Shared
{
    public class Shift
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string MorningTime { get; set; } = string.Empty;
        public string AfternoonTime { get; set; } = string.Empty;

        public bool IsPngIcon => Icon.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        
        public bool IsOpenIconicIcon => Icon.StartsWith("oi-", StringComparison.OrdinalIgnoreCase);
        
        public bool IsUnicodeIcon => !IsPngIcon && !IsOpenIconicIcon;
        
        public string GetOpenIconicClass() => IsOpenIconicIcon ? $"oi {Icon}" : string.Empty;
    }
}
