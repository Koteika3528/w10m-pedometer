using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StepCounter.Services
{
    /// <summary>Источник данных о шагах (встроенный шагомер или акселерометр).</summary>
    public interface IStepSource
    {
        /// <summary>Название источника для отображения пользователю.</summary>
        string Name { get; }

        /// <summary>Пытается подключиться к датчику. Возвращает false, если источник недоступен.</summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Шаги за последние <paramref name="days"/> дней, от самого старого дня к сегодняшнему.
        /// Заодно синхронизирует счётчик за сегодня.
        /// </summary>
        Task<IReadOnlyList<DailySteps>> GetHistoryAsync(int days);

        /// <summary>Начать получать данные в реальном времени.</summary>
        void Start();

        /// <summary>Прекратить получать данные (например, при приостановке приложения).</summary>
        void Stop();

        /// <summary>
        /// Изменилось количество шагов за сегодня. Аргумент — новое значение.
        /// Может вызываться не из UI-потока.
        /// </summary>
        event EventHandler<int> TodayStepsChanged;
    }
}
