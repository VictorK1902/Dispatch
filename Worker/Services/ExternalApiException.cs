namespace Dispatch.Worker.Services;

public class ExternalApiException(string message) : Exception(message);

public class StockPriceApiException(string message) : ExternalApiException(message);

public class WeatherApiException(string message) : ExternalApiException(message);
