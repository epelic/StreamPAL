using System.Windows;
using System.Windows.Media;
using StreamForge.Models;

namespace StreamForge;

public sealed class StatisticsChart : FrameworkElement
{
    private static readonly Color[] Colors = [Color.FromRgb(49, 210, 168), Color.FromRgb(62, 166, 255), Color.FromRgb(238, 83, 255), Color.FromRgb(255, 185, 48), Color.FromRgb(126, 105, 255), Color.FromRgb(255, 100, 120)];
    public SourceInstance? Instance { get; set; }
    public IReadOnlyList<ListenerSample> Samples { get; set; } = [];

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); var w = ActualWidth; var h = ActualHeight; if (w < 100 || h < 100) return;
        dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(190, 203, 213)), 1), new Rect(0, 0, w, h), 8, 8);
        var plot = new Rect(54, 24, Math.Max(10, w - 76), Math.Max(10, h - 68));
        var max = Math.Max(10, Samples.SelectMany(x => x.Streams.Values.Append(x.Total)).DefaultIfEmpty().Max()); max = (int)Math.Ceiling(max / 10d) * 10;
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(220, 228, 234)), 1);
        for (var i = 0; i <= 4; i++) { var y = plot.Bottom - plot.Height * i / 4; dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y)); Label(dc, (max * i / 4).ToString(), 10, new Point(10, y - 7)); }
        for (var i = 0; i <= 6; i++) { var x = plot.Left + plot.Width * i / 6; dc.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom)); Label(dc, $"−{72 - i * 12}h", 10, new Point(x - 12, plot.Bottom + 8)); }
        var start = DateTime.UtcNow.AddHours(-72); var end = DateTime.UtcNow;
        if (Instance is not null)
        {
            var active = Instance.Encoders.Where(e => Samples.Any(s => s.Streams.ContainsKey(e.Id))).ToList();
            for (var i = 0; i < active.Count; i++) DrawSeries(dc, plot, start, end, max, Samples.Select(s => (s.TimestampUtc, s.Streams.GetValueOrDefault(active[i].Id))), Colors[i % Colors.Length], 1.7);
            DrawSeries(dc, plot, start, end, max, Samples.Select(s => (s.TimestampUtc, s.Total)), Color.FromRgb(25, 35, 45), 2.6);
            var legendX = plot.Left; foreach (var (encoder, i) in active.Select((e, i) => (e, i))) { dc.DrawEllipse(new SolidColorBrush(Colors[i % Colors.Length]), null, new Point(legendX + 4, 12), 4, 4); Label(dc, encoder.Name, 10, new Point(legendX + 11, 4)); legendX += Math.Min(150, 20 + encoder.Name.Length * 7); }
        }
    }

    private static void DrawSeries(DrawingContext dc, Rect plot, DateTime start, DateTime end, int max, IEnumerable<(DateTime Time, int Value)> values, Color color, double width)
    {
        var geometry = new StreamGeometry(); using var ctx = geometry.Open(); var first = true;
        foreach (var p in values.Where(x => x.Time >= start).OrderBy(x => x.Time)) { var x = plot.Left + (p.Time - start).TotalSeconds / (end - start).TotalSeconds * plot.Width; var y = plot.Bottom - Math.Clamp(p.Value / (double)max, 0, 1) * plot.Height; if (first) { ctx.BeginFigure(new Point(x, y), false, false); first = false; } else ctx.LineTo(new Point(x, y), true, false); }
        geometry.Freeze(); dc.DrawGeometry(null, new Pen(new SolidColorBrush(color), width), geometry);
    }
    private static void Label(DrawingContext dc, string text, double size, Point point) { var f = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, new SolidColorBrush(Color.FromRgb(150, 170, 186)), 1.0); dc.DrawText(f, point); }
}
