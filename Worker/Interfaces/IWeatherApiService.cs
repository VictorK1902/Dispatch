namespace Dispatch.Worker.Interfaces;

public class WeatherForecast
{
    public DateTime[] Times { get; set; } = [];
    public double[] Temperatures { get; set; } = [];
}

public interface IWeatherApiService
{
    Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken);
}
