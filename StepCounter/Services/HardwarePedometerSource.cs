using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StepCounter.Data;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation.Metadata;
using Windows.Storage;

namespace StepCounter.Services
{
    /// <summary>
    /// Встроенный шагомер (Windows.Devices.Sensors.Pedometer). Система считает шаги
    /// в фоне сама; приложение забирает накопленную системную историю
    /// и переносит её в свою базу, чтобы хранить данные дольше.
    /// </summary>
    public sealed class HardwarePedometerSource : IStepSource
    {
        // Класс устройств «датчик активности»: по нему проверяется разрешение «Движение».
        static readonly Guid ActivitySensorClassId = new Guid("9D9E0118-1807-4F2E-96E4-2CE57142E196");

        // При самом первом запуске забираем историю за этот период (сколько система хранит).
        const int FirstSyncDays = 30;

        const string StateKey = "PedometerState";

        static readonly PedometerStepKind[] StepKinds =
        {
            PedometerStepKind.Unknown, PedometerStepKind.Walking, PedometerStepKind.Running,
        };

        readonly StepDatabase _database;
        readonly object _lock = new object();

        // Последнее учтённое показание для каждого вида шагов (ходьба, бег, ...).
        readonly Dictionary<PedometerStepKind, Baseline> _baselines = new Dictionary<PedometerStepKind, Baseline>();
        DateTimeOffset _lastTimestamp = DateTimeOffset.MinValue;

        Pedometer _pedometer;
        bool _running;

        public HardwarePedometerSource(StepDatabase database)
        {
            _database = database;
        }

        public string Name => "Встроенный шагомер";

        public int PendingStepsToday => 0;

        /// <summary>Пользователь запретил доступ к данным о движении.</summary>
        public bool IsAccessDenied { get; private set; }

        public event EventHandler StepsDetected;

        public async Task<bool> InitializeAsync()
        {
            if (!ApiInformation.IsTypePresent("Windows.Devices.Sensors.Pedometer"))
            {
                return false;
            }

            try
            {
                DeviceAccessStatus status = DeviceAccessInformation
                    .CreateFromDeviceClassId(ActivitySensorClassId).CurrentStatus;
                if (status == DeviceAccessStatus.DeniedByUser || status == DeviceAccessStatus.DeniedBySystem)
                {
                    IsAccessDenied = true;
                    return false;
                }
            }
            catch (Exception)
            {
                // Проверка разрешения не поддерживается — просто пробуем открыть датчик.
            }

            try
            {
                _pedometer = await Pedometer.GetDefaultAsync();
            }
            catch (UnauthorizedAccessException)
            {
                IsAccessDenied = true;
                return false;
            }
            catch (Exception)
            {
                return false;
            }

            if (_pedometer == null)
            {
                return false;
            }

            LoadState();
            return true;
        }

        public async Task SyncAsync()
        {
            DateTimeOffset from;
            lock (_lock)
            {
                from = _lastTimestamp == DateTimeOffset.MinValue
                    ? DateTimeOffset.Now.AddDays(-FirstSyncDays)
                    : _lastTimestamp;
            }

            IReadOnlyList<PedometerReading> readings = await Pedometer.GetSystemHistoryAsync(from);

            var segments = new List<ActivitySegment>();
            lock (_lock)
            {
                foreach (PedometerReading reading in readings.OrderBy(r => r.Timestamp))
                {
                    ActivitySegment segment = Process(reading);
                    if (segment != null)
                    {
                        segments.Add(segment);
                    }
                }
            }

            await Task.Run(() =>
            {
                _database.AddSegments(segments);
                lock (_lock)
                {
                    SaveState();
                }
            });
        }

        public void Start()
        {
            if (_pedometer == null || _running)
            {
                return;
            }

            _running = true;
            _pedometer.ReadingChanged += OnReadingChanged;
        }

        public void Stop()
        {
            if (_pedometer == null || !_running)
            {
                return;
            }

            _running = false;
            _pedometer.ReadingChanged -= OnReadingChanged;
        }

