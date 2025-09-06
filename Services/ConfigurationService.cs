using ShiftScheduler.Shared;
using System.Text.Json;

namespace ShiftScheduler.Services
{
    public interface IConfigurationService
    {
        (DateTimeOffset StartTime, DateTimeOffset EndTime) GetZurichTime(DateOnly date, string timeRange);
        ApplicationConfiguration GetConfiguration();
        void UpdateConfiguration(ApplicationConfiguration configuration);
        Task<string> ExportConfigurationAsync();
        Task ImportConfigurationAsync(string jsonContent);
        List<Shift> GetShifts();
        TransportConfiguration GetTransportConfiguration();
        
        Task SaveScheduleAsync(int year, int month, Dictionary<DateTime, string> schedule);
        Task<Dictionary<DateTime, string>> LoadScheduleAsync(int year, int month);
        Task DeleteScheduleAsync(int year, int month);

        ShiftTimes ParseShiftTimes(DateTime date, Shift shift);
    }

    public record ShiftTimes(DateTimeOffset? MorningStart, DateTimeOffset? MorningEnd, DateTimeOffset? AfternoonStart, DateTimeOffset? AfternoonEnd);

    public class ConfigurationService : IConfigurationService
    {
        private ApplicationConfiguration _configuration;
        private readonly object _lock = new object();
        private readonly string _configDirectory;
        private readonly string _shiftsFilePath;
        private readonly string _transportFilePath;

        private readonly TimeZoneInfo localTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

        public (DateTimeOffset StartTime, DateTimeOffset EndTime) GetZurichTime(DateOnly date, string timeRange)
        {
            var times = timeRange.Split('-'); ;
            var startTime = TimeOnly.Parse(times[0]);
            var endTime = TimeOnly.Parse(times[1]);
            
            TimeSpan offset = localTimeZone.GetUtcOffset(DateTime.UtcNow);
            return (new DateTimeOffset(date, startTime, offset), new DateTimeOffset(date, endTime, offset));
        }

        public ConfigurationService(ApplicationConfiguration initialConfiguration)
        {
            _configDirectory = Path.Combine(Directory.GetCurrentDirectory(), "config");
            _shiftsFilePath = Path.Combine(_configDirectory, "shifts.json");
            _transportFilePath = Path.Combine(_configDirectory, "transport.json");
            
            // Ensure config directory exists
            Directory.CreateDirectory(_configDirectory);
            
            // Load configuration from external files if they exist, otherwise use provided configuration
            _configuration = LoadConfigurationFromFiles(initialConfiguration);
            
            // If no external files existed, save the initial configuration to create them
            if (!File.Exists(_shiftsFilePath) || !File.Exists(_transportFilePath))
            {
                SaveConfigurationToFiles(_configuration);
            }
        }

        public ApplicationConfiguration GetConfiguration()
        {
            lock (_lock)
            {
                return new ApplicationConfiguration
                {
                    Transport = new TransportConfiguration
                    {
                        StartStation = _configuration.Transport.StartStation,
                        EndStation = _configuration.Transport.EndStation,
                        ApiBaseUrl = _configuration.Transport.ApiBaseUrl,
                        SafetyBufferMinutes = _configuration.Transport.SafetyBufferMinutes,
                        MinBreakMinutes = _configuration.Transport.MinBreakMinutes,
                        MaxEarlyArrivalMinutes = _configuration.Transport.MaxEarlyArrivalMinutes,
                        MaxLateArrivalMinutes = _configuration.Transport.MaxLateArrivalMinutes,
                        CacheDurationDays = _configuration.Transport.CacheDurationDays
                    },
                    Shifts = _configuration.Shifts.Select(s => new Shift
                    {
                        Name = s.Name,
                        Icon = s.Icon,
                        MorningTime = s.MorningTime,
                        AfternoonTime = s.AfternoonTime
                    }).ToList()
                };
            }
        }

        public void UpdateConfiguration(ApplicationConfiguration configuration)
        {
            lock (_lock)
            {
                _configuration = new ApplicationConfiguration
                {
                    Transport = new TransportConfiguration
                    {
                        StartStation = configuration.Transport.StartStation,
                        EndStation = configuration.Transport.EndStation,
                        ApiBaseUrl = configuration.Transport.ApiBaseUrl,
                        SafetyBufferMinutes = configuration.Transport.SafetyBufferMinutes,
                        MinBreakMinutes = configuration.Transport.MinBreakMinutes,
                        MaxEarlyArrivalMinutes = configuration.Transport.MaxEarlyArrivalMinutes,
                        MaxLateArrivalMinutes = configuration.Transport.MaxLateArrivalMinutes,
                        CacheDurationDays = configuration.Transport.CacheDurationDays
                    },
                    Shifts = configuration.Shifts.Select(s => new Shift
                    {
                        Name = s.Name,
                        Icon = s.Icon,
                        MorningTime = s.MorningTime,
                        AfternoonTime = s.AfternoonTime
                    }).ToList()
                };
                
                // Persist changes to external files
                SaveConfigurationToFiles(_configuration);
            }
        }

