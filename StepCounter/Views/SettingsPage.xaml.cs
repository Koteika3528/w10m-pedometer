using System.Globalization;
using StepCounter.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StepCounter.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            GoalBox.Text = AppSettings.DailyGoal.ToString(CultureInfo.InvariantCulture);
            StepLengthBox.Text = AppSettings.StepLengthCm.ToString(CultureInfo.InvariantCulture);
            WeightBox.Text = AppSettings.WeightKg.ToString(CultureInfo.InvariantCulture);
        }

        void OnSaveClick(object sender, RoutedEventArgs e)
        {
            int goal, stepLength, weight;
            if (!TryParse(GoalBox.Text, 100, 100000, out goal))
            {
                ShowError("Цель — число от 100 до 100 000.");
                return;
            }
            if (!TryParse(StepLengthBox.Text, 20, 200, out stepLength))
            {
                ShowError("Длина шага — число от 20 до 200 см.");
                return;
            }
            if (!TryParse(WeightBox.Text, 20, 300, out weight))
            {
                ShowError("Вес — число от 20 до 300 кг.");
                return;
            }

            AppSettings.DailyGoal = goal;
            AppSettings.StepLengthCm = stepLength;
            AppSettings.WeightKg = weight;

            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        static bool TryParse(string text, int min, int max, out int value)
        {
            string digits = (text ?? string.Empty).Replace(" ", string.Empty).Replace(" ", string.Empty);
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                && value >= min && value <= max;
        }
    }
}
