using StepCounter.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StepCounter.Views
{
    public sealed partial class StatisticsPage : Page
    {
        bool _isUpdatingButtons;

        public StatisticsPage()
        {
            ViewModel = new StatisticsViewModel(App.ViewModel.Database);
            InitializeComponent();
        }

        public StatisticsViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _isUpdatingButtons = true;
            WeekButton.IsChecked = ViewModel.Period == StatisticsPeriod.Week;
            MonthButton.IsChecked = ViewModel.Period == StatisticsPeriod.Month;
            YearButton.IsChecked = ViewModel.Period == StatisticsPeriod.Year;
            _isUpdatingButtons = false;

            await ViewModel.LoadAsync();
        }

        async void OnPeriodChecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingButtons)
            {
                return;
            }

            StatisticsPeriod period = sender == MonthButton ? StatisticsPeriod.Month
                : sender == YearButton ? StatisticsPeriod.Year
                : StatisticsPeriod.Week;
            await ViewModel.SetPeriodAsync(period);
        }

        async void OnPreviousClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.PreviousAsync();
        }

        async void OnNextClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.NextAsync();
        }
    }
}
