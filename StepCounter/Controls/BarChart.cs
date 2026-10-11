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
    /// Столбчатая диаграмма: для каждого столбца синий — шаги, оранжевый — ккал.
    /// Каждая величина масштабируется по своему максимуму.
    /// Подпись столбца растягивается до следующей подписи.
    /// </summary>
    public sealed class BarChart : UserControl
    {
        public static readonly DependencyProperty BarsProperty = DependencyProperty.Register(
            nameof(Bars), typeof(object), typeof(BarChart),
            new PropertyMetadata(null, (d, e) => ((BarChart)d).Rebuild()));

        readonly Grid _plot = new Grid();
        readonly Grid _labels = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        readonly Brush _stepsBrush;
        readonly Brush _kcalBrush;
        readonly Brush _axisBrush;
        readonly Brush _labelBrush;

        Rectangle[] _stepBars = new Rectangle[0];
        Rectangle[] _kcalBars = new Rectangle[0];
        double[] _stepFractions = new double[0];
        double[] _kcalFractions = new double[0];

        public BarChart()
        {
            _stepsBrush = (Brush)Application.Current.Resources["StepsBrush"];
            _kcalBrush = (Brush)Application.Current.Resources["KcalBrush"];
            _axisBrush = (Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"];
            _labelBrush = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"];

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _plot.SizeChanged += (s, e) => UpdateHeights();
            root.Children.Add(_plot);
            Grid.SetRow(_labels, 1);
            root.Children.Add(_labels);
            Content = root;
        }

        /// <summary>Список <see cref="ChartBar"/>.</summary>
        public object Bars
        {
            get { return GetValue(BarsProperty); }
            set { SetValue(BarsProperty, value); }
        }

        void Rebuild()
        {
            List<ChartBar> bars = (Bars as IEnumerable<ChartBar>)?.ToList() ?? new List<ChartBar>();
            int count = bars.Count;

            _plot.Children.Clear();
            _plot.ColumnDefinitions.Clear();
            _labels.Children.Clear();
            _labels.ColumnDefinitions.Clear();

            _stepBars = new Rectangle[count];
            _kcalBars = new Rectangle[count];
            _stepFractions = new double[count];
            _kcalFractions = new double[count];

            double maxSteps = count > 0 ? bars.Max(b => b.Steps) : 0;
            double maxKcal = count > 0 ? bars.Max(b => b.Kcal) : 0;
            // Чем больше столбцов, тем меньше промежутки между ними.
            double gap = count > 20 ? 0.5 : 1.5;

            for (int i = 0; i < count; i++)
            {
                _plot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                _labels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                _stepFractions[i] = maxSteps > 0 ? bars[i].Steps / maxSteps : 0;
                _kcalFractions[i] = maxKcal > 0 ? bars[i].Kcal / maxKcal : 0;

                var cell = new Grid { Margin = new Thickness(gap, 0, gap, 0) };
                cell.ColumnDefinitions.Add(new ColumnDefinition());
                cell.ColumnDefinitions.Add(new ColumnDefinition());
                _stepBars[i] = CreateBar(_stepsBrush, 0);
                _kcalBars[i] = CreateBar(_kcalBrush, 1);
                cell.Children.Add(_stepBars[i]);
                cell.Children.Add(_kcalBars[i]);
                Grid.SetColumn(cell, i);
                _plot.Children.Add(cell);

                if (!string.IsNullOrEmpty(bars[i].Label))
                {
                    int span = 1;
                    while (i + span < count && string.IsNullOrEmpty(bars[i + span].Label))
                    {
                        span++;
                    }
                    var label = new TextBlock
                    {
                        Text = bars[i].Label,
                        FontSize = 11,
                        Foreground = _labelBrush,
                        Margin = new Thickness(gap, 0, 0, 0),
                    };
                    Grid.SetColumn(label, i);
                    Grid.SetColumnSpan(label, span);
                    _labels.Children.Add(label);
                }
            }

            var axis = new Rectangle { Height = 1, Fill = _axisBrush, VerticalAlignment = VerticalAlignment.Bottom };
            Grid.SetColumnSpan(axis, Math.Max(1, count));
            _plot.Children.Add(axis);

            UpdateHeights();
        }

        static Rectangle CreateBar(Brush brush, int column)
        {
            var bar = new Rectangle
            {
                Fill = brush,
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 0,
            };
            Grid.SetColumn(bar, column);
            return bar;
        }

        void UpdateHeights()
        {
            double height = _plot.ActualHeight;
            if (height <= 0)
            {
                return;
            }

            for (int i = 0; i < _stepBars.Length; i++)
            {
                _stepBars[i].Height = BarHeight(_stepFractions[i], height);
                _kcalBars[i].Height = BarHeight(_kcalFractions[i], height);
            }
        }

        static double BarHeight(double fraction, double height)
        {
            // Маленькие, но ненулевые значения всё равно видны.
            return fraction > 0 ? Math.Max(2, fraction * height) : 0;
        }
    }
}
