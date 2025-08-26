using ShiftScheduler.Shared;
using System.Text.Json;

namespace ShiftScheduler.Services
{
    public interface IConfigurationService
    {
        ApplicationConfiguration GetConfiguration();
        void UpdateConfiguration(ApplicationConfiguration configuration);
        Task<string> ExportConfigurationAsync();
        Task ImportConfigurationAsync(string jsonContent);
        List<Shift> GetShifts();
        TransportConfiguration GetTransportConfiguration();
    }

    public class ConfigurationService : IConfigurationService
    {
        private ApplicationConfiguration _configuration;
        private readonly object _lock = new object();

        public ConfigurationService(ApplicationConfiguration initialConfiguration)
        {
            _configuration = initialConfiguration;
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
    }
}