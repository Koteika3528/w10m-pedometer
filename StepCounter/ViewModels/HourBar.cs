namespace StepCounter.ViewModels
{
    /// <summary>Столбец диаграммы активности за один час.</summary>
    public sealed class HourBar
    {
        public HourBar(int hour, int steps, double kcal)
        {
            Hour = hour;
            Steps = steps;
            Kcal = kcal;
        }

        public int Hour { get; }

        public int Steps { get; }

        public double Kcal { get; }
    }
}
