using Microsoft.Maui.Graphics;

namespace SBSBeautySpa.Mobile.Views.Admin
{
    // Stacked columns: spa bookings (bottom) + shop (top) per day or month. Drawn with MAUI Graphics, no extra library.
    public class RevenueColumnsDrawable : IDrawable
    {
        public List<(string Label, double Bookings, double Shop)> Points { get; set; } = new();
        public Color BookingsColor { get; set; } = Color.FromArgb("#6D3FE0");
        public Color ShopColor { get; set; } = Color.FromArgb("#2F9BE0");

        private static readonly Color Axis = Color.FromArgb("#6E63A6");
        private static readonly Color Grid = Color.FromArgb("#ECE8F7");

        public void Draw(ICanvas canvas, RectF rect)
        {
            const float left = 44, right = 6, top = 10, bottom = 24;
            var plot = new RectF(left, top, Math.Max(10, rect.Width - left - right), Math.Max(10, rect.Height - top - bottom));
            double max = Points.Count == 0 ? 0 : Points.Max(p => p.Bookings + p.Shop);
            double niceMax = NiceMax(max);

            canvas.FontSize = 10;
            canvas.FontColor = Axis;
            for (int i = 0; i <= 4; i++)
            {
                float y = plot.Bottom - plot.Height * i / 4f;
                canvas.StrokeColor = Grid;
                canvas.StrokeSize = 1;
                canvas.DrawLine(plot.Left, y, plot.Right, y);
                canvas.DrawString(Short(niceMax * i / 4), 0, y - 8, left - 6, 16, HorizontalAlignment.Right, VerticalAlignment.Center);
            }
            if (Points.Count == 0) return;

            float slot = plot.Width / Points.Count;
            float barW = Math.Max(2, Math.Min(34, slot * 0.62f));
            int labelEvery = Math.Max(1, (int)Math.Ceiling(34f / slot));

            for (int i = 0; i < Points.Count; i++)
            {
                var p = Points[i];
                float x = plot.Left + slot * i + (slot - barW) / 2f;
                float hB = (float)(p.Bookings / niceMax * plot.Height);
                float hS = (float)(p.Shop / niceMax * plot.Height);
                if (hB > 0)
                {
                    canvas.FillColor = BookingsColor;
                    canvas.FillRoundedRectangle(x, plot.Bottom - hB, barW, hB, 2);
                }
                if (hS > 0)
                {
                    canvas.FillColor = ShopColor;
                    canvas.FillRoundedRectangle(x, plot.Bottom - hB - hS, barW, hS, 2);
                }
                if (i % labelEvery == 0)
                {
                    canvas.FontColor = Axis;
                    canvas.DrawString(p.Label, x + barW / 2f - 28, plot.Bottom + 4, 56, 16, HorizontalAlignment.Center, VerticalAlignment.Top);
                }
            }
        }

        private static double NiceMax(double max)
        {
            if (max <= 0) return 100;
            double pow = Math.Pow(10, Math.Floor(Math.Log10(max)));
            foreach (var m in new[] { 1.0, 2.0, 2.5, 5.0, 10.0 })
                if (max <= m * pow) return m * pow;
            return 10 * pow;
        }

        private static string Short(double v)
        {
            if (v >= 1_000_000) return "R" + (v / 1_000_000).ToString("0.#") + "m";
            if (v >= 1000) return "R" + (v / 1000).ToString("0.#") + "k";
            return "R" + v.ToString("0");
        }
    }

    // Donut chart. Each slice carries its own colour; the total is written in the middle.
    public class DonutDrawable : IDrawable
    {
        public List<(double Value, Color Color)> Slices { get; set; } = new();
        public string CenterTop { get; set; } = "";
        public string CenterBottom { get; set; } = "";

        public void Draw(ICanvas canvas, RectF rect)
        {
            float size = Math.Min(rect.Width, rect.Height) - 8;
            if (size < 20) return;
            float cx = rect.Center.X, cy = rect.Center.Y;
            float radius = size / 2f, thickness = size * 0.2f, mid = radius - thickness / 2f;
            double total = Slices.Sum(s => s.Value);

            canvas.StrokeSize = thickness;
            canvas.StrokeLineCap = LineCap.Butt;
            if (total <= 0)
            {
                canvas.StrokeColor = Color.FromArgb("#ECE8F7");
                canvas.DrawCircle(cx, cy, mid);
            }
            else
            {
                double angle = 90; // start at 12 o'clock, go clockwise
                var nonZero = Slices.Where(s => s.Value > 0).ToList();
                foreach (var s in nonZero)
                {
                    double sweep = s.Value / total * 360.0;
                    canvas.StrokeColor = s.Color;
                    if (nonZero.Count == 1) canvas.DrawCircle(cx, cy, mid);
                    else
                    {
                        // DrawArc angles are counter-clockwise; going from angle down by sweep makes it clockwise.
                        float gap = 1.2f;
                        canvas.DrawArc(cx - mid, cy - mid, mid * 2, mid * 2, (float)angle, (float)(angle - sweep + gap), true, false);
                    }
                    angle -= sweep;
                }
            }
            canvas.FontColor = Color.FromArgb("#2B1B6B");
            canvas.FontSize = 17;
            canvas.DrawString(CenterTop, cx - radius, cy - 17, radius * 2, 22, HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.FontSize = 11;
            canvas.FontColor = Color.FromArgb("#6E63A6");
            canvas.DrawString(CenterBottom, cx - radius, cy + 4, radius * 2, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
