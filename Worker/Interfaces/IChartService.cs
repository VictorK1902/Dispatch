namespace Dispatch.Worker.Interfaces;

public interface IChartService
{
    byte[] CreateLineChart(string title, string xLabel, string yLabel, DateTime[] xs, double[] ys);
}
