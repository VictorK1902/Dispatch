using System.Text.Json;
using Dispatch.Contracts;
using Dispatch.Data.Entities;
using Dispatch.Worker.Interfaces;
using Dispatch.Worker.Services.Handlers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Worker.Tests.Services.Handlers;

public class WeatherReportHandlerTests
{
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<IChartService> _chartServiceMock = new();
    private readonly Mock<IWeatherApiService> _weatherApiMock = new();
    private readonly WeatherReportHandler _sut;

    public WeatherReportHandlerTests()
    {
        var loggerMock = new Mock<ILogger<WeatherReportHandler>>();
        _sut = new WeatherReportHandler(_emailServiceMock.Object, _chartServiceMock.Object, _weatherApiMock.Object, loggerMock.Object);
    }

    // These datetime props don't matter in this test class
    private static Job CreateJob(string payload) => new()
    {
        Id = Guid.NewGuid(),
        ClientId = "test",
        JobModuleId = JobModuleTypes.WeatherReport,
        Status = JobStatus.Scheduled,
        ScheduledAt = DateTime.UtcNow,
        DataPayload = payload,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void JobModuleId_ReturnsWeatherReport()
    {
        Assert.Equal(JobModuleTypes.WeatherReport, _sut.JobModuleId);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_FetchesDataCreatesChartSendsEmail()
    {
        var payload = JsonSerializer.Serialize(new { sendTo = "user@test.com", latitude = 40.123, longitude = -74.123, forecastDays = 3, day = DateTime.UtcNow.Date });
        var job = CreateJob(payload);

        var times = new[] { DateTime.UtcNow, DateTime.UtcNow.AddHours(1) };
        var temps = new[] { 72.0, 75.0 };
        _weatherApiMock.Setup(w => w.GetForecastAsync(40.123, -74.123, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherForecast { Times = times, Temperatures = temps });

        var chartBytes = new byte[] { 1, 2, 3 };
        _chartServiceMock.Setup(c => c.CreateLineChart("Weather Forecast Chart", "Time", "Temperature (Fahrenheit)", times, temps))
            .Returns(chartBytes);

        _emailServiceMock.Setup(e => e.SendAsyncWithImageAttachment("user@test.com", It.IsAny<string>(), "Please see attached for the weather forecast chart for (40.123,-74.123)", chartBytes, "chart.png", "image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync("acs-msg-456");

        var result = await _sut.ExecuteAsync(job, CancellationToken.None);

        Assert.Equal("acs-msg-456", result);
        _weatherApiMock.Verify(w => w.GetForecastAsync(40.123, -74.123, 3, It.IsAny<CancellationToken>()), Times.Once);
        _chartServiceMock.Verify(c => c.CreateLineChart("Weather Forecast Chart", "Time", "Temperature (Fahrenheit)", times, temps), Times.Once);
        _emailServiceMock.Verify(e => e.SendAsyncWithImageAttachment("user@test.com", "Weather Forecast Chart", "Please see attached for the weather forecast chart for (40.123,-74.123)", chartBytes, "chart.png", "image/png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPayload_Throws()
    {
        var job = CreateJob("invalid json {{{");

        await Assert.ThrowsAsync<JsonException>(() => _sut.ExecuteAsync(job, CancellationToken.None));
    }
}
