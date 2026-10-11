namespace StepCounter.ViewModels
{
    /// <summary>Столбец диаграммы: шаги и ккал за час, день или месяц.</summary>
    public sealed class ChartBar
    {
        public ChartBar(string label, double steps, double kcal)
        {
            Label = label;
            Steps = steps;
            Kcal = kcal;
        }

        /// <summary>Подпись под столбцом; пустая — без подписи.</summary>
        public string Label { get; }

        public double Steps { get; }

        public double Kcal { get; }
    }
}