        void OnReadingChanged(Pedometer sender, PedometerReadingChangedEventArgs args)
        {
            ActivitySegment segment;
            lock (_lock)
            {
                segment = Process(args.Reading);
            }

            if (segment != null)
            {
                _database.AddSegments(new[] { segment });
                lock (_lock)
                {
                    SaveState();
                }
                StepsDetected?.Invoke(this, EventArgs.Empty);
            }
        }

        // Превращает очередное накопленное показание в отрезок активности. Вызывать под _lock.
        ActivitySegment Process(PedometerReading reading)
        {
            if (reading.Timestamp <= _lastTimestamp)
            {
                return null; // Уже учтено.
            }
            _lastTimestamp = reading.Timestamp;

            var current = new Baseline(reading.CumulativeSteps, reading.CumulativeStepsDuration, reading.Timestamp);
            Baseline previous;
            bool hasPrevious = _baselines.TryGetValue(reading.StepKind, out previous);
            _baselines[reading.StepKind] = current;
            if (!hasPrevious)
            {
                return null;
            }

            // Если счётчик уменьшился, значит он был сброшен (например, после перезагрузки).
            bool reset = current.Steps < previous.Steps;
            int steps = reset ? current.Steps : current.Steps - previous.Steps;
            if (steps <= 0)
            {
                return null;
            }

            TimeSpan span = current.Timestamp - previous.Timestamp;
            TimeSpan active = reset || current.Duration < previous.Duration
                ? current.Duration
                : current.Duration - previous.Duration;
            if (active <= TimeSpan.Zero)
            {
                active = TimeSpan.FromTicks(steps * TimeSpan.FromMilliseconds(500).Ticks);
            }
            if (active > span && span > TimeSpan.Zero)
            {
                active = span;
            }

            return ActivitySegment.Create(
                reading.Timestamp, active, steps,
                reading.StepKind == PedometerStepKind.Running,
                ActivitySegment.SourcePedometer);
        }

        void LoadState()
        {
            object value;
            if (!ApplicationData.Current.LocalSettings.Values.TryGetValue(StateKey, out value))
            {
                return;
            }

            var state = value as ApplicationDataCompositeValue;
            if (state == null)
            {
                return;
            }

            lock (_lock)
            {
                object last;
                if (state.TryGetValue("last", out last) && last is long)
                {
                    _lastTimestamp = new DateTimeOffset((long)last, TimeSpan.Zero);
                }

                foreach (PedometerStepKind kind in StepKinds)
                {
                    string prefix = ((int)kind).ToString();
                    object steps, duration, timestamp;
                    if (state.TryGetValue(prefix + "s", out steps) && steps is int
                        && state.TryGetValue(prefix + "d", out duration) && duration is long
                        && state.TryGetValue(prefix + "t", out timestamp) && timestamp is long)
                    {
                        _baselines[kind] = new Baseline(
                            (int)steps,
                            TimeSpan.FromTicks((long)duration),
                            new DateTimeOffset((long)timestamp, TimeSpan.Zero));
                    }
                }
            }
        }

        // Вызывать под _lock.
        void SaveState()
        {
            var state = new ApplicationDataCompositeValue();
            state["last"] = _lastTimestamp.UtcTicks;
            foreach (KeyValuePair<PedometerStepKind, Baseline> pair in _baselines)
            {
                string prefix = ((int)pair.Key).ToString();
                state[prefix + "s"] = pair.Value.Steps;
                state[prefix + "d"] = pair.Value.Duration.Ticks;
                state[prefix + "t"] = pair.Value.Timestamp.UtcTicks;
            }
            ApplicationData.Current.LocalSettings.Values[StateKey] = state;
        }

        struct Baseline
        {
            public Baseline(int steps, TimeSpan duration, DateTimeOffset timestamp)
            {
                Steps = steps;
                Duration = duration;
                Timestamp = timestamp;
            }

            public int Steps { get; }

            public TimeSpan Duration { get; }

            public DateTimeOffset Timestamp { get; }
        }
    }
}
