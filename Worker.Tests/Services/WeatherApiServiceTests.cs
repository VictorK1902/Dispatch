using System.Net;
using System.Text.Json;
using Dispatch.Worker.Services;

namespace Worker.Tests.Services;

public class WeatherApiServiceTests
{
    private readonly DateTime baseDateTime = new DateTime(2026, 4, 15);
    private static WeatherApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://fake-api.test") };
        return new WeatherApiService(httpClient);
    }

    [Fact]
    public async Task GetForecastAsync_HappyPath_ParsesResponse()
    {
        var json = JsonSerializer.Serialize(new
        {
            hourly = new
            {
                time = new[] { baseDateTime.AddHours(1), baseDateTime.AddHours(2) },
                temperature_2m = new[] { 68.5D, 70.2D }
            }
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var result = await service.GetForecastAsync(40.0, -74.0, 1, CancellationToken.None);

        Assert.Equal(2, result.Times.Length);
        Assert.Equal(2, result.Temperatures.Length);
        Assert.Equal(baseDateTime.AddHours(1), result.Times[0]);
        Assert.Equal(baseDateTime.AddHours(2), result.Times[1]);
        Assert.Equal(68.5D, result.Temperatures[0]);
        Assert.Equal(70.2D, result.Temperatures[1]);
    }

    [Fact]
    public async Task GetForecastAsync_ApiError_ThrowsWeatherApiException()
    {
        var json = JsonSerializer.Serialize(new
        {
            error = true,
            reason = "Latitude must be in range"
        });
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<WeatherApiException>(() =>
            service.GetForecastAsync(999.0, 999.0, 1, CancellationToken.None));

        Assert.Contains("Latitude must be in range", ex.Message);
    }

    [Fact]
    public async Task GetForecastAsync_HttpFailure_Throws()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError, "error");
        var service = CreateService(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetForecastAsync(40.0, -74.0, 1, CancellationToken.None));
    }
}
