using System.Text.Json;
using Dispatch.Contracts;
using Dispatch.Contracts.JobModules;
using Dispatch.Data.Entities;
using Dispatch.Worker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dispatch.Worker.Services.Handlers;

public class StockPriceReportHandler : IJobModuleHandler
{
    public int JobModuleId => JobModuleTypes.StockPriceReport;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IEmailService _emailService;
    private readonly IChartService _chartService;
    private readonly IStockPriceApiService _stockPriceApiService;
    private readonly ILogger<StockPriceReportHandler> _logger;

    public StockPriceReportHandler(IEmailService emailService, IChartService chartService, IStockPriceApiService stockPriceApiService, ILogger<StockPriceReportHandler> logger)
    {
        _emailService = emailService;
        _chartService = chartService;
        _stockPriceApiService = stockPriceApiService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(Job job, CancellationToken cancellationToken)
    {
        var input = JsonSerializer.Deserialize<StockPriceReportInput>(job.DataPayload, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize DataPayload for job {job.Id}");

        _logger.LogInformation("Executing StockPriceReport for job {JobId}: Symbol={Symbol}", job.Id, input.Symbol);

        var data = await _stockPriceApiService.GetMonthlyPricesAsync(input.Symbol, cancellationToken);
        var chartData = _chartService.CreateLineChart($"{input.Symbol} Historical Price Chart", "Month/Year", "Closing Price", data.Dates, data.ClosingPrices);

        return await _emailService.SendAsyncWithImageAttachment(input.SendTo, 
                                $"Stock Price Report for {input.Symbol}", 
                                $"Please see attached for the historical monthly stock price of {input.Symbol}",
                                chartData, "chart.png", "image/png", cancellationToken);
    }
}
