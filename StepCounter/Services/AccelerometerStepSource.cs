using System;
using System.Threading.Tasks;
using StepCounter.Data;
using Windows.Devices.Sensors;

namespace StepCounter.Services
{
    /// <summary>
    /// Запасной источник для телефонов без встроенного шагомера:
    /// шаги считаются по акселерометру, пока приложение открыто.
    /// Шаги собираются в отрезки (не длиннее минуты и не через границу часа)
    /// и по завершении отрезка записываются в базу.
    /// </summary>
    public sealed class AccelerometerStepSource : IStepSource
    {
        const uint DesiredReportIntervalMs = 20;

        // Темп (шагов в секунду), начиная с которого считаем, что человек бежит.
        const double RunningCadence = 2.6;

        static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(2);
        static readonly TimeSpan MaxSegment = TimeSpan.FromSeconds(60);
        static readonly TimeSpan TypicalStep = TimeSpan.FromMilliseconds(500);

        readonly StepDatabase _database;
        readonly StepDetector _detector = new StepDetector();
        readonly object _lock = new object();

        Accelerometer _accelerometer;
        bool _running;

        bool _hasSegment;
        DateTimeOffset _segmentStart;
        DateTimeOffset _segmentLastStep;
        int _segmentSteps;

        public AccelerometerStepSource(StepDatabase database)
        {
            _database = database;
        }

        public string Name => "Акселерометр (считает, только пока приложение открыто)";

        public int PendingStepsToday
        {
            get
            {
                lock (_lock)
                {
                    return _hasSegment && _segmentStart.LocalDateTime.Date == DateTime.Today ? _segmentSteps : 0;
                }
            }
        }

        public event EventHandler StepsDetected;

        public Task<bool> InitializeAsync()
        {
            _accelerometer = Accelerometer.GetDefault();
            return Task.FromResult(_accelerometer != null);
        }

        public Task SyncAsync()
        {
            ActivitySegment finished = null;
            lock (_lock)
            {
                if (_hasSegment && DateTimeOffset.Now - _segmentLastStep > MaxGap)
                {
                    finished = CloseSegment();
                }
            }

            if (finished == null)
            {
                return Task.CompletedTask;
            }
            return Task.Run(() => _database.AddSegments(new[] { finished }));
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

            ActivitySegment finished = null;
            lock (_lock)
            {
                if (_hasSegment)
                {
                    finished = CloseSegment();
                }
            }
            if (finished != null)
            {
                _database.AddSegments(new[] { finished });
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

            DateTimeOffset now = reading.Timestamp;
            ActivitySegment finished = null;
            lock (_lock)
            {
                if (_hasSegment && NeedsNewSegment(now))
                {
                    finished = CloseSegment();
                }

                if (_hasSegment)
                {
                    _segmentLastStep = now;
                    _segmentSteps += newSteps;
                }
                else
                {
                    // Детектор может подтвердить сразу несколько шагов, сделанных чуть раньше.
                    _hasSegment = true;
                    _segmentStart = now - TimeSpan.FromTicks(TypicalStep.Ticks * (newSteps - 1));
                    _segmentLastStep = now;
                    _segmentSteps = newSteps;
                }
            }

            if (finished != null)
            {
                _database.AddSegments(new[] { finished });
            }
            StepsDetected?.Invoke(this, EventArgs.Empty);
        }

        // Вызывать под _lock.
        bool NeedsNewSegment(DateTimeOffset now)
        {
            if (now - _segmentLastStep > MaxGap || now - _segmentStart > MaxSegment)
            {
                return true;
            }

            DateTime start = _segmentStart.LocalDateTime;
            DateTime current = now.LocalDateTime;
            return start.Date != current.Date || start.Hour != current.Hour;
        }

        // Вызывать под _lock.
        ActivitySegment CloseSegment()
        {
            TimeSpan active = _segmentLastStep - _segmentStart + TypicalStep;
            double cadence = _segmentSteps / active.TotalSeconds;
            _hasSegment = false;
            return ActivitySegment.Create(
                _segmentLastStep, active, _segmentSteps, cadence >= RunningCadence, ActivitySegment.SourceAccelerometer);
        }
    }
}
