using ShiftScheduler.Shared;

namespace ShiftScheduler.Services
{
    public interface ITransportService
    {
        Task<TransportConnection?> GetConnectionAsync(DateTime shiftStartTime);
    }
    
    public interface ITransportApiService
    {
        Task<TransportConnection?> GetConnectionAsync(DateTime shiftStartTime);
    }
}