using StepCounter.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StepCounter.Views
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        public MainViewModel ViewModel => App.ViewModel;

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.Back)
            {
                // Вернулись из настроек — цель или длина шага могли измениться.
                ViewModel.ApplySettings();
                return;
            }

            await ViewModel.InitializeAsync();
        }

        async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshAsync();
        }

        void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }
    }
}
