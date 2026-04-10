using System.Net;
using System.Text.Json;
using Dispatch.Worker.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace Worker.Tests.Services;

public class StockPriceApiServiceTests
{
    private static StockPriceApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://fake-api.test") };
        var options = Options.Create(new StockPriceApiServiceOptions { ApiKey = "test-key" });
        return new StockPriceApiService(httpClient, options);
    }

    [Fact]
    public async Task GetMonthlyPricesAsync_HappyPath_ParsesResponse()
    {
        var json = """
        {
            "Monthly Time Series": {
                "2026-01-31": { "4. close": "150.00" },
                "2026-02-28": { "4. close": "155.50" }
            }
        }
        """;
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var result = await service.GetMonthlyPricesAsync("AAPL", CancellationToken.None);

        Assert.Equal(2, result.Dates.Length);
        Assert.Equal(2, result.ClosingPrices.Length);
        Assert.Equal(new DateTime(2026, 1, 31), result.Dates[0]);
        Assert.Equal(new DateTime(2026, 2, 28), result.Dates[1]);
        Assert.Equal(150.00D, result.ClosingPrices[0]);
        Assert.Equal(155.50D, result.ClosingPrices[1]);
    }

    [Fact]
    public async Task GetMonthlyPricesAsync_ApiError_ThrowsStockPriceApiException()
    {
        var json = """{ "Error Message": "Invalid API call" }""";
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<StockPriceApiException>(() =>
            service.GetMonthlyPricesAsync("INVALID", CancellationToken.None));

        Assert.Contains("Invalid API call", ex.Message);
    }

    [Fact]
    public async Task GetMonthlyPricesAsync_HttpFailure_Throws()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError, "error");
        var service = CreateService(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetMonthlyPricesAsync("AAPL", CancellationToken.None));
    }
}

internal class FakeHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;

    public FakeHttpHandler(HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content)
        });
    }
}
