using System.Text.Json;
using Dispatch.Worker.Interfaces;
using Microsoft.Extensions.Options;

namespace Dispatch.Worker.Services;

public class StockPriceApiServiceOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

public class StockPriceApiService : IStockPriceApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public StockPriceApiService(HttpClient httpClient, IOptions<StockPriceApiServiceOptions> options)
    {
        _httpClient = httpClient;
        _apiKey = options.Value.ApiKey;
    }

    public async Task<StockPriceData> GetMonthlyPricesAsync(string symbol, CancellationToken cancellationToken)
    {
        var url = $"/query?function=TIME_SERIES_MONTHLY&symbol={symbol}&apikey={_apiKey}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (doc.RootElement.TryGetProperty("Error Message", out var errorElement))
            throw new StockPriceApiException(errorElement.GetString() ?? "Unknown API error");

        var timeSeries = doc.RootElement.GetProperty("Monthly Time Series");

        var entries = timeSeries.EnumerateObject()
            .OrderBy(e => e.Name)
            .ToArray();
        
        var dates = entries.Select(e => DateTime.Parse(e.Name)).ToArray();
        var closingPrices = entries.Select(e => double.Parse(e.Value.GetProperty("4. close").GetString()!)).ToArray();

        return new StockPriceData
        {
            Dates = dates,
            ClosingPrices = closingPrices
        };
    }
}
