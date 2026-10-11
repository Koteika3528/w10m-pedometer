using System;
using System.Globalization;
using StepCounter.Services;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace StepCounter.Controls
{
    /// <summary>Праздничный экран с конфетти при выполнении дневной нормы.</summary>
    public sealed partial class CelebrationOverlay : UserControl
    {
        const int ConfettiCount = 70;
        static readonly TimeSpan AutoHideAfter = TimeSpan.FromSeconds(6);

        static readonly Color[] ConfettiColors =
        {
            Color.FromArgb(255, 0xFF, 0x98, 0x00),
            Color.FromArgb(255, 0x1E, 0x88, 0xE5),
            Color.FromArgb(255, 0x43, 0xA0, 0x47),
            Color.FromArgb(255, 0xE5, 0x39, 0x35),
            Color.FromArgb(255, 0xFD, 0xD8, 0x35),
            Color.FromArgb(255, 0x8E, 0x24, 0xAA),
        };

        readonly Random _random = new Random();
        readonly DispatcherTimer _hideTimer;
        Storyboard _storyboard;

        public CelebrationOverlay()
        {
            InitializeComponent();
            _hideTimer = new DispatcherTimer { Interval = AutoHideAfter };
            _hideTimer.Tick += (s, e) => Hide();
        }

        public void Show(int steps)
        {
            CheerText.Text = Motivation.Pick(Motivation.Celebration, DateTime.Today.DayOfYear);
            StepsText.Text = string.Format(
                CultureInfo.CurrentCulture, "Сегодня уже {0:N0} {1}.",
                steps, Motivation.Plural(steps, "шаг", "шага", "шагов"));

            Visibility = Visibility.Visible;
            UpdateLayout();
            StartAnimation();
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        public void Hide()
        {
            _hideTimer.Stop();
            _storyboard?.Stop();
            _storyboard = null;
            ConfettiCanvas.Children.Clear();
            Visibility = Visibility.Collapsed;
        }

        void OnTapped(object sender, TappedRoutedEventArgs e)
        {
            Hide();
        }

        void StartAnimation()
        {
            _storyboard?.Stop();
            ConfettiCanvas.Children.Clear();

            double width = Root.ActualWidth > 0 ? Root.ActualWidth : 400;
            double height = Root.ActualHeight > 0 ? Root.ActualHeight : 700;
            var storyboard = new Storyboard();

            for (int i = 0; i < ConfettiCount; i++)
            {
                var transform = new CompositeTransform();
                var piece = new Rectangle
                {
                    Width = 6 + _random.Next(6),
                    Height = 10 + _random.Next(8),
                    Fill = new SolidColorBrush(ConfettiColors[_random.Next(ConfettiColors.Length)]),
                    RenderTransform = transform,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                };
                Canvas.SetLeft(piece, _random.NextDouble() * width);
                Canvas.SetTop(piece, -30);
                ConfettiCanvas.Children.Add(piece);

                var duration = TimeSpan.FromSeconds(2.5 + _random.NextDouble() * 2.5);
                var delay = TimeSpan.FromSeconds(_random.NextDouble() * 1.5);
                double drift = (_random.NextDouble() - 0.5) * 160;

                storyboard.Children.Add(Animate(piece, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)",
                    0, height + 60, duration, delay, new QuadraticEase { EasingMode = EasingMode.EaseIn }));
                storyboard.Children.Add(Animate(piece, "(UIElement.RenderTransform).(CompositeTransform.TranslateX)",
                    0, drift, duration, delay, new SineEase { EasingMode = EasingMode.EaseInOut }));
                storyboard.Children.Add(Animate(piece, "(UIElement.RenderTransform).(CompositeTransform.Rotation)",
                    0, (_random.NextDouble() - 0.5) * 1080, duration, delay, null));
            }

            // Карточка с сообщением «выпрыгивает».
            var bounce = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 };
            storyboard.Children.Add(Animate(MessageCard, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)",
                0.3, 1, TimeSpan.FromMilliseconds(450), TimeSpan.Zero, bounce));
            storyboard.Children.Add(Animate(MessageCard, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)",
                0.3, 1, TimeSpan.FromMilliseconds(450), TimeSpan.Zero, bounce));
            storyboard.Children.Add(Animate(MessageCard, "Opacity",
                0, 1, TimeSpan.FromMilliseconds(250), TimeSpan.Zero, null));

            _storyboard = storyboard;
            storyboard.Begin();
        }

        static DoubleAnimation Animate(DependencyObject target, string path, double from, double to,
            TimeSpan duration, TimeSpan delay, EasingFunctionBase easing)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(duration),
                BeginTime = delay,
                EasingFunction = easing,
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, path);
            return animation;
        }
    }
}
