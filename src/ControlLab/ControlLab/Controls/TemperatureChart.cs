using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Media;
using ControlLab.Services;

namespace ControlLab.Controls;

public sealed class TemperatureChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<ThermalSample>?> SamplesProperty =
        AvaloniaProperty.Register<TemperatureChart, IReadOnlyList<ThermalSample>?>(nameof(Samples));
    public static readonly StyledProperty<bool> IsDarkProperty =
        AvaloniaProperty.Register<TemperatureChart, bool>(nameof(IsDark), true);
    public IReadOnlyList<ThermalSample>? Samples { get => GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }
    public bool IsDark { get => GetValue(IsDarkProperty); set => SetValue(IsDarkProperty, value); }
    static TemperatureChart() => AffectsRender<TemperatureChart>(SamplesProperty, IsDarkProperty);

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var text = new SolidColorBrush(IsDark ? Color.Parse("#AAAAAA") : Color.Parse("#666666"));
        var grid = new Pen(new SolidColorBrush(IsDark ? Color.Parse("#343434") : Color.Parse("#E5E5E5")), 1);
        var actual = new SolidColorBrush(IsDark ? Color.Parse("#75BEFF") : Color.Parse("#005FB8"));
        var target = new SolidColorBrush(IsDark ? Color.Parse("#E5C07B") : Color.Parse("#A56A00"));
        void Label(string value, double x, double y) => context.DrawText(new FormattedText(value,
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 12, text), new Point(x, y));
        var plot = new Rect(48, 32, Math.Max(1, Bounds.Width - 66), Math.Max(1, Bounds.Height - 62));
        context.DrawLine(new Pen(actual, 2), new Point(50, 14), new Point(70, 14)); Label("实际温度", 78, 5);
        context.DrawLine(new Pen(target, 1.5, new DashStyle([4, 3], 0)), new Point(160, 14), new Point(180, 14)); Label("目标温度", 188, 5);
        double Y(double value) => plot.Bottom - (value - 20) / 65 * plot.Height;
        foreach (var value in new[] { 20, 40, 60, 80 })
        {
            context.DrawLine(grid, new Point(plot.Left, Y(value)), new Point(plot.Right, Y(value)));
            Label(value + "°", 10, Y(value) - 8);
        }
        var samples = Samples;
        if (samples is null || samples.Count == 0) { Label("等待温度数据", plot.Left + 12, plot.Top + 14); return; }
        var end = samples[^1].Seconds;
        var start = Math.Max(0, end - 180);
        var span = Math.Max(30, end - start);
        double X(double seconds) => plot.Left + (seconds - start) / span * plot.Width;
        for (var i = 0; i <= 4; i++) Label($"{start + span * i / 4:F0}s", plot.Left + plot.Width * i / 4 - 10, plot.Bottom + 8);
        void Series(Func<ThermalSample, double> selector, IPen pen)
        {
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                var first = true;
                foreach (var sample in samples)
                {
                    if (sample.Seconds < start) continue;
                    var point = new Point(X(sample.Seconds), Y(selector(sample)));
                    if (first) { path.BeginFigure(point, false); first = false; } else path.LineTo(point);
                }
            }
            context.DrawGeometry(null, pen, geometry);
        }
        using (context.PushClip(plot))
        {
            Series(p => p.Target, new Pen(target, 1.5, new DashStyle([4, 3], 0)));
            Series(p => p.Temperature, new Pen(actual, 2));
        }
    }
}
