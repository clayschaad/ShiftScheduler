using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using ShiftScheduler.Services;
using ShiftScheduler.Shared;
using Shouldly;

namespace ShiftScheduler.Services.Tests;

public class TransportServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly TransportService _transportService;

    public TransportServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        
        var config = new TransportConfiguration
        {
            StartStation = "Zurich HB",
            EndStation = "Bern",
            ApiBaseUrl = "https://transport.opendata.ch/v1",
            SafetyBufferMinutes = 30,
            MinBreakMinutes = 60,
            MaxEarlyArrivalMinutes = 60,
            MaxLateArrivalMinutes = 15
        };
        
        _transportService = new TransportService(_httpClient, config);
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