using Dispatch.Worker.Interfaces;
using ScottPlot;

namespace Dispatch.Worker.Services;

public class ChartService : IChartService
{
    public byte[] CreateLineChart(string title, string xLabel, string yLabel, DateTime[] xs, double[] ys)
    {
        using var plot = new Plot();

        double[] xDoubles = xs.Select(d => d.ToOADate()).ToArray();
        var scatter = plot.Add.ScatterLine(xDoubles, ys);

        plot.Axes.DateTimeTicksBottom();
        plot.Title(title);
        plot.XLabel(xLabel);
        plot.YLabel(yLabel);

        return plot.GetImageBytes(800, 400, ImageFormat.Png);
    }
}
