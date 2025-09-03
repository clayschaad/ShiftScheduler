using ShiftScheduler.Shared;

namespace ShiftScheduler.Services
{
    public interface ITransportService
    {
        Task<TransportConnection?> GetConnectionAsync(DateTimeOffset shiftStartTime);
    }
    
    public interface ITransportApiService
    {
        Task<TransportConnection?> GetConnectionAsync(DateTimeOffset shiftStartTime);
    }
}