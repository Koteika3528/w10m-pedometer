using System;
using System.Threading.Tasks;

namespace StepCounter.Services
{
    /// <summary>
    /// Источник шагов (встроенный шагомер или акселерометр).
    /// Все данные источник записывает в <see cref="Data.StepDatabase"/>.
    /// </summary>
    public interface IStepSource
    {
        /// <summary>Название источника для отображения пользователю.</summary>
        string Name { get; }

        /// <summary>
        /// Шаги за сегодня, которые ещё не попали в базу (идёт текущий отрезок ходьбы).
        /// </summary>
        int PendingStepsToday { get; }

        /// <summary>Пытается подключиться к датчику. Возвращает false, если источник недоступен.</summary>
        Task<bool> InitializeAsync();

        /// <summary>Переносит в базу всё, что накопилось с прошлой синхронизации.</summary>
        Task SyncAsync();

        /// <summary>Начать получать данные в реальном времени.</summary>
        void Start();

        /// <summary>Прекратить получать данные (например, при приостановке приложения).</summary>
        void Stop();

        /// <summary>Появились новые шаги. Может вызываться не из UI-потока.</summary>
        event EventHandler StepsDetected;
    }
}
