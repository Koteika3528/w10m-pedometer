using StepCounter.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StepCounter.Views
{
    public sealed partial class MainPage : Page
    {
        bool _flipViewShown;

        public MainPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel.GoalReached += OnGoalReached;
        }

        public MainViewModel ViewModel => App.ViewModel;

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.Back)
            {
                // Вернулись из настроек — цель, длина шага или вес могли измениться.
                await ViewModel.ApplySettingsAsync();
                return;
            }

            await ViewModel.InitializeAsync();
        }

        void OnFlipViewLoaded(object sender, RoutedEventArgs e)
        {
            // При загрузке FlipView выбирает первый элемент — при первом показе открываем «сегодня».
            if (!_flipViewShown)
            {
                _flipViewShown = true;
                ViewModel.GoToToday();
            }
        }

        void OnGoalReached(object sender, int steps)
        {
            Celebration.Show(steps);
        }

        void OnTodayClick(object sender, RoutedEventArgs e)
        {
            ViewModel.GoToToday();
        }

        async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.SyncAndReloadAsync();
        }

        void OnStatisticsClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(StatisticsPage));
        }

        void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }
    }
}
