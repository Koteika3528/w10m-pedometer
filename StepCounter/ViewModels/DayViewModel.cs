using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepCounter.Data;
using StepCounter.Services;

namespace StepCounter.ViewModels
{
    /// <summary>Одна страница FlipView — итоги одного дня.</summary>
    public sealed class DayViewModel : ObservableObject
    {
        static readonly IReadOnlyList<ChartBar> NoBars = new ChartBar[0];

        bool _isLoaded;
        bool _hasData;
        bool _isNoData;
        string _stepsText = "0";
        string _goalText;
        double _progress;
        string _kcalText;
        string _kcalHint;
        string _distanceText;
        string _motivationText;
        string _workoutsText = "0";
        string _workoutsLabel = "тренировок";
        string _activeTimeText = "0 мин";
        IReadOnlyList<ChartBar> _hourly = NoBars;

        public DayViewModel(DateTime date)
        {
            Date = date.Date;
        }

        public DateTime Date { get; }

        public int Key => DayKey.From(Date);

        public bool IsToday => Date == DateTime.Today;

        public string Title
        {
            get
            {
                DateTime today = DateTime.Today;
                if (Date == today)
                {
                    return "сегодня";
                }
                if (Date == today.AddDays(-1))
                {
                    return "вчера";
                }
                string format = Date.Year == today.Year ? "dddd, d MMMM" : "d MMMM yyyy";
                return CultureInfo.CurrentCulture.TextInfo.ToLower(Date.ToString(format, CultureInfo.CurrentCulture));
            }
        }

        public bool IsLoaded
        {
            get { return _isLoaded; }
            set { SetProperty(ref _isLoaded, value); }
        }

        public bool HasData { get { return _hasData; } private set { SetProperty(ref _hasData, value); } }
        public bool IsNoData { get { return _isNoData; } private set { SetProperty(ref _isNoData, value); } }
        public string StepsText { get { return _stepsText; } private set { SetProperty(ref _stepsText, value); } }
        public string GoalText { get { return _goalText; } private set { SetProperty(ref _goalText, value); } }
        public double Progress { get { return _progress; } private set { SetProperty(ref _progress, value); } }
        public string KcalText { get { return _kcalText; } private set { SetProperty(ref _kcalText, value); } }
        public string KcalHint { get { return _kcalHint; } private set { SetProperty(ref _kcalHint, value); } }
        public string DistanceText { get { return _distanceText; } private set { SetProperty(ref _distanceText, value); } }
        public string MotivationText { get { return _motivationText; } private set { SetProperty(ref _motivationText, value); } }
        public string WorkoutsText { get { return _workoutsText; } private set { SetProperty(ref _workoutsText, value); } }
        public string WorkoutsLabel { get { return _workoutsLabel; } private set { SetProperty(ref _workoutsLabel, value); } }
        public string ActiveTimeText { get { return _activeTimeText; } private set { SetProperty(ref _activeTimeText, value); } }
        public IReadOnlyList<ChartBar> Hourly { get { return _hourly; } private set { SetProperty(ref _hourly, value); } }

        /// <summary>Шаги за день вместе с ещё не сохранёнными (для поздравления).</summary>
        public int TotalSteps { get; private set; }

        /// <param name="summary">Итоги дня или null, если записей нет.</param>
        /// <param name="segments">Отрезки активности дня (для графика по часам).</param>
        /// <param name="pendingSteps">Шаги текущего, ещё не записанного отрезка (только для сегодня).</param>
        public void Apply(DaySummary summary, IList<ActivitySegment> segments, int pendingSteps)
        {
            bool isToday = IsToday;
            OnPropertyChanged(nameof(Title));
            IsLoaded = true;

            if (summary == null && pendingSteps == 0 && !isToday)
            {
                TotalSteps = 0;
                HasData = false;
                IsNoData = true;
                Hourly = NoBars;
                return;
            }

            int walking = (summary?.WalkingSteps ?? 0) + pendingSteps;
            int running = summary?.RunningSteps ?? 0;
            int steps = walking + running;
            int goal = AppSettings.DailyGoal;
            double kcal = ActivityMath.Kcal(walking, running);
            int workouts = summary?.Workouts ?? 0;
            CultureInfo culture = CultureInfo.CurrentCulture;

            TotalSteps = steps;
            StepsText = steps.ToString("N0", culture);
            GoalText = string.Format(culture, "{0} из {1:N0}", Motivation.Plural(steps, "шаг", "шага", "шагов"), goal);
            Progress = goal > 0 ? Math.Min(100.0, 100.0 * steps / goal) : 0;
            KcalText = kcal.ToString("N0", culture);
            KcalHint = Motivation.KcalEquivalent(kcal);
            DistanceText = string.Format(culture, "{0:0.00}", ActivityMath.DistanceKm(walking, running));
            MotivationText = Motivation.ForDay(Date, isToday, steps, goal);
            WorkoutsText = workouts.ToString(culture);
            WorkoutsLabel = Motivation.Plural(workouts, "тренировка", "тренировки", "тренировок");
            ActiveTimeText = Motivation.FormatDuration(TimeSpan.FromSeconds(summary?.ActiveSeconds ?? 0));
            Hourly = BuildHourly(segments, isToday ? pendingSteps : 0);

            IsNoData = false;
            HasData = true;
        }

        static IReadOnlyList<ChartBar> BuildHourly(IList<ActivitySegment> segments, int pendingSteps)
        {
            var walking = new int[24];
            var running = new int[24];
            foreach (ActivitySegment segment in segments ?? Enumerable.Empty<ActivitySegment>())
            {
                if (segment.IsRunning)
                {
                    running[segment.Hour] += segment.Steps;
                }
                else
                {
                    walking[segment.Hour] += segment.Steps;
                }
            }
            walking[DateTime.Now.Hour] += pendingSteps;

            var bars = new ChartBar[24];
            for (int hour = 0; hour < 24; hour++)
            {
                string label = hour % 6 == 0 ? hour.ToString("00") + ":00" : null;
                bars[hour] = new ChartBar(
                    label, walking[hour] + running[hour], ActivityMath.Kcal(walking[hour], running[hour]));
            }
            return bars;
        }
    }
}
