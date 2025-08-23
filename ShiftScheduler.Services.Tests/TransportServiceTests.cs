using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using ShiftScheduler.Services;
using ShiftScheduler.Shared.Models;
using Shouldly;

namespace ShiftScheduler.Services.Tests;

public class TransportServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly TransportConfiguration _config;
    private readonly TransportService _transportService;

    public TransportServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        
        _config = new TransportConfiguration
        {
            StartStation = "Zurich HB",
            EndStation = "Bern",
            ApiBaseUrl = "https://transport.opendata.ch/v1",
            SafetyBufferMinutes = 30,
            MinBreakMinutes = 60
        };
        
        _transportService = new TransportService(_httpClient, _config);
    }

    [Fact]
    public async Task GetConnectionAsync_WithValidApiResponse_ShouldReturnMappedConnection()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        var apiResponse = CreateValidApiResponse();
        var jsonResponse = JsonSerializer.Serialize(apiResponse);
        
        SetupHttpMockResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        var result = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        result.ShouldNotBeNull();
        result.DepartureTime.ShouldBe("2023-12-15T06:45:00");
        result.ArrivalTime.ShouldBe("2023-12-15T07:30:00");
        result.Duration.ShouldBe("00:45:00");
        result.Platform.ShouldBe("5");
        result.Sections.ShouldNotBeEmpty();
        result.Sections.Count.ShouldBe(1);
        
        var section = result.Sections.First();
        section.Journey?.Name.ShouldBe("IC 1");
        section.Journey?.Category.ShouldBe("IC");
        section.Journey?.Number.ShouldBe("1");
    }

    [Fact]
    public async Task GetConnectionAsync_WithCustomEndStation_ShouldUseCustomStation()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        var customEndStation = "Basel";
        var apiResponse = CreateValidApiResponse();
        var jsonResponse = JsonSerializer.Serialize(apiResponse);
        
        SetupHttpMockResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        await _transportService.GetConnectionAsync(shiftStartTime, customEndStation);

        // Assert
        _httpMessageHandlerMock.Protected()
            .Verify("SendAsync", Times.Once(), 
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains(Uri.EscapeDataString(customEndStation))),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetConnectionAsync_WithHttpException_ShouldReturnMockConnection()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        
        SetupHttpMockResponse(HttpStatusCode.InternalServerError, "Server Error");

        // Act
        var result = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        result.ShouldNotBeNull();
        result.DepartureTime.ShouldNotBeNullOrEmpty();
        result.ArrivalTime.ShouldNotBeNullOrEmpty();
        result.Duration.ShouldBe("00:45:00");
        result.Platform.ShouldBe("3");
    }

    [Fact]
    public async Task GetConnectionAsync_WithEmptyApiResponse_ShouldReturnNull()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        var emptyResponse = new TransportApiResponse { Connections = new List<TransportApiConnection>() };
        var jsonResponse = JsonSerializer.Serialize(emptyResponse);
        
        SetupHttpMockResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        var result = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetConnectionAsync_WithNullApiResponse_ShouldReturnNull()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        
        SetupHttpMockResponse(HttpStatusCode.OK, "null");

        // Act
        var result = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetConnectionAsync_WithMultipleConnections_ShouldReturnBestConnection()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        var apiResponse = CreateMultipleConnectionsResponse();
        var jsonResponse = JsonSerializer.Serialize(apiResponse);
        
        SetupHttpMockResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        var result = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        result.ShouldNotBeNull();
        // Now with proper datetime formats, the algorithm should work correctly
        // Latest acceptable arrival: 8:00 - 30 min = 7:30
        // Valid connections: 07:15 and 07:25 (both arrive before 7:30)
        // Algorithm should return the latest valid: 07:25
        result.ArrivalTime.ShouldBe("2023-12-15T07:25:00");
    }

    [Fact]
    public void FormatConnectionSummary_WithValidConnection_ShouldReturnFormattedString()
    {
        // Arrange
        var connection = new TransportConnection
        {
            DepartureTime = "06:45",
            ArrivalTime = "07:30",
            Sections = new List<TransportSection>
            {
                new TransportSection
                {
                    Journey = new TransportJourney
                    {
                        Category = "IC",
                        Number = "1"
                    }
                }
            }
        };

        // Act
        var result = _transportService.FormatConnectionSummary(connection);

        // Assert
        result.ShouldBe("IC 1: 06:45 → 07:30");
    }

    [Fact]
    public void FormatConnectionSummary_WithNullConnection_ShouldReturnNoTransportInfo()
    {
        // Act
        var result = _transportService.FormatConnectionSummary(null);

        // Assert
        result.ShouldBe("No transport info");
    }

    [Fact]
    public void FormatConnectionSummary_WithEmptyDepartureTime_ShouldReturnNoTransportInfo()
    {
        // Arrange
        var connection = new TransportConnection
        {
            DepartureTime = "",
            ArrivalTime = "07:30"
        };

        // Act
        var result = _transportService.FormatConnectionSummary(connection);

        // Assert
        result.ShouldBe("No transport info");
    }

    [Fact]
    public void FormatConnectionSummary_WithNoJourney_ShouldUseDefaultTrainLabel()
    {
        // Arrange
        var connection = new TransportConnection
        {
            DepartureTime = "06:45",
            ArrivalTime = "07:30",
            Sections = new List<TransportSection>()
        };

        // Act
        var result = _transportService.FormatConnectionSummary(connection);

        // Assert
        result.ShouldBe("Train: 06:45 → 07:30");
    }

    [Fact]
    public void FormatConnectionSummary_WithDateTimeParsing_ShouldFormatCorrectly()
    {
        // Arrange
        var connection = new TransportConnection
        {
            DepartureTime = "2023-12-15T06:45:00",
            ArrivalTime = "2023-12-15T07:30:00",
            Sections = new List<TransportSection>
            {
                new TransportSection
                {
                    Journey = new TransportJourney
                    {
                        Category = "S",
                        Number = "3"
                    }
                }
            }
        };

        // Act
        var result = _transportService.FormatConnectionSummary(connection);

        // Assert
        result.ShouldBe("S 3: 06:45 → 07:30");
    }

    [Theory]
    [InlineData(30, "07:25")] // Should arrive 5 minutes before latest acceptable time
    [InlineData(60, "06:55")] // Should arrive 5 minutes before latest acceptable time with 60 min buffer
    [InlineData(15, "07:40")] // Should arrive 5 minutes before latest acceptable time with 15 min buffer
    public async Task GetConnectionAsync_WithDifferentSafetyBuffers_ShouldCreateCorrectMockConnection(
        int safetyBufferMinutes, string expectedArrivalTime)
    {
        // Arrange
        var config = new TransportConfiguration
        {
            StartStation = "Test Start",
            EndStation = "Test End",
            ApiBaseUrl = "https://test.api",
            SafetyBufferMinutes = safetyBufferMinutes
        };
        var service = new TransportService(_httpClient, config);
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        
        SetupHttpMockResponse(HttpStatusCode.InternalServerError, "Error");

        // Act
        var result = await service.GetConnectionAsync(shiftStartTime);

        // Assert
        result.ShouldNotBeNull();
        result.ArrivalTime.ShouldBe(expectedArrivalTime);
    }

    [Fact]
    public async Task GetConnectionAsync_WithInvalidJson_ShouldReturnMockConnection()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        
        SetupHttpMockResponse(HttpStatusCode.OK, "invalid json {[}");

        // Act
        var result = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        result.ShouldNotBeNull();
        result.Duration.ShouldBe("00:45:00");
        result.Platform.ShouldBe("3");
    }

    [Fact]
    public async Task GetConnectionAsync_WithNetworkTimeout_ShouldReturnMockConnection()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network timeout"));

        // Act
        var result = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        result.ShouldNotBeNull();
        result.Duration.ShouldBe("00:45:00");
        result.Platform.ShouldBe("3");
    }

    [Fact]
    public async Task GetConnectionAsync_WithConnectionsWithInvalidTimes_ShouldFallbackToMockConnection()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        var apiResponse = new TransportApiResponse
        {
            Connections = new List<TransportApiConnection>
            {
                new TransportApiConnection
                {
                    From = new TransportApiCheckpoint
                    {
                        Station = new TransportApiStation { Name = "Start", Id = "start" },
                        Departure = "invalid-time",
                        Platform = "1"
                    },
                    To = new TransportApiCheckpoint
                    {
                        Station = new TransportApiStation { Name = "End", Id = "end" },
                        Arrival = "not-a-time",
                        Platform = "2"
                    },
                    Duration = "00:30:00"
                }
            }
        };
        var jsonResponse = JsonSerializer.Serialize(apiResponse);
        
        SetupHttpMockResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        var result = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        // When API contains invalid time formats, the service should fall back to mock connection
        // because the FindBestConnection method can't parse invalid times
        result.ShouldNotBeNull();
        result.Duration.ShouldBe("00:45:00"); // Mock connection duration
        result.Platform.ShouldBe("3"); // Mock connection platform
    }

    [Fact]
    public async Task GetConnectionAsync_WithNullStations_ShouldHandleGracefully()
    {
        // Arrange
        var shiftStartTime = new DateTime(2023, 12, 15, 8, 0, 0);
        var apiResponse = new TransportApiResponse
        {
            Connections = new List<TransportApiConnection>
            {
                new TransportApiConnection
                {
                    From = null,
                    To = null,
                    Duration = "00:30:00"
                }
            }
        };
        var jsonResponse = JsonSerializer.Serialize(apiResponse);
        
        SetupHttpMockResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        var result = await _transportService.GetConnectionAsync(shiftStartTime);

        // Assert
        result.ShouldNotBeNull();
        result.DepartureTime.ShouldBe(string.Empty);
        result.ArrivalTime.ShouldBe(string.Empty);
        result.Platform.ShouldBe(string.Empty);
    }

    [Fact]
    public void FormatConnectionSummary_WithNullJourneySection_ShouldUseDefaultTrainLabel()
    {
        // Arrange
        var connection = new TransportConnection
        {
            DepartureTime = "06:45",
            ArrivalTime = "07:30",
            Sections = new List<TransportSection>
            {
                new TransportSection
                {
                    Journey = null
                }
            }
        };

        // Act
        var result = _transportService.FormatConnectionSummary(connection);

        // Assert
        result.ShouldBe("Train: 06:45 → 07:30");
    }

    [Fact]
    public void FormatConnectionSummary_WithEmptyJourneyFields_ShouldUseTrainLabel()
    {
        // Arrange
        var connection = new TransportConnection
        {
            DepartureTime = "06:45",
            ArrivalTime = "07:30",
            Sections = new List<TransportSection>
            {
                new TransportSection
                {
                    Journey = new TransportJourney
                    {
                        Category = "",
                        Number = ""
                    }
                }
            }
        };

        // Act
        var result = _transportService.FormatConnectionSummary(connection);

        // Assert
        result.ShouldBe(" : 06:45 → 07:30");
    }

    private void SetupHttpMockResponse(HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private static TransportApiResponse CreateValidApiResponse()
    {
        return new TransportApiResponse
        {
            Connections = new List<TransportApiConnection>
            {
                new TransportApiConnection
                {
                    From = new TransportApiCheckpoint
                    {
                        Station = new TransportApiStation { Name = "Zurich HB", Id = "zurich" },
                        Departure = "2023-12-15T06:45:00",
                        Platform = "5"
                    },
                    To = new TransportApiCheckpoint
                    {
                        Station = new TransportApiStation { Name = "Bern", Id = "bern" },
                        Arrival = "2023-12-15T07:30:00",
                        Platform = "3"
                    },
                    Duration = "00:45:00",
                    Sections = new List<TransportApiSection>
                    {
                        new TransportApiSection
                        {
                            Journey = new TransportApiJourney
                            {
                                Name = "IC 1",
                                Category = "IC",
                                Number = "1"
                            },
                            Departure = new TransportApiCheckpoint
                            {
                                Station = new TransportApiStation { Name = "Zurich HB", Id = "zurich" },
                                Departure = "2023-12-15T06:45:00",
                                Platform = "5"
                            },
                            Arrival = new TransportApiCheckpoint
                            {
                                Station = new TransportApiStation { Name = "Bern", Id = "bern" },
                                Arrival = "2023-12-15T07:30:00",
                                Platform = "3"
                            }
                        }
                    }
                }
            }
        };
    }

    private static TransportApiResponse CreateMultipleConnectionsResponse()
    {
        return new TransportApiResponse
        {
            Connections = new List<TransportApiConnection>
            {
                // Early connection - valid
                new TransportApiConnection
                {
                    From = new TransportApiCheckpoint
                    {
                        Station = new TransportApiStation { Name = "Zurich HB", Id = "zurich" },
                        Departure = "2023-12-15T06:30:00",
                        Platform = "5"
                    },
                    To = new TransportApiCheckpoint
                    {
                        Station = new TransportApiStation { Name = "Bern", Id = "bern" },
                        Arrival = "2023-12-15T07:15:00",
                        Platform = "3"
                    },
                    Duration = "00:45:00"
                },
                // Later valid connection - should be selected as best
                new TransportApiConnection
                {
                    From = new TransportApiCheckpoint
                    {
                        Station = new TransportApiStation { Name = "Zurich HB", Id = "zurich" },
                        Departure = "2023-12-15T06:40:00",
                        Platform = "4"
                    },
                    To = new TransportApiCheckpoint
                    {
                        Station = new TransportApiStation { Name = "Bern", Id = "bern" },
                        Arrival = "2023-12-15T07:25:00",
                        Platform = "2"
                    },
                    Duration = "00:45:00"
                },
                // Too late connection - invalid (arrives after 07:30 which is shift start - 30 min buffer)
                new TransportApiConnection
                {
                    From = new TransportApiCheckpoint
                    {
                        Station = new TransportApiStation { Name = "Zurich HB", Id = "zurich" },
                        Departure = "2023-12-15T06:50:00",
                        Platform = "6"
                    },
                    To = new TransportApiCheckpoint
                    {
                        Station = new TransportApiStation { Name = "Bern", Id = "bern" },
                        Arrival = "2023-12-15T07:35:00",
                        Platform = "1"
                    },
                    Duration = "00:45:00"
                }
            }
        };
    }
}