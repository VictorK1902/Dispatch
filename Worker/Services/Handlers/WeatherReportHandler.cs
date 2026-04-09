using System.Text.Json;
using Dispatch.Contracts;
using Dispatch.Contracts.JobModules;
using Dispatch.Data.Entities;
using Dispatch.Worker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dispatch.Worker.Services.Handlers;

public class WeatherReportHandler : IJobModuleHandler
{
    public int JobModuleId => JobModuleTypes.WeatherReport;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IEmailService _emailService;
    private readonly IChartService _chartService;
    private readonly IWeatherApiService _weatherApiService;
    private readonly ILogger<WeatherReportHandler> _logger;

    public WeatherReportHandler(IEmailService emailService, IChartService chartService, IWeatherApiService weatherApiService, ILogger<WeatherReportHandler> logger)
    {
        _emailService = emailService;
        _chartService = chartService;
        _weatherApiService = weatherApiService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(Job job, CancellationToken cancellationToken)
    {
        var input = JsonSerializer.Deserialize<WeatherReportInput>(job.DataPayload, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize DataPayload for job {job.Id}");

        _logger.LogInformation("Executing WeatherReport for job {JobId}", job.Id);

        var data = await _weatherApiService.GetForecastAsync(input.Latitude, input.Longitude, input.ForecastDays, cancellationToken);
        var chartData = _chartService.CreateLineChart("Weather Forecast Chart", "Time", "Temperature (Fahrenheit)", data.Times, data.Temperatures);

        return await _emailService.SendAsyncWithImageAttachment(input.SendTo, 
                        "Weather Forecast Chart", 
                        $"Please see attached for the weather forecast chart for ({input.Latitude},{input.Longitude})",
                        chartData, "chart.png", "image/png", cancellationToken);
    }
}
