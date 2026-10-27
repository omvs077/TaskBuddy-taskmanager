using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TaskBuddyWPF.Controls
{
    // Pure renderer: the page owns the sample history and calls SetData each tick.
    // Lets mini sidebar cards and the big detail view share the same underlying
    // history without each control maintaining its own divergent copy.
    public partial class PerformanceGraph : UserControl
    {
        private double[] _samples = Array.Empty<double>();
        private double _maxValue = 100;
        public static readonly Color DefaultAccentColor = Color.FromRgb(90, 170, 255);
        private Color _accentColor = DefaultAccentColor;
        public Color AccentColor
        {
            get => _accentColor;
            set { _accentColor = value; Redraw(); }
        }

        // Optional second series: dashed, unfilled overlay line sharing the
        // primary series' scale. Existing single-series callers (Performance
        // tab's CPU/Memory/Disk) are unaffected since this defaults to null.
        private double[]? _secondSamples;
        private Color _secondColor = Colors.Orange;

        public void SetSecondSeries(double[]? samples, Color color)
        {
            _secondSamples = samples;
            _secondColor = color;
            Redraw();
        }

        public PerformanceGraph()
        {
            InitializeComponent();
            SizeChanged += (s, e) => Redraw();
        }

        public void SetHeader(string title, string value)
        {
            TitleText.Text = title;
            ValueText.Text = value;
        }

        public void SetHeaderVisible(bool visible)
        {
            HeaderGrid.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetData(double[] samples, double max)
        {
            _samples = samples;
            _maxValue = max > 0 ? max : 1;
            Redraw();
        }

        private void Redraw()
        {
            GraphCanvas.Children.Clear();
            double w = GraphCanvas.ActualWidth;
            double h = GraphCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // Gridlines at 25/50/75% — faint, drawn first so the plot sits on top.
            var gridBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
            foreach (var frac in new[] { 0.25, 0.5, 0.75 })
            {
                double y = h - (frac * h);
                var line = new Line { X1 = 0, Y1 = y, X2 = w, Y2 = y, Stroke = gridBrush, StrokeThickness = 1 };
                GraphCanvas.Children.Add(line);
            }

            if (_samples.Length < 2) return;

            double stepX = w / (_samples.Length - 1);
            var points = new PointCollection();
            for (int i = 0; i < _samples.Length; i++)
            {
                double x = i * stepX;
                double normalized = Math.Clamp(_samples[i] / _maxValue, 0, 1);
                double y = h - (normalized * h);
                points.Add(new Point(x, y));
            }

            // Filled area: smooth top curve, then straight edges down to the
            // baseline so only the data curve itself is smoothed, not the fill's
            // corners.
            var fillFigure = BuildSmoothFigure(points);
            fillFigure.Segments.Add(new LineSegment(new Point(points[points.Count - 1].X, h), true));
            fillFigure.Segments.Add(new LineSegment(new Point(points[0].X, h), true));
            fillFigure.IsClosed = true;
            var fillGeometry = new PathGeometry();
            fillGeometry.Figures.Add(fillFigure);

            var gradientBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(90, _accentColor.R, _accentColor.G, _accentColor.B), 0));
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, _accentColor.R, _accentColor.G, _accentColor.B), 1));

            var fillPath = new System.Windows.Shapes.Path { Data = fillGeometry, Fill = gradientBrush };
            GraphCanvas.Children.Add(fillPath);

            var strokeFigure = BuildSmoothFigure(points);
            var strokeGeometry = new PathGeometry();
            strokeGeometry.Figures.Add(strokeFigure);
            var strokePath = new System.Windows.Shapes.Path
            {
                Data = strokeGeometry,
                Stroke = new SolidColorBrush(_accentColor),
                StrokeThickness = 2.5,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            GraphCanvas.Children.Add(strokePath);

            if (_secondSamples != null && _secondSamples.Length >= 2)
            {
                double stepX2 = w / (_secondSamples.Length - 1);
                var points2 = new PointCollection();
                for (int i = 0; i < _secondSamples.Length; i++)
                {
                    double x = i * stepX2;
                    double normalized = Math.Clamp(_secondSamples[i] / _maxValue, 0, 1);
                    double y = h - (normalized * h);
                    points2.Add(new Point(x, y));
                }

                var dashedFigure = BuildSmoothFigure(points2);
                var dashedGeometry = new PathGeometry();
                dashedGeometry.Figures.Add(dashedFigure);
                var dashedPath = new System.Windows.Shapes.Path
                {
                    Data = dashedGeometry,
                    Stroke = new SolidColorBrush(_secondColor),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    StrokeLineJoin = PenLineJoin.Round
                };
                GraphCanvas.Children.Add(dashedPath);
            }
        }

        // Standard uniform Catmull-Rom-to-cubic-Bezier conversion: produces a
        // smooth curve passing through every sample point (unlike a fitted
        // approximation curve, which wouldn't touch the actual data values).
        // Well-established, widely-published formula — not a novel technique.
        private static PathFigure BuildSmoothFigure(PointCollection points)
        {
            var figure = new PathFigure { StartPoint = points[0], IsClosed = false };
            if (points.Count < 3)
            {
                for (int i = 1; i < points.Count; i++)
                    figure.Segments.Add(new LineSegment(points[i], true));
                return figure;
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                Point p0 = i == 0 ? points[i] : points[i - 1];
                Point p1 = points[i];
                Point p2 = points[i + 1];
                Point p3 = (i + 2 < points.Count) ? points[i + 2] : points[i + 1];

                var b1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
                var b2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);

                figure.Segments.Add(new BezierSegment(b1, b2, p2, true));
            }
            return figure;
        }
    }
}



