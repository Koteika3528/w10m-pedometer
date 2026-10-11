using System;
using System.Globalization;

namespace StepCounter.ViewModels
{
    /// <summary>Строка в списке истории.</summary>
    public sealed class DayStepsItem : ObservableObject
    {
        int _steps;
        int _goal;

        public DayStepsItem(DateTime date, int steps, int goal)
        {
            Date = date.Date;
            _steps = steps;
            _goal = goal;
        }

        public DateTime Date { get; }

        public string DateText
        {
            get
            {
                DateTime today = DateTime.Today;
                if (Date == today)
                {
                    return "Сегодня";
                }
                if (Date == today.AddDays(-1))
                {
                    return "Вчера";
                }
                return Date.ToString("ddd, d MMMM", CultureInfo.CurrentCulture);
            }
        }

        public int Steps
        {
            get { return _steps; }
            set
            {
                if (SetProperty(ref _steps, value))
                {
                    OnPropertyChanged(nameof(StepsText));
                    OnPropertyChanged(nameof(Percent));
                }
            }
        }

        public int Goal
        {
            get { return _goal; }
            set
            {
                if (SetProperty(ref _goal, value))
                {
                    OnPropertyChanged(nameof(Percent));
                }
            }
        }

        public string StepsText => _steps.ToString("N0", CultureInfo.CurrentCulture);

        public double Percent => _goal > 0 ? Math.Min(100.0, 100.0 * _steps / _goal) : 0;
    }
}
