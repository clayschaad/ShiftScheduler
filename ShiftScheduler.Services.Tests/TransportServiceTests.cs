using Microsoft.Extensions.Caching.Memory;
using Moq;
using ShiftScheduler.Services;
using ShiftScheduler.Shared;
using Shouldly;

namespace ShiftScheduler.Services.Tests;

public class TransportServiceTests
{
    private readonly Mock<ITransportApiService> _transportServiceMock;
    private readonly IMemoryCache _memoryCache;
    private readonly TransportService _transportService;
    private readonly TransportConfiguration _config;

    public TransportServiceTests()
    {
        _transportServiceMock = new Mock<ITransportApiService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        _config = new TransportConfiguration
        {
            StartStation = "Zurich HB",
            EndStation = "Bern",
            ApiBaseUrl = "https://transport.opendata.ch/v1",
            SafetyBufferMinutes = 30,
            MinBreakMinutes = 60,
            MaxEarlyArrivalMinutes = 60,
            MaxLateArrivalMinutes = 15,
            CacheDurationDays = 1
        };
        
        _transportService = new TransportService(_transportServiceMock.Object, _config, _memoryCache);
    }

    [Fact]
    public async Task GetConnectionAsync_WithValidConnection_ShouldCacheResult()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        var connection = new TransportConnection
        {
            DepartureTime = "2023-12-15T06:45:00",
            ArrivalTime = "2023-12-15T07:30:00",
            Duration = "00:45:00",
            Platform = "5"
        };
        
        _transportServiceMock
            .Setup(x => x.GetConnectionAsync(shiftStartTime))
            .ReturnsAsync(connection);

        // Act - First call should hit the transport service
        var result1 = await _transportService.GetConnectionAsync(shiftStartTime);
        
        // Act - Second call should use cache
        var result2 = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        result1.ShouldNotBeNull();
        result2.ShouldNotBeNull();
        result1.ArrivalTime.ShouldBe(result2.ArrivalTime);
        result1.DepartureTime.ShouldBe(result2.DepartureTime);
        
        // Verify that transport service was called only once
        _transportServiceMock.Verify(x => x.GetConnectionAsync(shiftStartTime), Times.Once);
    }

    [Fact]
    public async Task GetConnectionAsync_WithNullConnection_ShouldNotCache()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        
        _transportServiceMock
            .Setup(x => x.GetConnectionAsync(shiftStartTime))
            .ReturnsAsync((TransportConnection?)null);

        // Act - First call
        var result1 = await _transportService.GetConnectionAsync(shiftStartTime);
        
        // Act - Second call should call transport service again since null wasn't cached
        var result2 = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        result1.ShouldBeNull();
        result2.ShouldBeNull();
        
        // Verify that transport service was called twice (no caching for null)
        _transportServiceMock.Verify(x => x.GetConnectionAsync(shiftStartTime), Times.Exactly(2));
    }

    [Fact]
    public async Task GetConnectionAsync_WithDifferentDates_ShouldCreateSeparateCacheEntries()
    {
        // Arrange
        var shiftStartTime1 = new DateTime(2023, 12, 15, 8, 0, 0);
        var shiftStartTime2 = new DateTime(2023, 12, 16, 8, 0, 0);
        
        var connection1 = new TransportConnection { ArrivalTime = "2023-12-15T07:30:00" };
        var connection2 = new TransportConnection { ArrivalTime = "2023-12-16T07:30:00" };
        
        _transportServiceMock
            .Setup(x => x.GetConnectionAsync(shiftStartTime1))
            .ReturnsAsync(connection1);
        
        _transportServiceMock
            .Setup(x => x.GetConnectionAsync(shiftStartTime2))
            .ReturnsAsync(connection2);

        // Act - Different dates should result in different cache keys
        var result1 = await _transportService.GetConnectionAsync(shiftStartTime1);
        var result2 = await _transportService.GetConnectionAsync(shiftStartTime2);

        // Assert
        result1.ShouldNotBeNull();
        result2.ShouldNotBeNull();
        result1.ArrivalTime.ShouldBe("2023-12-15T07:30:00");
        result2.ArrivalTime.ShouldBe("2023-12-16T07:30:00");
        
        // Verify that transport service was called twice (different cache keys)
        _transportServiceMock.Verify(x => x.GetConnectionAsync(It.IsAny<DateTime>()), Times.Exactly(2));
    }
}