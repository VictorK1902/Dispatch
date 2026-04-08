namespace Dispatch.Worker.Interfaces;

public class StockPriceData
{
    public DateTime[] Dates { get; set; } = [];
    public double[] ClosingPrices { get; set; } = [];
}

public interface IStockPriceApiService
{
    Task<StockPriceData> GetMonthlyPricesAsync(string symbol, CancellationToken cancellationToken);
}
