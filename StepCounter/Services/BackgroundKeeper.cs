using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Devices.Geolocation;

namespace StepCounter.Services
{
    /// <summary>
    /// Не даёт системе заморозить приложение при выключенном экране:
    /// запрашивает продлённое выполнение (ExtendedExecutionSession) с причиной
    /// «отслеживание местоположения». Такая сессия на Windows 10 Mobile позволяет
    /// работать под экраном блокировки, но система может её отозвать
    /// (нехватка памяти, режим экономии заряда).
    /// </summary>
    public sealed class BackgroundKeeper
    {
        readonly object _lock = new object();

        ExtendedExecutionSession _session;
        Geolocator _geolocator;

        public bool IsActive
        {
            get
            {
                lock (_lock)
                {
                    return _session != null;
                }
            }
        }

        /// <summary>Система отозвала разрешение работать в фоне. Может вызываться не из UI-потока.</summary>
        public event EventHandler<ExtendedExecutionRevokedReason> Revoked;

        /// <summary>
        /// Включает работу в фоне. Вызывать из UI-потока, пока приложение на экране
        /// (может показать запрос доступа к местоположению).
        /// Возвращает null при успехе или текст ошибки.
        /// </summary>
        public async Task<string> StartAsync()
        {
            if (IsActive)
            {
                return null;
            }

            GeolocationAccessStatus access;
            try
            {
                access = await Geolocator.RequestAccessAsync();
            }
            catch (Exception ex)
            {
                return "Не удалось запросить доступ к местоположению: " + ex.Message;
            }
            if (access != GeolocationAccessStatus.Allowed)
            {
                return "Разрешите приложению доступ к местоположению: без него система не даёт работать в фоне.";
            }

            // Местоположение по сетям (без GPS) и редкие обновления — почти не тратят заряд,
            // но сессия «отслеживания местоположения» должна им пользоваться.
            var geolocator = new Geolocator
            {
                DesiredAccuracyInMeters = 1000,
                MovementThreshold = 200,
            };
            geolocator.PositionChanged += OnPositionChanged;

            var session = new ExtendedExecutionSession
            {
                Reason = ExtendedExecutionReason.LocationTracking,
                Description = "Подсчёт шагов при выключенном экране",
            };
            session.Revoked += OnSessionRevoked;

            ExtendedExecutionResult result;
            try
            {
                result = await session.RequestExtensionAsync();
            }
            catch (Exception ex)
            {
                result = ExtendedExecutionResult.Denied;
                System.Diagnostics.Debug.WriteLine(ex);
            }

            if (result != ExtendedExecutionResult.Allowed)
            {
                session.Revoked -= OnSessionRevoked;
                session.Dispose();
                geolocator.PositionChanged -= OnPositionChanged;
                return "Система не разрешила работу в фоне. Проверьте режим экономии заряда.";
            }

            lock (_lock)
            {
                _session = session;
                _geolocator = geolocator;
            }
            return null;
        }

        public void Stop()
        {
            ExtendedExecutionSession session;
            Geolocator geolocator;
            lock (_lock)
            {
                session = _session;
                geolocator = _geolocator;
                _session = null;
                _geolocator = null;
            }

            if (geolocator != null)
            {
                geolocator.PositionChanged -= OnPositionChanged;
            }
            if (session != null)
            {
                session.Revoked -= OnSessionRevoked;
                session.Dispose();
            }
        }

        void OnPositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            // Координаты не нужны: подписка только поддерживает сессию.
        }

        void OnSessionRevoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            Stop();
            Revoked?.Invoke(this, args.Reason);
        }
    }
}
