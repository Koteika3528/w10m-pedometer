using System;
using StepCounter.ViewModels;
using StepCounter.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StepCounter
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            Resuming += OnResuming;
        }

        /// <summary>Общая модель главного экрана (живёт всё время работы приложения).</summary>
        public static MainViewModel ViewModel { get; private set; }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                rootFrame.Navigated += OnNavigated;
                Window.Current.Content = rootFrame;

                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            }

            if (ViewModel == null)
            {
                ViewModel = new MainViewModel(Window.Current.Dispatcher);
            }

            if (!e.PrelaunchActivated)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                Window.Current.Activate();
            }
        }

        void OnNavigated(object sender, NavigationEventArgs e)
        {
            var frame = (Frame)sender;
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                frame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        // Аппаратная/программная кнопка «Назад» на телефоне.
        void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            var frame = Window.Current.Content as Frame;
            if (frame != null && frame.CanGoBack)
            {
                e.Handled = true;
                frame.GoBack();
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        void OnSuspending(object sender, SuspendingEventArgs e)
        {
            SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
            ViewModel?.Suspend();
            deferral.Complete();
        }

        void OnResuming(object sender, object e)
        {
            ViewModel?.Resume();
        }
    }
}
