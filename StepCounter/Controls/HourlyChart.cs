using System;
using System.Collections.Generic;
using System.Linq;
using StepCounter.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace StepCounter.Controls
{
    /// <summary>
    /// Столбчатая диаграмма активности по часам: для каждого часа синий столбец — шаги,
    /// оранжевый — ккал. Каждая величина масштабируется по своему максимуму за день.
    /// </summary>
    public sealed class HourlyChart : UserControl
    {
        public static readonly DependencyProperty BarsProperty = DependencyProperty.Register(
            nameof(Bars), typeof(object), typeof(HourlyChart),
            new PropertyMetadata(null, (d, e) => ((HourlyChart)d).Rebuild()));

        const int Hours = 24;

        readonly Grid _plot = new Grid();
        readonly Rectangle[] _stepBars = new Rectangle[Hours];
        readonly Rectangle[] _kcalBars = new Rectangle[Hours];
        double[] _stepFractions = new double[Hours];
        double[] _kcalFractions = new double[Hours];

        public HourlyChart()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Brush stepsBrush = (Brush)Application.Current.Resources["StepsBrush"];
            Brush kcalBrush = (Brush)Application.Current.Resources["KcalBrush"];
            Brush axisBrush = (Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"];

            for (int hour = 0; hour < Hours; hour++)
            {
                _plot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var cell = new Grid { Margin = new Thickness(1, 0, 1, 0) };
                cell.ColumnDefinitions.Add(new ColumnDefinition());
                cell.ColumnDefinitions.Add(new ColumnDefinition());

                _stepBars[hour] = CreateBar(stepsBrush, 0);
                _kcalBars[hour] = CreateBar(kcalBrush, 1);
                cell.Children.Add(_stepBars[hour]);
                cell.Children.Add(_kcalBars[hour]);

                Grid.SetColumn(cell, hour);
                _plot.Children.Add(cell);
            }

            var axis = new Rectangle { Height = 1, Fill = axisBrush, VerticalAlignment = VerticalAlignment.Bottom };
            Grid.SetColumnSpan(axis, Hours);
            _plot.Children.Add(axis);
            _plot.SizeChanged += (s, e) => UpdateHeights();
            root.Children.Add(_plot);

            var labels = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            for (int hour = 0; hour < Hours; hour++)
            {
                labels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            for (int hour = 0; hour < Hours; hour += 6)
            {
                var label = new TextBlock
                {
                    Text = hour.ToString("00") + ":00",
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
                };
                Grid.SetColumn(label, hour);
                Grid.SetColumnSpan(label, 6);
                labels.Children.Add(label);
            }
            Grid.SetRow(labels, 1);
            root.Children.Add(labels);

            Content = root;
        }

        /// <summary>Список <see cref="HourBar"/> (24 элемента).</summary>
        public object Bars
        {
            get { return GetValue(BarsProperty); }
            set { SetValue(BarsProperty, value); }
        }

        static Rectangle CreateBar(Brush brush, int column)
        {
            var bar = new Rectangle
            {
                Fill = brush,
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 0,
                RadiusX = 1,
                RadiusY = 1,
            };
            Grid.SetColumn(bar, column);
            return bar;
        }

        void Rebuild()
        {
            var bars = (Bars as IEnumerable<HourBar>)?.ToList() ?? new List<HourBar>();
            int maxSteps = bars.Count > 0 ? bars.Max(b => b.Steps) : 0;
            double maxKcal = bars.Count > 0 ? bars.Max(b => b.Kcal) : 0;

            _stepFractions = new double[Hours];
            _kcalFractions = new double[Hours];
            foreach (HourBar bar in bars)
            {
                if (bar.Hour < 0 || bar.Hour >= Hours)
                {
                    continue;
                }
                _stepFractions[bar.Hour] = maxSteps > 0 ? (double)bar.Steps / maxSteps : 0;
                _kcalFractions[bar.Hour] = maxKcal > 0 ? bar.Kcal / maxKcal : 0;
            }
            UpdateHeights();
        }

        void UpdateHeights()
        {
            double height = _plot.ActualHeight;
            if (height <= 0)
            {
                return;
            }

            for (int hour = 0; hour < Hours; hour++)
            {
                _stepBars[hour].Height = BarHeight(_stepFractions[hour], height);
                _kcalBars[hour].Height = BarHeight(_kcalFractions[hour], height);
            }
        }

        static double BarHeight(double fraction, double height)
        {
            // Маленькие, но ненулевые значения всё равно видны.
            return fraction > 0 ? Math.Max(2, fraction * height) : 0;
        }
    }
}