        public async Task<string> ExportConfigurationAsync()
        {
            var config = GetConfiguration();
            return await Task.FromResult(JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        public async Task ImportConfigurationAsync(string jsonContent)
        {
            var configuration = JsonSerializer.Deserialize<ApplicationConfiguration>(jsonContent);
            if (configuration != null)
            {
                UpdateConfiguration(configuration);
            }
            await Task.CompletedTask;
        }

        public List<Shift> GetShifts()
        {
            return GetConfiguration().Shifts;
        }

        public TransportConfiguration GetTransportConfiguration()
        {
            return GetConfiguration().Transport;
        }
        
        // Schedule persistence methods
        public async Task SaveScheduleAsync(int year, int month, Dictionary<DateTime, string> schedule)
        {
            try
            {
                var scheduleFileName = $"schedule-{year:D4}-{month:D2}.json";
                var scheduleFilePath = Path.Combine(_configDirectory, scheduleFileName);
                
                var serializableSchedule = schedule.ToDictionary(
                    kvp => kvp.Key.ToString("yyyy-MM-dd"),
                    kvp => kvp.Value
                );
                
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(serializableSchedule, jsonOptions);
                await File.WriteAllTextAsync(scheduleFilePath, json);
            }
            catch
            {
                // Log error if needed, but don't throw to avoid breaking the application
            }
        }
        
        public async Task<Dictionary<DateTime, string>> LoadScheduleAsync(int year, int month)
        {
            try
            {
                var scheduleFileName = $"schedule-{year:D4}-{month:D2}.json";
                var scheduleFilePath = Path.Combine(_configDirectory, scheduleFileName);
                
                if (!File.Exists(scheduleFilePath))
                    return new Dictionary<DateTime, string>();
                
                var json = await File.ReadAllTextAsync(scheduleFilePath);
                var serializableSchedule = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                
                if (serializableSchedule == null)
                    return new Dictionary<DateTime, string>();
                
                return serializableSchedule.ToDictionary(
                    kvp => DateTime.Parse(kvp.Key),
                    kvp => kvp.Value
                );
            }
            catch
            {
                return new Dictionary<DateTime, string>();
            }
        }
        
        public async Task DeleteScheduleAsync(int year, int month)
        {
            try
            {
                var scheduleFileName = $"schedule-{year:D4}-{month:D2}.json";
                var scheduleFilePath = Path.Combine(_configDirectory, scheduleFileName);
                
                if (File.Exists(scheduleFilePath))
                {
                    File.Delete(scheduleFilePath);
                }
                await Task.CompletedTask;
            }
            catch
            {
                // Log error if needed, but don't throw
            }
        }

        public ShiftTimes ParseShiftTimes(DateTime date, Shift shift)
        {
            DateTimeOffset? morningStart = null;
            DateTimeOffset? morningEnd = null;
            DateTimeOffset? afternoonStart = null;
            DateTimeOffset? afternoonEnd = null;
            
            TimeSpan offset = localTimeZone.GetUtcOffset(DateTime.UtcNow);
            
            if (!string.IsNullOrEmpty(shift.MorningTime))
            {
                var times = shift.MorningTime.Split('-');
                if (times.Length == 2)
                {
                    var start = $"{date:yyyy-MM-dd}T{times[0]}:00+{offset.Hours}:{offset.Minutes}";
                    var end = $"{date:yyyy-MM-dd}T{times[1]}:00+{offset.Hours}:{offset.Minutes}";
                    morningStart = DateTimeOffset.Parse(start);
                    morningEnd = DateTimeOffset.Parse(end);
                }
            }
            
            if (!string.IsNullOrEmpty(shift.AfternoonTime))
            {
                var times = shift.AfternoonTime.Split('-');
                if (times.Length == 2)
                {
                    var start = $"{date:yyyy-MM-dd}T{times[0]}:00+{offset.Hours}:{offset.Minutes}";
                    var end = $"{date:yyyy-MM-dd}T{times[1]}:00+{offset.Hours}:{offset.Minutes}";
                    afternoonStart = DateTimeOffset.Parse(start);
                    afternoonEnd = DateTimeOffset.Parse(end);
                }
            }
            
            return new ShiftTimes(
                MorningStart: morningStart,
                MorningEnd: morningEnd,
                AfternoonStart: afternoonStart,
                AfternoonEnd: afternoonEnd);
        }

        private ApplicationConfiguration LoadConfigurationFromFiles(ApplicationConfiguration fallbackConfiguration)
        {
            try
            {
                var shifts = LoadShiftsFromFile() ?? fallbackConfiguration.Shifts;
                var transport = LoadTransportFromFile() ?? fallbackConfiguration.Transport;
                
                return new ApplicationConfiguration
                {
                    Transport = transport,
                    Shifts = shifts
                };
            }
            catch
            {
                // If any error occurs loading from files, use fallback configuration
                return fallbackConfiguration;
            }
        }
        
        private List<Shift>? LoadShiftsFromFile()
        {
            if (!File.Exists(_shiftsFilePath))
                return null;
                
            try
            {
                var json = File.ReadAllText(_shiftsFilePath);
                return JsonSerializer.Deserialize<List<Shift>>(json);
            }
            catch
            {
                return null;
            }
        }
        
        private TransportConfiguration? LoadTransportFromFile()
        {
            if (!File.Exists(_transportFilePath))
                return null;
                
            try
            {
                var json = File.ReadAllText(_transportFilePath);
                return JsonSerializer.Deserialize<TransportConfiguration>(json);
            }
            catch
            {
                return null;
            }
        }
        
        private void SaveConfigurationToFiles(ApplicationConfiguration configuration)
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                
                var shiftsJson = JsonSerializer.Serialize(configuration.Shifts, jsonOptions);
                File.WriteAllText(_shiftsFilePath, shiftsJson);
                
                var transportJson = JsonSerializer.Serialize(configuration.Transport, jsonOptions);
                File.WriteAllText(_transportFilePath, transportJson);
            }
            catch
            {
                // Log error if needed, but don't throw to avoid breaking the application
                // In a production app, you'd want proper logging here
            }
        }
    }
}