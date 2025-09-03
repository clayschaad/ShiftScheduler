using Microsoft.Extensions.Logging;
using Moq;
using ShiftScheduler.Shared;
using Shouldly;

namespace ShiftScheduler.Services.Tests;

public class TransportConnectionCalculatorTests
{
    private readonly Mock<ILogger> loggerMock = new();
    
    // Tests for new enhanced logic
    [Fact]
    public void FindBestConnectionEnhanced_WithGoodTimingConnection_ShouldReturnLatestValidConnection()
    {
        // Arrange - Shift starts at 8:00, safety buffer 30 min, so latest arrival is 7:30
        var connections = new List<TransportConnection>
        {
            new(arrivalTime: "2023-12-15T07:15:00"),
            new(arrivalTime: "2023-12-15T07:25:00"), // Should be selected (latest valid)
            new(arrivalTime: "2023-12-15T08:05:00") // Too late
        };
        var shiftStartTime = T("2023-12-15T08:00:00");

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, 
            new ConnectionPickArgument(shiftStartTime, 30, 60, 15),
            loggerMock.Object);

        // Assert
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe(T("2023-12-15T07:25:00"));
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithTooEarlyConnection_ShouldPreferLaterConnection()
    {
        // Arrange - Shift starts at 8:00, max early arrival 60 min, so earliest acceptable is 7:00
        var connections = new List<TransportConnection>
        {
            new(arrivalTime: "2023-12-15T06:45:00"), // Too early (more than 60 min before 8:00)
            new(arrivalTime: "2023-12-15T08:05:00") // Within acceptable late range (15 min after 8:00)
        };
        var shiftStartTime = T("2023-12-15T08:00:00");

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, 
            new ConnectionPickArgument(shiftStartTime, 30, 60, 15),
            loggerMock.Object);

        // Assert
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe(T("2023-12-15T08:05:00"));
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithMultipleLateConnections_ShouldReturnEarliest()
    {
        // Arrange - Shift starts at 8:00, no valid early connections
        var connections = new List<TransportConnection>
        {
            new(arrivalTime: "2023-12-15T08:10:00"),
            new(arrivalTime: "2023-12-15T08:05:00"), // Should be selected (earliest late)
            new(arrivalTime: "2023-12-15T08:12:00")
        };
        var shiftStartTime = T("2023-12-15T08:00:00");

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, 
            new ConnectionPickArgument(shiftStartTime, 30, 60, 15),
            loggerMock.Object);

        // Assert
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe(T("2023-12-15T08:05:00"));
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithConnectionsBeyondAcceptableRange_ShouldReturnNull()
    {
        // Arrange - All connections are too late (beyond max late arrival)
        var connections = new List<TransportConnection>
        {
            new(arrivalTime: "2023-12-15T08:20:00"),
            new(arrivalTime: "2023-12-15T08:25:00"),
            new(arrivalTime: "2023-12-15T08:30:00")
        };
        var shiftStartTime = T("2023-12-15T08:00:00");

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections,
            new ConnectionPickArgument(shiftStartTime, 30, 60, 15),
            loggerMock.Object);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithAcceptableEarlyConnection_ShouldNotPreferLate()
    {
        // Arrange - Early connection is acceptable (not too early), should prefer it over late
        var connections = new List<TransportConnection>
        {
            new(arrivalTime: "2023-12-15T07:20:00"), // 40 min before shift - acceptable
            new(arrivalTime: "2023-12-15T08:05:00") // 5 min after shift
        };
        var shiftStartTime = T("2023-12-15T08:00:00");

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, 
            new ConnectionPickArgument(shiftStartTime, 30, 60, 15),
            loggerMock.Object);

        // Assert
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe(T("2023-12-15T07:20:00"));
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithEmptyConnections_ShouldReturnNull()
    {
        // Arrange
        var shiftStartTime = T("2023-12-15T08:00:00");

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            new List<TransportConnection>(), 
            new ConnectionPickArgument(shiftStartTime, 30, 60, 15),
            loggerMock.Object);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithMixedValidAndLateConnections_ShouldChooseBasedOnTiming()
    {
        // Arrange - Test scenario where both valid and late connections exist
        var connections = new List<TransportConnection>
        {
            new(arrivalTime: "2023-12-15T06:30:00"), // Too early (90 min before shift)
            new(arrivalTime: "2023-12-15T07:25:00"), // Valid but would be considered too early
            new(arrivalTime: "2023-12-15T08:05:00"), // Late but acceptable
            new(arrivalTime: "2023-12-15T08:20:00") // Too late
        };
        var shiftStartTime = T("2023-12-15T08:00:00");

        // Act - MaxEarlyArrivalMinutes = 30, so earliest acceptable is 7:30
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, 
            new ConnectionPickArgument(shiftStartTime, 30, 30, 15),
            loggerMock.Object);

        // Assert - 7:25 arrives at 7:25, which is 35 min before shift (more than 30 min), so should prefer late connection
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe(T("2023-12-15T08:05:00"));
    }
    
    [Fact]
    public void FindBestConnectionEnhanced_WithMyCase_ShouldChooseBasedOnTiming()
    {
        var connections = new List<TransportConnection>
        {
            new(arrivalTime: "2025-09-01T11:41:00"),
            new(arrivalTime: "2025-09-01T12:41:00"),
            new(arrivalTime: "2025-09-01T13:41:00"),
            new(arrivalTime: "2025-09-01T14:41:00"),
            new(arrivalTime: "2025-09-01T15:41:00")
        };
        var shiftStartTime = T("2025-09-01T15:30:00");

        var result = TransportConnectionCalculator.FindBestConnection(
            connections, 
            new ConnectionPickArgument(shiftStartTime, 10, 60, 15),
            loggerMock.Object);

        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe(T("2025-09-01T14:41:00"));
    }
    
    private DateTimeOffset T(string dateTimeString)
    {
        return DateTimeOffset.Parse(dateTimeString);
    }
}