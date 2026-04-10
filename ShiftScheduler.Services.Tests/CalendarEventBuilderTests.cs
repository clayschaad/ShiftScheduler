using ShiftScheduler.Services;
using ShiftScheduler.Shared;
using Shouldly;

namespace ShiftScheduler.Services.Tests;

public class CalendarEventBuilderTests
{
    [Fact]
    public void BuildEventContent_WithTransport_IncludesTransportInDescription()
    {
        var shift = new Shift { Name = "Früh" };
        var transport = new TransportConnection
        {
            Platform = "Gleis 3",
            DepartureTime = DateTimeOffset.Parse("2026-04-01T05:30:00+02:00"),
            ArrivalTime = DateTimeOffset.Parse("2026-04-01T05:55:00+02:00"),
            Duration = TimeSpan.FromMinutes(25)
        };

        var result = CalendarEventBuilder.BuildEventContent(shift, transport);

        result.Summary.ShouldBe("Früh");
        result.Description.ShouldBe("Transport: Gleis 3: 05:30 → 05:55 (25 Minutes)");
    }

    [Fact]
    public void BuildEventContent_WithoutTransport_EmptyDescription()
    {
        var shift = new Shift { Name = "Spät" };

        var result = CalendarEventBuilder.BuildEventContent(shift, null);

        result.Summary.ShouldBe("Spät");
        result.Description.ShouldBeEmpty();
    }

    [Fact]
    public void FormatTransportInfo_FormatsCorrectly()
    {
        var transport = new TransportConnection
        {
            Platform = "Gleis 3",
            DepartureTime = DateTimeOffset.Parse("2026-04-01T05:30:00+02:00"),
            ArrivalTime = DateTimeOffset.Parse("2026-04-01T05:55:00+02:00"),
            Duration = TimeSpan.FromMinutes(25)
        };

        var result = CalendarEventBuilder.FormatTransportInfo(transport);

        result.ShouldBe("Gleis 3: 05:30 → 05:55 (25 Minutes)");
    }

    [Fact]
    public void FormatTransportInfo_WithNullPlatform_UsesEmptyString()
    {
        var transport = new TransportConnection
        {
            Platform = null,
            DepartureTime = DateTimeOffset.Parse("2026-04-01T05:30:00+02:00"),
            ArrivalTime = DateTimeOffset.Parse("2026-04-01T05:55:00+02:00"),
            Duration = TimeSpan.FromMinutes(25)
        };

        var result = CalendarEventBuilder.FormatTransportInfo(transport);

        result.ShouldBe(": 05:30 → 05:55 (25 Minutes)");
    }
}
