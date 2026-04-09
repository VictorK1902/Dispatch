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
        plot.Layout.Fixed(new PixelPadding(80, 30, 60, 50));

        plot.Title(title);
        plot.Axes.Title.Label.FontName = Fonts.Default;
        plot.Axes.Title.Label.Bold = false;

        plot.XLabel(xLabel);
        plot.Axes.Bottom.Label.FontName = Fonts.Default;
        plot.Axes.Bottom.Label.Bold = false;

        plot.YLabel(yLabel);
        plot.Axes.Left.Label.FontName = Fonts.Default;
        plot.Axes.Left.Label.Bold = false;

        int width = Math.Max(800, Math.Min(1600, xs.Length * 5));
        int height = width / 2;
        return plot.GetImageBytes(width, height, ImageFormat.Png);
    }
}
