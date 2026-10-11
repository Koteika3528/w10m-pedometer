using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StepCounter.Data;
using StepCounter.Services;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace StepCounter.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        /// <summary>Сколько дней назад можно пролистать.</summary>
        const int DaysBack = 365;

        readonly CoreDispatcher _dispatcher;
        readonly DispatcherTimer _clockTimer;
        readonly DispatcherTimer _syncTimer;
        readonly BackgroundKeeper _backgroundKeeper = new BackgroundKeeper();

        StepDatabase _database;
        IStepSource _source;
        bool _initialized;
        bool _isSyncing;
        DateTime _today;
        int _selectedIndex;

        string _sourceText = "Поиск датчиков…";
        string _statusText;
        bool _isAccessHintVisible;
        string _backgroundText;

        public MainViewModel(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _clockTimer.Tick += OnClockTick;

            // Шаги приходят часто — синхронизируемся с базой не чаще раза в несколько секунд.
            _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _syncTimer.Tick += OnSyncTimerTick;

            _backgroundKeeper.Revoked += OnBackgroundRevoked;

            _today = DateTime.Today;
            for (int i = DaysBack - 1; i >= 0; i--)
            {
                Days.Add(new DayViewModel(_today.AddDays(-i)));
            }
            _selectedIndex = Days.Count - 1;
        }

        /// <summary>Дни от самого старого к сегодняшнему (сегодня — последний).</summary>
        public ObservableCollection<DayViewModel> Days { get; } = new ObservableCollection<DayViewModel>();

        /// <summary>База истории (null, пока не открыта).</summary>
        public StepDatabase Database => _database;

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value < 0 || !SetProperty(ref _selectedIndex, value))
                {
                    return;
                }
                var ignored = LoadAroundSelectedAsync();
            }
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

        /// <summary>Состояние работы в фоне (пусто, если режим выключен).</summary>
        public string BackgroundText
        {
            get { return _backgroundText; }
            private set { SetProperty(ref _backgroundText, value); }
        }

        /// <summary>Сегодня выполнена норма шагов (аргумент — шаги). Вызывается в UI-потоке один раз за день.</summary>
        public event EventHandler<int> GoalReached;

        DayViewModel TodayItem => Days[Days.Count - 1];

        /// <summary>Открывает базу, выбирает источник шагов и загружает данные. Вызывается из UI-потока.</summary>
        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            try
            {
                _database = await Task.Run(() => StepDatabase.Open());
            }
            catch (Exception ex)
            {
                SourceText = "Ошибка базы данных";
                StatusText = ex.Message;
                return;
            }

            var hardware = new HardwarePedometerSource(_database);
            if (await hardware.InitializeAsync())
            {
                _source = hardware;
            }
            else
            {
                IsAccessHintVisible = hardware.IsAccessDenied;
                var accelerometer = new AccelerometerStepSource(_database);
                if (await accelerometer.InitializeAsync())
                {
                    _source = accelerometer;
                }
            }

            if (_source == null)
            {
                SourceText = "Датчики не найдены";
                StatusText = "На этом устройстве нет ни шагомера, ни акселерометра. Доступна только история.";
            }
            else
            {
                SourceText = _source.Name;
                _source.StepsDetected += OnStepsDetected;
                _source.Start();
            }

            _clockTimer.Start();
            await UpdateBackgroundModeAsync();
            await SyncAndReloadAsync();
        }

        /// <summary>
        /// Включает или выключает работу в фоне. Вызывается из UI-потока, пока приложение на экране.
        /// Возвращает null при успехе или текст ошибки (тогда настройка выключается).
        /// </summary>
        public async Task<string> SetKeepRunningInBackgroundAsync(bool enabled)
        {
            AppSettings.KeepRunningInBackground = enabled;
            string error = await UpdateBackgroundModeAsync();
            if (error != null)
            {
                AppSettings.KeepRunningInBackground = false;
            }
            return error;
        }

        async Task<string> UpdateBackgroundModeAsync()
        {
            if (!AppSettings.KeepRunningInBackground)
            {
                _backgroundKeeper.Stop();
                BackgroundText = null;
                return null;
            }

            string error = await _backgroundKeeper.StartAsync();
            BackgroundText = error ?? "Работает в фоне, в том числе при выключенном экране";
            return error;
        }

        void OnBackgroundRevoked(object sender, ExtendedExecutionRevokedReason reason)
        {
            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                BackgroundText = reason == ExtendedExecutionRevokedReason.SystemPolicy
                    ? "Система остановила работу в фоне (экономия заряда или нехватка памяти). Включится снова при открытии."
                    : null;
            });
        }

        /// <summary>Забирает новые шаги из датчика и обновляет экран. Вызывается из UI-потока.</summary>
        public async Task SyncAndReloadAsync()
        {
            if (_database == null || _isSyncing)
            {
                return;
            }

            _isSyncing = true;
            try
            {
                if (_source != null)
                {
                    await _source.SyncAsync();
                    StatusText = null;
                }
            }
            catch (Exception ex)
            {
                StatusText = "Не удалось прочитать данные датчика: " + ex.Message;
            }
            finally
            {
                _isSyncing = false;
            }

            await LoadDayAsync(TodayItem);
            await LoadAroundSelectedAsync();
            CheckGoal();
        }

        public void GoToToday()
        {
            Select(Days.Count - 1);
        }

        /// <summary>Выбирает день, даже если индекс в модели уже такой (FlipView мог разойтись с моделью).</summary>
        void Select(int index)
        {
            _selectedIndex = -1;
            SelectedIndex = index;
        }

        /// <summary>Пересчитать отображение после изменения настроек.</summary>
        public async Task ApplySettingsAsync()
        {
            foreach (DayViewModel day in Days)
            {
                day.IsLoaded = false;
            }
            await LoadDayAsync(TodayItem);
            await LoadAroundSelectedAsync();
            CheckGoal();
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
                _source?.Start();
                CheckDateChanged();
                // Если система отозвала работу в фоне — запрашиваем снова, пока приложение на экране.
                await UpdateBackgroundModeAsync();
                await SyncAndReloadAsync();
            });
        }

        void OnStepsDetected(object sender, EventArgs e)
        {
            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!_syncTimer.IsEnabled)
                {
                    _syncTimer.Start();
                }
            });
        }

        async void OnSyncTimerTick(object sender, object e)
        {
            _syncTimer.Stop();
            CheckDateChanged();
            await SyncAndReloadAsync();
        }

        async void OnClockTick(object sender, object e)
        {
            // Раз в минуту: смена дня, запись завершённых отрезков, обновление «сегодня».
            CheckDateChanged();
            await SyncAndReloadAsync();
        }

        /// <summary>После полуночи добавляет новый день в конец списка.</summary>
        bool CheckDateChanged()
        {
            DateTime today = DateTime.Today;
            if (today == _today)
            {
                return false;
            }

            bool wasOnToday = _selectedIndex == Days.Count - 1;
            DateTime selectedDate = _selectedIndex >= 0 && _selectedIndex < Days.Count
                ? Days[_selectedIndex].Date
                : _today;

            // FlipView сам сдвигает выбранный индекс при удалении элементов,
            // поэтому после изменений выбираем день заново по дате.
            for (DateTime date = _today.AddDays(1); date <= today; date = date.AddDays(1))
            {
                Days.Add(new DayViewModel(date));
                Days.RemoveAt(0);
            }
            _today = today;

            // Подписи «сегодня»/«вчера» поменялись — перечитать всё.
            foreach (DayViewModel day in Days)
            {
                day.IsLoaded = false;
            }

            int index = Days.Count - 1;
            if (!wasOnToday)
            {
                for (int i = 0; i < Days.Count; i++)
                {
                    if (Days[i].Date == selectedDate)
                    {
                        index = i;
                        break;
                    }
                }
            }
            Select(index);
            return true;
        }

        async Task LoadAroundSelectedAsync()
        {
            int index = _selectedIndex;
            // Загружаем и соседние дни, чтобы при свайпе они были готовы.
            for (int i = index - 1; i <= index + 1; i++)
            {
                if (i >= 0 && i < Days.Count && !Days[i].IsLoaded)
                {
                    await LoadDayAsync(Days[i]);
                }
            }
        }

        async Task LoadDayAsync(DayViewModel day)
        {
            if (_database == null)
            {
                return;
            }

            int key = day.Key;
            DaySummary summary = null;
            List<ActivitySegment> segments = null;
            try
            {
                await Task.Run(() =>
                {
                    summary = _database.GetDay(key);
                    segments = _database.GetSegments(key);
                });
            }
            catch (Exception ex)
            {
                StatusText = "Ошибка чтения базы: " + ex.Message;
                return;
            }

            int pending = day.IsToday && _source != null ? _source.PendingStepsToday : 0;
            day.Apply(summary, segments, pending);
        }

        void CheckGoal()
        {
            DayViewModel today = TodayItem;
            int goal = AppSettings.DailyGoal;
            if (!today.IsLoaded || today.TotalSteps < goal)
            {
                return;
            }

            if (AppSettings.LastCelebratedDay == today.Key)
            {
                return;
            }

            AppSettings.LastCelebratedDay = today.Key;
            GoalReached?.Invoke(this, today.TotalSteps);
        }
    }
}
