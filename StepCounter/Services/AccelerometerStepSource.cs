using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Sensors;

namespace StepCounter.Services
{
    /// <summary>
    /// Запасной источник для телефонов без встроенного шагомера:
    /// шаги считаются по акселерометру, пока приложение открыто.
    /// </summary>
    public sealed class AccelerometerStepSource : IStepSource
    {
        const uint DesiredReportIntervalMs = 20;
        const int SaveEverySteps = 20;

        readonly StepHistoryStore _store;
        readonly StepDetector _detector = new StepDetector();
        readonly object _lock = new object();

        Accelerometer _accelerometer;
        bool _running;
        DateTime _day;
        int _todaySteps;
        int _unsavedSteps;

        public AccelerometerStepSource(StepHistoryStore store)
        {
            _store = store;
        }

        public string Name => "Акселерометр (считает, только пока приложение открыто)";

        public event EventHandler<int> TodayStepsChanged;

        public Task<bool> InitializeAsync()
        {
            _accelerometer = Accelerometer.GetDefault();
            if (_accelerometer == null)
            {
                return Task.FromResult(false);
            }

            lock (_lock)
            {
                _day = DateTime.Today;
                _todaySteps = _store.Get(_day);
            }
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<DailySteps>> GetHistoryAsync(int days)
        {
            var result = new List<DailySteps>(days);
            lock (_lock)
            {
                RollDayIfNeeded();
                Save();

                DateTime today = DateTime.Today;
                for (int i = days - 1; i > 0; i--)
                {
                    DateTime date = today.AddDays(-i);
                    result.Add(new DailySteps(date, _store.Get(date)));
                }
                result.Add(new DailySteps(today, _todaySteps));
            }
            return Task.FromResult<IReadOnlyList<DailySteps>>(result);
        }

        public void Start()
        {
            if (_accelerometer == null || _running)
            {
                return;
            }

            _running = true;
            _detector.Reset();
            _accelerometer.ReportInterval = Math.Max(_accelerometer.MinimumReportInterval, DesiredReportIntervalMs);
            _accelerometer.ReadingChanged += OnReadingChanged;
        }

        public void Stop()
        {
            if (_accelerometer == null || !_running)
            {
                return;
            }

            _running = false;
            _accelerometer.ReadingChanged -= OnReadingChanged;
            // 0 — вернуть интервал по умолчанию, чтобы не тратить батарею.
            _accelerometer.ReportInterval = 0;

            lock (_lock)
            {
                Save();
            }
        }

        void OnReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;
            int newSteps = _detector.AddSample(
                reading.AccelerationX, reading.AccelerationY, reading.AccelerationZ, reading.Timestamp);
            if (newSteps == 0)
            {
                return;
            }

            int total;
            lock (_lock)
            {
                RollDayIfNeeded();
                _todaySteps += newSteps;
                _unsavedSteps += newSteps;
                if (_unsavedSteps >= SaveEverySteps)
                {
                    Save();
                }
                total = _todaySteps;
            }

            TodayStepsChanged?.Invoke(this, total);
        }

        // Вызывать под _lock.
        void RollDayIfNeeded()
        {
            DateTime today = DateTime.Today;
            if (today == _day)
            {
                return;
            }

            Save();
            _day = today;
            _todaySteps = _store.Get(today);
        }

        // Вызывать под _lock.
        void Save()
        {
            if (_unsavedSteps == 0)
            {
                return;
            }

            _store.Set(_day, _todaySteps);
            _unsavedSteps = 0;
        }
    }
}
