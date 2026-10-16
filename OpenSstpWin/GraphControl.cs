using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenSstpWin
{
    internal sealed class GraphControl : Control
    {
        private const int GridLines = 4;
        private const double MinAxisMax = 8.0 * 1024.0;
        private static readonly double[] NiceSteps = { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 1024 };

        internal static readonly Color DownColor = Color.FromArgb(33, 150, 243);
        internal static readonly Color UpColor = Color.FromArgb(255, 152, 0);

        private IReadOnlyList<TrafficSample> samples = Array.Empty<TrafficSample>();
        private int windowSeconds = 60;

        internal GraphControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        internal void SetData(IReadOnlyList<TrafficSample> newSamples, int window)
        {
            samples = newSamples;
            windowSeconds = Math.Max(2, window);
            Invalidate();
        }

        private static double NiceMax(long peak)
        {
            if (peak <= MinAxisMax) return MinAxisMax;

            double unit = 1.0;
            while (peak / unit >= 1024.0) unit *= 1024.0;

            double scaled = peak / unit;
            foreach (double step in NiceSteps)
            {
                if (step >= scaled) return step * unit;
            }

            return 1024.0 * unit;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var font = new Font(Font.FontFamily, 8f);
            using var textBrush = new SolidBrush(Color.FromArgb(150, ForeColor));
            using var gridPen = new Pen(Color.FromArgb(50, ForeColor), 1f);
            using var farFormat = new StringFormat { Alignment = StringAlignment.Far };
            using var nearFormat = new StringFormat { Alignment = StringAlignment.Near };

            float left = g.MeasureString("000.0 MB/s", font).Width + 6f;
            float top = 10f;
            float right = Width - 8f;
            float bottom = Height - 20f;
            if (right <= left + 10f || bottom <= top + 10f) return;

            int count = Math.Min(samples.Count, windowSeconds);
            int offset = samples.Count - count;

            long peak = 0;
            for (int i = 0; i < count; i++)
            {
                peak = Math.Max(peak, Math.Max(samples[offset + i].Rx, samples[offset + i].Tx));
            }
            double axisMax = NiceMax(peak);

            for (int i = 0; i <= GridLines; i++)
            {
                float y = bottom - (bottom - top) * i / GridLines;
                g.DrawLine(gridPen, left, y, right, y);
                g.DrawString(Format.Speed(axisMax * i / GridLines), font, textBrush,
                    new RectangleF(0, y - 7f, left - 4f, 14f), farFormat);
            }

            string windowLabel = windowSeconds >= 60 ? "-" + (windowSeconds / 60) + " min" : "-" + windowSeconds + " s";
            g.DrawString(windowLabel, font, textBrush, new RectangleF(left, bottom + 4f, 100f, 14f), nearFormat);
            g.DrawString("now", font, textBrush, new RectangleF(right - 100f, bottom + 4f, 100f, 14f), farFormat);

            DrawSeries(g, count, offset, left, right, top, bottom, axisMax, true);
            DrawSeries(g, count, offset, left, right, top, bottom, axisMax, false);
        }

        private void DrawSeries(Graphics g, int count, int offset, float left, float right,
                                float top, float bottom, double axisMax, bool isDownload)
        {
            if (count < 2) return;

            Color color = isDownload ? DownColor : UpColor;
            float stepX = (right - left) / (windowSeconds - 1);

            var points = new PointF[count];
            for (int i = 0; i < count; i++)
            {
                long value = isDownload ? samples[offset + i].Rx : samples[offset + i].Tx;
                float x = right - (count - 1 - i) * stepX;
                double ratio = Math.Min(1.0, Math.Max(0.0, value / axisMax));
                float y = bottom - (bottom - top) * (float)ratio;
                points[i] = new PointF(x, y);
            }

            var polygon = new PointF[count + 2];
            Array.Copy(points, polygon, count);
            polygon[count] = new PointF(points[count - 1].X, bottom);
            polygon[count + 1] = new PointF(points[0].X, bottom);

            using var fill = new SolidBrush(Color.FromArgb(64, color));
            using var pen = new Pen(color, 2f) { LineJoin = LineJoin.Round };
            g.FillPolygon(fill, polygon);
            g.DrawLines(pen, points);
        }
    }
}
