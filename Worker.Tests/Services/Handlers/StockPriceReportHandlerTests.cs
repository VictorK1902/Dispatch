using System.Text.Json;
using Dispatch.Contracts;
using Dispatch.Data.Entities;
using Dispatch.Worker.Interfaces;
using Dispatch.Worker.Services.Handlers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Worker.Tests.Services.Handlers;

public class StockPriceReportHandlerTests
{
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<IChartService> _chartServiceMock = new();
    private readonly Mock<IStockPriceApiService> _stockApiMock = new();
    private readonly StockPriceReportHandler _sprHandler;

    public StockPriceReportHandlerTests()
    {
        var loggerMock = new Mock<ILogger<StockPriceReportHandler>>();
        _sprHandler = new StockPriceReportHandler(_emailServiceMock.Object, _chartServiceMock.Object, _stockApiMock.Object, loggerMock.Object);
    }

    // These datetime props don't matter in this test class
    private static Job CreateJob(string payload) => new()
    {
        Id = Guid.NewGuid(),
        ClientId = "test",
        JobModuleId = JobModuleTypes.StockPriceReport,
        Status = JobStatus.Scheduled,
        ScheduledAt = DateTime.UtcNow,
        DataPayload = payload,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void JobModuleId_ReturnsStockPriceReport()
    {
        Assert.Equal(JobModuleTypes.StockPriceReport, _sprHandler.JobModuleId);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_FetchesDataCreatesChartSendsEmail()
    {
        var payload = JsonSerializer.Serialize(new { symbol = "AAPL", sendTo = "user@test.com" });
        var job = CreateJob(payload);

        var dates = new[] { new DateTime(2026, 1, 31), new DateTime(2026, 2, 28) };
        var prices = new[] { 150.0D, 155.0D };
        _stockApiMock.Setup(s => s.GetMonthlyPricesAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockPriceData { Dates = dates, ClosingPrices = prices });

        var chartBytes = new byte[] { 4, 5, 6 };
        _chartServiceMock.Setup(c => c.CreateLineChart("AAPL Historical Price Chart", "Month/Year", "Closing Price", dates, prices))
            .Returns(chartBytes);

        _emailServiceMock.Setup(e => e.SendAsyncWithImageAttachment("user@test.com", "Stock Price Report for AAPL", "Please see attached for the historical monthly stock price of AAPL", chartBytes, "chart.png", "image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync("acs-msg-789");

        var result = await _sprHandler.ExecuteAsync(job, CancellationToken.None);

        Assert.Equal("acs-msg-789", result);
        _stockApiMock.Verify(s => s.GetMonthlyPricesAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        _chartServiceMock.Verify(c => c.CreateLineChart("AAPL Historical Price Chart", "Month/Year", "Closing Price", dates, prices), Times.Once);
        _emailServiceMock.Verify(e => e.SendAsyncWithImageAttachment("user@test.com", "Stock Price Report for AAPL", "Please see attached for the historical monthly stock price of AAPL", chartBytes, "chart.png", "image/png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPayload_Throws()
    {
        var job = CreateJob("invalid json {{{");

        await Assert.ThrowsAsync<JsonException>(() => _sprHandler.ExecuteAsync(job, CancellationToken.None));        
    }
}
