using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation.Metadata;

namespace StepCounter.Services
{
    /// <summary>
    /// Встроенный шагомер (Windows.Devices.Sensors.Pedometer). Система считает шаги
    /// в фоне сама, поэтому история доступна, даже если приложение было закрыто.
    /// </summary>
    public sealed class HardwarePedometerSource : IStepSource
    {
        // Класс устройств «датчик активности»: по нему проверяется разрешение «Движение».
        static readonly Guid ActivitySensorClassId = new Guid("9D9E0118-1807-4F2E-96E4-2CE57142E196");

        readonly object _lock = new object();

        // Последнее известное накопленное значение для каждого вида шагов (ходьба, бег, ...).
        Dictionary<PedometerStepKind, int> _lastCumulative = new Dictionary<PedometerStepKind, int>();

        Pedometer _pedometer;
        bool _running;
        DateTime _day;
        int _todaySteps;

        public string Name => "Встроенный шагомер";

        /// <summary>Пользователь запретил доступ к данным о движении.</summary>
        public bool IsAccessDenied { get; private set; }

        public event EventHandler<int> TodayStepsChanged;

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

            return _pedometer != null;
        }

        public async Task<IReadOnlyList<DailySteps>> GetHistoryAsync(int days)
        {
            DateTime today = DateTime.Today;
            DateTime firstDay = today.AddDays(-(days - 1));

            // Берём показания с запасом в сутки, чтобы у первого показания периода
            // было предыдущее значение и шаги считались с начала дня.
            IReadOnlyList<PedometerReading> readings =
                await Pedometer.GetSystemHistoryAsync(new DateTimeOffset(firstDay.AddDays(-1)));

            var totals = new int[days];
            var lastCumulative = new Dictionary<PedometerStepKind, int>();
            foreach (PedometerReading reading in readings.OrderBy(r => r.Timestamp))
            {
                int previous;
                if (lastCumulative.TryGetValue(reading.StepKind, out previous))
                {
                    int index = (int)(reading.Timestamp.LocalDateTime.Date - firstDay).TotalDays;
                    if (index >= 0 && index < days)
                    {
                        totals[index] += Delta(previous, reading.CumulativeSteps);
                    }
                }
                lastCumulative[reading.StepKind] = reading.CumulativeSteps;
            }

            lock (_lock)
            {
                _day = today;
                _todaySteps = totals[days - 1];
                _lastCumulative = lastCumulative;
            }

            var result = new List<DailySteps>(days);
            for (int i = 0; i < days; i++)
            {
                result.Add(new DailySteps(firstDay.AddDays(i), totals[i]));
            }
            return result;
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
            PedometerReading reading = args.Reading;
            int total;
            lock (_lock)
            {
                DateTime today = DateTime.Today;
                if (today != _day)
                {
                    _day = today;
                    _todaySteps = 0;
                }

                int previous;
                if (_lastCumulative.TryGetValue(reading.StepKind, out previous))
                {
                    _todaySteps += Delta(previous, reading.CumulativeSteps);
                }
                _lastCumulative[reading.StepKind] = reading.CumulativeSteps;
                total = _todaySteps;
            }

            TodayStepsChanged?.Invoke(this, total);
        }

        static int Delta(int previous, int current)
        {
            // Если счётчик уменьшился, значит он был сброшен (например, после перезагрузки).
            return current >= previous ? current - previous : current;
        }
    }
}
