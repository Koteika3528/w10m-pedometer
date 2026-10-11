using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using StepCounter.Data;
using StepCounter.Services;

namespace StepCounter.ViewModels
{
    public enum StatisticsPeriod
    {
        Week,
        Month,
        Year,
    }

    /// <summary>
    /// Статистика за неделю, месяц или год с переключением на предыдущие периоды.
    /// Средние считаются по завершённым дням, где есть данные (сегодня не учитывается),
    /// суммы — по всем дням периода, включая сегодня.
    /// </summary>
    public sealed class StatisticsViewModel : ObservableObject
    {
        const string Empty = "—";

        static readonly string[] MonthLetters = { "я", "ф", "м", "а", "м", "и", "и", "а", "с", "о", "н", "д" };

        readonly StepDatabase _database;

        StatisticsPeriod _period = StatisticsPeriod.Week;
        DateTime _start;

        string _periodTitle;
        bool _canGoNext;
        bool _hasData;
        bool _isNoData;
        string _avgSteps = Empty;
        string _avgKcal = Empty;
        string _avgDistance = Empty;
        string _avgActive = Empty;
        string _totalSteps = Empty;
        string _totalKcal = Empty;
        string _totalDistance = Empty;
        string _totalActive = Empty;
        string _totalWorkouts = Empty;
        string _goalDays = Empty;
        string _dataDays = Empty;
        string _bestDay = Empty;
        string _chartTitle;
        IReadOnlyList<ChartBar> _bars = new ChartBar[0];

        public StatisticsViewModel(StepDatabase database)
        {
            _database = database;
            _start = PeriodStart(DateTime.Today, _period);
        }

        public StatisticsPeriod Period => _period;

        public string PeriodTitle { get { return _periodTitle; } private set { SetProperty(ref _periodTitle, value); } }
        public bool CanGoNext { get { return _canGoNext; } private set { SetProperty(ref _canGoNext, value); } }
        public bool HasData { get { return _hasData; } private set { SetProperty(ref _hasData, value); } }
        public bool IsNoData { get { return _isNoData; } private set { SetProperty(ref _isNoData, value); } }
        public string AvgSteps { get { return _avgSteps; } private set { SetProperty(ref _avgSteps, value); } }
        public string AvgKcal { get { return _avgKcal; } private set { SetProperty(ref _avgKcal, value); } }
        public string AvgDistance { get { return _avgDistance; } private set { SetProperty(ref _avgDistance, value); } }
        public string AvgActive { get { return _avgActive; } private set { SetProperty(ref _avgActive, value); } }
        public string TotalSteps { get { return _totalSteps; } private set { SetProperty(ref _totalSteps, value); } }
        public string TotalKcal { get { return _totalKcal; } private set { SetProperty(ref _totalKcal, value); } }
        public string TotalDistance { get { return _totalDistance; } private set { SetProperty(ref _totalDistance, value); } }
        public string TotalActive { get { return _totalActive; } private set { SetProperty(ref _totalActive, value); } }
        public string TotalWorkouts { get { return _totalWorkouts; } private set { SetProperty(ref _totalWorkouts, value); } }
        public string GoalDays { get { return _goalDays; } private set { SetProperty(ref _goalDays, value); } }
        public string DataDays { get { return _dataDays; } private set { SetProperty(ref _dataDays, value); } }
        public string BestDay { get { return _bestDay; } private set { SetProperty(ref _bestDay, value); } }
        public string ChartTitle { get { return _chartTitle; } private set { SetProperty(ref _chartTitle, value); } }
        public IReadOnlyList<ChartBar> Bars { get { return _bars; } private set { SetProperty(ref _bars, value); } }

        public Task SetPeriodAsync(StatisticsPeriod period)
        {
            if (period == _period)
            {
                return LoadAsync();
            }
            _period = period;
            _start = PeriodStart(DateTime.Today, period);
            return LoadAsync();
        }

        public Task PreviousAsync()
        {
            _start = Shift(_start, _period, -1);
            return LoadAsync();
        }

        public Task NextAsync()
        {
            DateTime next = Shift(_start, _period, 1);
            if (next <= DateTime.Today)
            {
                _start = next;
            }
            return LoadAsync();
        }

        public async Task LoadAsync()
        {
            DateTime today = DateTime.Today;
            DateTime start = _start;
            DateTime end = Shift(start, _period, 1).AddDays(-1);
            DateTime lastCounted = end < today ? end : today;

            PeriodTitle = FormatTitle(start, end, _period);
            CanGoNext = end < today;
            ChartTitle = _period == StatisticsPeriod.Year ? "в среднем в день по месяцам" : "по дням";

            List<DaySummary> days = new List<DaySummary>();
            if (_database != null && start <= today)
            {
                int fromKey = DayKey.From(start);
                int toKey = DayKey.From(lastCounted);
                days = await Task.Run(() => _database.GetDays(fromKey, toKey));
            }

            Apply(days, start, end, lastCounted, today);
        }

        void Apply(List<DaySummary> days, DateTime start, DateTime end, DateTime lastCounted, DateTime today)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            Bars = BuildBars(days, start, end);

            if (days.Count == 0)
            {
                HasData = false;
                IsNoData = true;
                return;
            }

