using System.Text.Json;
using Dispatch.Worker.Interfaces;

namespace Dispatch.Worker.Services;

public class WeatherApiService : IWeatherApiService
{
    private readonly HttpClient _httpClient;

    public WeatherApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken)
    {
        var url = $"/v1/forecast?latitude={latitude}&longitude={longitude}&hourly=temperature_2m&forecast_days={forecastDays}&wind_speed_unit=mph&temperature_unit=fahrenheit&precipitation_unit=inch";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (doc.RootElement.TryGetProperty("error", out var errorFlag) && errorFlag.GetBoolean())
        {
            var reason = doc.RootElement.TryGetProperty("reason", out var reasonElement)
                ? reasonElement.GetString() ?? "Unknown API error"
                : "Unknown API error";
            throw new WeatherApiException(reason);
        }

        var hourly = doc.RootElement.GetProperty("hourly");

        var times = hourly.GetProperty("time").EnumerateArray()
            .Select(d => DateTime.Parse(d.GetString()!))
            .ToArray();
        var temps = hourly.GetProperty("temperature_2m").EnumerateArray()
            .Select(t => t.GetDouble())
            .ToArray();

        return new WeatherForecast
        {
            Times = times,
            Temperatures = temps
        };
    }
}
