namespace Dispatch.Contracts.JobModules;

public class WeatherReportInput
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Day { get; set; }
    public int ForecastDays { get; set; }
    public string SendTo { get; set; } = string.Empty;
}