            int goal = AppSettings.DailyGoal;
            int todayKey = DayKey.From(today);
            int elapsedDays = lastCounted >= start ? (lastCounted - start).Days + 1 : 0;

            // Средние — по завершённым дням с данными.
            List<DaySummary> completed = days.Where(d => d.Day != todayKey).ToList();
            if (completed.Count > 0)
            {
                AvgSteps = completed.Average(d => d.Steps).ToString("N0", culture);
                AvgKcal = completed.Average(d => Kcal(d)).ToString("N0", culture);
                AvgDistance = completed.Average(d => Distance(d)).ToString("0.00", culture);
                AvgActive = Motivation.FormatDuration(TimeSpan.FromSeconds(completed.Average(d => d.ActiveSeconds)));
            }
            else
            {
                AvgSteps = AvgKcal = AvgDistance = AvgActive = Empty;
            }

            TotalSteps = days.Sum(d => (long)d.Steps).ToString("N0", culture);
            TotalKcal = days.Sum(d => Kcal(d)).ToString("N0", culture);
            TotalDistance = days.Sum(d => Distance(d)).ToString("0.0", culture);
            TotalActive = Motivation.FormatDuration(TimeSpan.FromSeconds(days.Sum(d => (long)d.ActiveSeconds)));
            TotalWorkouts = days.Sum(d => d.Workouts).ToString(culture);

            int goalDays = days.Count(d => d.Steps >= goal);
            GoalDays = string.Format(culture, "{0} из {1}", goalDays, elapsedDays);
            DataDays = string.Format(culture, "{0} из {1}", days.Count, elapsedDays);

            DaySummary best = days.OrderByDescending(d => d.Steps).First();
            BestDay = string.Format(culture, "{0:N0} — {1}",
                best.Steps, DayKey.ToDate(best.Day).ToString("ddd, d MMMM", culture));

            IsNoData = false;
            HasData = true;
        }

        static IReadOnlyList<ChartBar> BuildBars(List<DaySummary> days, DateTime start, DateTime end)
        {
            Dictionary<int, DaySummary> byDay = days.ToDictionary(d => d.Day);
            var bars = new List<ChartBar>();

            if (end - start > TimeSpan.FromDays(31))
            {
                // Год: среднее в день по каждому месяцу (по дням с данными).
                for (int month = 0; month < 12; month++)
                {
                    DateTime monthStart = start.AddMonths(month);
                    int from = DayKey.From(monthStart);
                    int to = DayKey.From(monthStart.AddMonths(1).AddDays(-1));
                    List<DaySummary> inMonth = days.Where(d => d.Day >= from && d.Day <= to).ToList();
                    double steps = inMonth.Count > 0 ? inMonth.Average(d => d.Steps) : 0;
                    double kcal = inMonth.Count > 0 ? inMonth.Average(d => Kcal(d)) : 0;
                    bars.Add(new ChartBar(MonthLetters[monthStart.Month - 1], steps, kcal));
                }
                return bars;
            }

            bool isWeek = (end - start).Days < 7;
            for (DateTime date = start; date <= end; date = date.AddDays(1))
            {
                DaySummary day;
                byDay.TryGetValue(DayKey.From(date), out day);
                string label;
                if (isWeek)
                {
                    label = date.ToString("ddd", CultureInfo.CurrentCulture);
                }
                else
                {
                    label = date.Day == 1 || date.Day % 5 == 0 ? date.Day.ToString() : null;
                }
                bars.Add(new ChartBar(label, day?.Steps ?? 0, day != null ? Kcal(day) : 0));
            }
            return bars;
        }

        static double Kcal(DaySummary d)
        {
            return ActivityMath.Kcal(d.WalkingSteps, d.RunningSteps);
        }

        static double Distance(DaySummary d)
        {
            return ActivityMath.DistanceKm(d.WalkingSteps, d.RunningSteps);
        }

        static DateTime PeriodStart(DateTime date, StatisticsPeriod period)
        {
            switch (period)
            {
                case StatisticsPeriod.Week:
                    // Неделя начинается с понедельника.
                    int offset = ((int)date.DayOfWeek + 6) % 7;
                    return date.Date.AddDays(-offset);
                case StatisticsPeriod.Month:
                    return new DateTime(date.Year, date.Month, 1);
                default:
                    return new DateTime(date.Year, 1, 1);
            }
        }

        static DateTime Shift(DateTime start, StatisticsPeriod period, int count)
        {
            switch (period)
            {
                case StatisticsPeriod.Week:
                    return start.AddDays(7 * count);
                case StatisticsPeriod.Month:
                    return start.AddMonths(count);
                default:
                    return start.AddYears(count);
            }
        }

        static string FormatTitle(DateTime start, DateTime end, StatisticsPeriod period)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            switch (period)
            {
                case StatisticsPeriod.Week:
                    if (start.Month == end.Month)
                    {
                        return string.Format(culture, "{0}–{1}", start.Day, end.ToString("d MMMM yyyy", culture));
                    }
                    return string.Format(culture, "{0} – {1}",
                        start.ToString("d MMM", culture), end.ToString("d MMM yyyy", culture));
                case StatisticsPeriod.Month:
                    return culture.TextInfo.ToLower(start.ToString("MMMM yyyy", culture));
                default:
                    return start.Year.ToString(culture);
            }
        }
    }
}
