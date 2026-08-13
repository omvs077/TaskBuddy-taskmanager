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
            if (w <= 0 || h <= 0 || _samples.Length < 2) return;

            double stepX = w / (_samples.Length - 1);
            var points = new PointCollection();

            for (int i = 0; i < _samples.Length; i++)
            {
                double x = i * stepX;
                double normalized = Math.Clamp(_samples[i] / _maxValue, 0, 1);
                double y = h - (normalized * h);
                points.Add(new Point(x, y));
            }

            var fillPoints = new PointCollection(points);
            fillPoints.Add(new Point(points[points.Count - 1].X, h));
            fillPoints.Add(new Point(points[0].X, h));

            var polygon = new Polygon
            {
                Points = fillPoints,
                Fill = new SolidColorBrush(Color.FromArgb(60, 90, 170, 255))
            };
            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromRgb(90, 170, 255)),
                StrokeThickness = 1.5
            };

            GraphCanvas.Children.Add(polygon);
            GraphCanvas.Children.Add(polyline);
        }
    }
}
