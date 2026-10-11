using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepCounter.Data;
using StepCounter.Services;

namespace StepCounter.ViewModels
{
    /// <summary>
    /// Средние значения в день за неделю, месяц и год.
    /// Считаются по завершённым дням (без сегодняшнего) и только по дням, где есть данные.
    /// </summary>
    public sealed class AveragesViewModel : ObservableObject
    {
        public const int YearDays = 365;
        const string Empty = "—";

        string _weekSteps = Empty, _weekKcal = Empty;
        string _monthSteps = Empty, _monthKcal = Empty;
        string _yearSteps = Empty, _yearKcal = Empty;

        public string WeekSteps { get { return _weekSteps; } private set { SetProperty(ref _weekSteps, value); } }
        public string WeekKcal { get { return _weekKcal; } private set { SetProperty(ref _weekKcal, value); } }
        public string MonthSteps { get { return _monthSteps; } private set { SetProperty(ref _monthSteps, value); } }
        public string MonthKcal { get { return _monthKcal; } private set { SetProperty(ref _monthKcal, value); } }
        public string YearSteps { get { return _yearSteps; } private set { SetProperty(ref _yearSteps, value); } }
        public string YearKcal { get { return _yearKcal; } private set { SetProperty(ref _yearKcal, value); } }

        /// <param name="days">Итоги дней за последний год, без сегодняшнего.</param>
        public void Apply(IReadOnlyCollection<DaySummary> days, DateTime today)
        {
            string steps, kcal;

            Average(days, today, 7, out steps, out kcal);
            WeekSteps = steps;
            WeekKcal = kcal;

            Average(days, today, 30, out steps, out kcal);
            MonthSteps = steps;
            MonthKcal = kcal;

            Average(days, today, YearDays, out steps, out kcal);
            YearSteps = steps;
            YearKcal = kcal;
        }

        static void Average(IEnumerable<DaySummary> days, DateTime today, int period, out string steps, out string kcal)
        {
            int fromDay = DayKey.From(today.AddDays(-period));
            List<DaySummary> inPeriod = days.Where(d => d.Day >= fromDay).ToList();
            if (inPeriod.Count == 0)
            {
                steps = Empty;
                kcal = Empty;
                return;
            }

            CultureInfo culture = CultureInfo.CurrentCulture;
            steps = inPeriod.Average(d => d.Steps).ToString("N0", culture);
            kcal = inPeriod.Average(d => ActivityMath.Kcal(d.WalkingSteps, d.RunningSteps)).ToString("N0", culture);
        }
    }
}
