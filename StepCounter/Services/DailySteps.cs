using System;

namespace StepCounter.Services
{
    /// <summary>Количество шагов за один календарный день.</summary>
    public sealed class DailySteps
    {
        public DailySteps(DateTime date, int steps)
        {
            Date = date.Date;
            Steps = steps;
        }

        public DateTime Date { get; }

        public int Steps { get; }
    }
}
