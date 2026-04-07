namespace Dispatch.Contracts.JobModules;

public class StockPriceReportInput
{
    public string Symbol { get; set; } = string.Empty;
    public string SendTo { get; set; } = string.Empty;
}
