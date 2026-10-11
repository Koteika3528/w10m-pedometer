using System;
using SQLite;

namespace StepCounter.Data
{
    /// <summary>
    /// Отрезок активности: сколько шагов сделано и сколько времени шли,
    /// заканчивая моментом <see cref="EndTicks"/>. Из отрезков считаются
    /// итоги дня, график по часам и тренировки.
    /// </summary>
    [Table("Segments")]
    public sealed class ActivitySegment
    {
        public const int SourcePedometer = 0;
        public const int SourceAccelerometer = 1;

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Конец отрезка, UTC ticks.</summary>
        [Indexed]
        public long EndTicks { get; set; }

        /// <summary>Сколько времени из отрезка человек шёл (ticks).</summary>
        public long ActiveTicks { get; set; }

        public int Steps { get; set; }

        public bool IsRunning { get; set; }

        public int Source { get; set; }

        /// <summary>Местный день (yyyyMMdd), к которому отнесён отрезок.</summary>
        [Indexed]
        public int Day { get; set; }

        /// <summary>Местный час (0–23), к которому отнесён отрезок.</summary>
        public int Hour { get; set; }

        public static ActivitySegment Create(DateTimeOffset end, TimeSpan active, int steps, bool isRunning, int source)
        {
            // День и час определяем по середине активного времени.
            DateTime middle = (end - TimeSpan.FromTicks(active.Ticks / 2)).LocalDateTime;
            return new ActivitySegment
            {
                EndTicks = end.UtcTicks,
                ActiveTicks = active.Ticks,
                Steps = steps,
                IsRunning = isRunning,
                Source = source,
                Day = DayKey.From(middle),
                Hour = middle.Hour,
            };
        }
    }
}
