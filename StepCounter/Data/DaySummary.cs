using SQLite;

namespace StepCounter.Data
{
    /// <summary>Итоги дня. Пересчитываются из отрезков при каждом их изменении.</summary>
    [Table("Days")]
    public sealed class DaySummary
    {
        [PrimaryKey]
        public int Day { get; set; }

        /// <summary>Все шаги за день (включая бег).</summary>
        public int Steps { get; set; }

        public int RunningSteps { get; set; }

        public int ActiveSeconds { get; set; }

        public int Workouts { get; set; }

        [Ignore]
        public int WalkingSteps => Steps - RunningSteps;
    }
}
