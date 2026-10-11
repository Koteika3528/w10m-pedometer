using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using StepCounter.Services;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace StepCounter.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        const int HistoryDays = 7;

        // Грубая оценка расхода энергии при ходьбе: ккал на кг веса на км.
        const double KcalPerKgPerKm = 0.5;

        readonly CoreDispatcher _dispatcher;
        readonly DispatcherTimer _dayTimer;

        IStepSource _source;
        bool _initialized;
        bool _isRefreshing;
        DateTime _shownDay;
        int _todaySteps;

        string _todayStepsText = "0";
        string _goalText;
        double _progressValue;
        string _distanceText;
        string _caloriesText;
        string _sourceText = "Поиск датчиков…";
        string _statusText;
        bool _isAccessHintVisible;

        public MainViewModel(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _dayTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _dayTimer.Tick += OnDayTimerTick;
            UpdateTodayTexts();
        }

        public ObservableCollection<DayStepsItem> History { get; } = new ObservableCollection<DayStepsItem>();

        public string TodayStepsText
        {
            get { return _todayStepsText; }
            private set { SetProperty(ref _todayStepsText, value); }
        }

        public string GoalText
        {
            get { return _goalText; }
            private set { SetProperty(ref _goalText, value); }
        }

        public double ProgressValue
        {
            get { return _progressValue; }
            private set { SetProperty(ref _progressValue, value); }
        }

        public string DistanceText
        {
            get { return _distanceText; }
            private set { SetProperty(ref _distanceText, value); }
        }

        public string CaloriesText
        {
            get { return _caloriesText; }
            private set { SetProperty(ref _caloriesText, value); }
        }

        public string SourceText
        {
            get { return _sourceText; }
            private set { SetProperty(ref _sourceText, value); }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value); }
        }

        public bool IsAccessHintVisible
        {
            get { return _isAccessHintVisible; }
            private set { SetProperty(ref _isAccessHintVisible, value); }
        }

        /// <summary>Выбирает источник шагов и загружает данные. Вызывается из UI-потока.</summary>
        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            var hardware = new HardwarePedometerSource();
            if (await hardware.InitializeAsync())
            {
                _source = hardware;
            }
            else
            {
                IsAccessHintVisible = hardware.IsAccessDenied;
                var accelerometer = new AccelerometerStepSource(new StepHistoryStore());
                if (await accelerometer.InitializeAsync())
                {
                    _source = accelerometer;
                }
            }

            if (_source == null)
            {
                SourceText = "Датчики не найдены";
                StatusText = "На этом устройстве нет ни шагомера, ни акселерометра.";
                return;
            }

            SourceText = "Источник: " + _source.Name;
            _source.TodayStepsChanged += OnTodayStepsChanged;
            _source.Start();
            _dayTimer.Start();
            await RefreshAsync();
        }

        /// <summary>Перечитывает историю и сегодняшние шаги. Вызывается из UI-потока.</summary>
        public async Task RefreshAsync()
        {
            if (_source == null || _isRefreshing)
            {
                return;
            }

            IReadOnlyList<DailySteps> history;
            _isRefreshing = true;
            try
            {
                history = await _source.GetHistoryAsync(HistoryDays);
            }
            catch (Exception ex)
            {
                StatusText = "Не удалось прочитать данные: " + ex.Message;
                return;
            }
            finally
            {
                _isRefreshing = false;
            }

            StatusText = null;
            _shownDay = DateTime.Today;

            int goal = AppSettings.DailyGoal;
            History.Clear();
            // Новые дни сверху.
            for (int i = history.Count - 1; i >= 0; i--)
            {
                History.Add(new DayStepsItem(history[i].Date, history[i].Steps, goal));
            }

            SetTodaySteps(history.Count > 0 ? history[history.Count - 1].Steps : 0);
        }

        /// <summary>Пересчитать отображение после изменения настроек.</summary>
        public void ApplySettings()
        {
            int goal = AppSettings.DailyGoal;
            foreach (DayStepsItem item in History)
            {
                item.Goal = goal;
            }
            UpdateTodayTexts();
        }

        /// <summary>Приложение приостанавливается. Можно вызывать из любого потока.</summary>
        public void Suspend()
        {
            _source?.Stop();
        }

        /// <summary>Приложение возобновлено. Можно вызывать из любого потока.</summary>
        public void Resume()
        {
            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (_source == null)
                {
                    return;
                }
                _source.Start();
                await RefreshAsync();
            });
        }

        void OnTodayStepsChanged(object sender, int steps)
        {
            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (DateTime.Today != _shownDay)
                {
                    // Наступил новый день — список истории устарел.
                    var ignoredRefresh = RefreshAsync();
                    return;
                }
                SetTodaySteps(steps);
            });
        }

        async void OnDayTimerTick(object sender, object e)
        {
            if (DateTime.Today != _shownDay)
            {
                await RefreshAsync();
            }
        }

        void SetTodaySteps(int steps)
        {
            _todaySteps = steps;
            if (History.Count > 0 && History[0].Date == DateTime.Today)
            {
                History[0].Steps = steps;
            }
            UpdateTodayTexts();
        }

        void UpdateTodayTexts()
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            int goal = AppSettings.DailyGoal;
            double km = _todaySteps * AppSettings.StepLengthCm / 100000.0;
            double kcal = km * AppSettings.WeightKg * KcalPerKgPerKm;

            TodayStepsText = _todaySteps.ToString("N0", culture);
            GoalText = string.Format(culture, "из {0:N0} шагов", goal);
            ProgressValue = goal > 0 ? Math.Min(100.0, 100.0 * _todaySteps / goal) : 0;
            DistanceText = string.Format(culture, "{0:0.00} км", km);
            CaloriesText = string.Format(culture, "{0:0} ккал", kcal);
        }
    }
}
