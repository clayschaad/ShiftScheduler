using ShiftScheduler.Services;
using ShiftScheduler.Shared;
using Shouldly;

namespace ShiftScheduler.Services.Tests;

public class TransportConnectionCalculatorTests
{
    // Tests for new enhanced logic
    [Fact]
    public void FindBestConnectionEnhanced_WithGoodTimingConnection_ShouldReturnLatestValidConnection()
    {
        // Arrange - Shift starts at 8:00, safety buffer 30 min, so latest arrival is 7:30
        var connections = new List<TransportConnection>
        {
            new TransportConnection { ArrivalTime = "2023-12-15T07:15:00" },
            new TransportConnection { ArrivalTime = "2023-12-15T07:25:00" }, // Should be selected (latest valid)
            new TransportConnection { ArrivalTime = "2023-12-15T08:05:00" }  // Too late
        };
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, shiftStartTime, 30, 60, 15);

        // Assert
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe("2023-12-15T07:25:00");
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithTooEarlyConnection_ShouldPreferLaterConnection()
    {
        // Arrange - Shift starts at 8:00, max early arrival 60 min, so earliest acceptable is 7:00
        var connections = new List<TransportConnection>
        {
            new TransportConnection { ArrivalTime = "2023-12-15T06:45:00" }, // Too early (more than 60 min before 8:00)
            new TransportConnection { ArrivalTime = "2023-12-15T08:05:00" }  // Within acceptable late range (15 min after 8:00)
        };
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, shiftStartTime, 30, 60, 15);

        // Assert
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe("2023-12-15T08:05:00");
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithMultipleLateConnections_ShouldReturnEarliest()
    {
        // Arrange - Shift starts at 8:00, no valid early connections
        var connections = new List<TransportConnection>
        {
            new TransportConnection { ArrivalTime = "2023-12-15T08:10:00" },
            new TransportConnection { ArrivalTime = "2023-12-15T08:05:00" }, // Should be selected (earliest late)
            new TransportConnection { ArrivalTime = "2023-12-15T08:12:00" }
        };
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, shiftStartTime, 30, 60, 15);

        // Assert
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe("2023-12-15T08:05:00");
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithConnectionsBeyondAcceptableRange_ShouldReturnNull()
    {
        // Arrange - All connections are too late (beyond max late arrival)
        var connections = new List<TransportConnection>
        {
            new TransportConnection { ArrivalTime = "2023-12-15T08:20:00" },
            new TransportConnection { ArrivalTime = "2023-12-15T08:25:00" },
            new TransportConnection { ArrivalTime = "2023-12-15T08:30:00" }
        };
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, shiftStartTime, 30, 60, 15);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithAcceptableEarlyConnection_ShouldNotPreferLate()
    {
        // Arrange - Early connection is acceptable (not too early), should prefer it over late
        var connections = new List<TransportConnection>
        {
            new TransportConnection { ArrivalTime = "2023-12-15T07:20:00" }, // 40 min before shift - acceptable
            new TransportConnection { ArrivalTime = "2023-12-15T08:05:00" }  // 5 min after shift
        };
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, shiftStartTime, 30, 60, 15);

        // Assert
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe("2023-12-15T07:20:00");
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithEmptyConnections_ShouldReturnNull()
    {
        // Arrange
        var connections = new List<TransportConnection>();
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);

        // Act
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, shiftStartTime, 30, 60, 15);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindBestConnectionEnhanced_WithMixedValidAndLateConnections_ShouldChooseBasedOnTiming()
    {
        // Arrange - Test scenario where both valid and late connections exist
        var connections = new List<TransportConnection>
        {
            new TransportConnection { ArrivalTime = "2023-12-15T06:30:00" }, // Too early (90 min before shift)
            new TransportConnection { ArrivalTime = "2023-12-15T07:25:00" }, // Valid but would be considered too early
            new TransportConnection { ArrivalTime = "2023-12-15T08:05:00" }, // Late but acceptable
            new TransportConnection { ArrivalTime = "2023-12-15T08:20:00" }  // Too late
        };
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);

        // Act - MaxEarlyArrivalMinutes = 30, so earliest acceptable is 7:30
        var result = TransportConnectionCalculator.FindBestConnection(
            connections, shiftStartTime, 30, 30, 15);

        // Assert - 7:25 arrives at 7:25, which is 35 min before shift (more than 30 min), so should prefer late connection
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe("2023-12-15T08:05:00");
    }
}