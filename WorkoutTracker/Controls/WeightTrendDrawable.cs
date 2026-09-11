namespace WorkoutTracker.Controls;

/// <summary>
/// A minimal self-drawn line chart for the Progress tab — avoids pulling in a
/// charting package for what's a handful of points per exercise.
/// </summary>
public class WeightTrendDrawable : IDrawable
{
    public List<(DateOnly Date, double Value)> Points { get; init; } = new();
    public Color LineColor { get; init; } = Colors.OrangeRed;
    public string EmptyMessage { get; init; } = "Log a few weighted sets to see progress here.";

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Points.Count == 0)
        {
            canvas.FontColor = Colors.Gray;
            canvas.FontSize = 12;
            canvas.DrawString(EmptyMessage, dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        const float paddingLeft = 34f;
        const float paddingRight = 12f;
        const float paddingTop = 16f;
        const float paddingBottom = 20f;
        var left = paddingLeft;
        var right = Math.Max(paddingLeft + 1, dirtyRect.Width - paddingRight);
        var top = paddingTop;
        var bottom = Math.Max(paddingTop + 1, dirtyRect.Height - paddingBottom);

        var minY = Points.Min(p => p.Value);
        var maxY = Points.Max(p => p.Value);
        if (maxY - minY < 1) { minY -= 1; maxY += 1; }

        float XFor(int i) => Points.Count == 1 ? (left + right) / 2 : left + (right - left) * i / (Points.Count - 1);
        float YFor(double v) => (float)(bottom - (v - minY) / (maxY - minY) * (bottom - top));

        canvas.StrokeColor = Colors.Gray.WithAlpha(0.3f);
        canvas.StrokeSize = 1;
        canvas.DrawLine(left, bottom, right, bottom);

        if (Points.Count > 1)
        {
            canvas.StrokeColor = LineColor;
            canvas.StrokeSize = 2.5f;
            for (var i = 0; i < Points.Count - 1; i++)
            {
                canvas.DrawLine(XFor(i), YFor(Points[i].Value), XFor(i + 1), YFor(Points[i + 1].Value));
            }
        }

        canvas.FillColor = LineColor;
        for (var i = 0; i < Points.Count; i++)
        {
            canvas.FillCircle(XFor(i), YFor(Points[i].Value), 3.5f);
        }

        canvas.FontColor = Colors.Gray;
        canvas.FontSize = 10;
        canvas.DrawString(maxY.ToString("0.#"), 0, top - 6, paddingLeft - 6, 14, HorizontalAlignment.Right, VerticalAlignment.Center);
        canvas.DrawString(minY.ToString("0.#"), 0, bottom - 6, paddingLeft - 6, 14, HorizontalAlignment.Right, VerticalAlignment.Center);

        canvas.DrawString(Points[0].Date.ToString("MMM d"), XFor(0) - 30, bottom + 2, 60, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
        if (Points.Count > 1)
        {
            canvas.DrawString(Points[^1].Date.ToString("MMM d"), XFor(Points.Count - 1) - 30, bottom + 2, 60, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }
}
