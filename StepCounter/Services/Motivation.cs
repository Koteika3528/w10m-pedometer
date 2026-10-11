using System;

namespace StepCounter.Services
{
    /// <summary>Тексты поощрения и мотивации.</summary>
    public static class Motivation
    {
        const double KcalPerApple = 80;

        static readonly string[] NoSteps =
        {
            "Самое время размяться!",
            "Каждый путь начинается с первого шага.",
            "Небольшая прогулка поднимет настроение.",
        };

        static readonly string[] Started =
        {
            "Хорошее начало, продолжайте!",
            "Вы в пути — так держать!",
            "Первые шаги сделаны, дальше легче.",
        };

        static readonly string[] Quarter =
        {
            "Четверть пути позади!",
            "Отличный темп, не сбавляйте!",
            "Уже неплохо — прогуляйтесь ещё.",
        };

        static readonly string[] Half =
        {
            "Больше половины — вы молодец!",
            "Половина нормы есть, финиш всё ближе.",
            "Отлично идёте!",
        };

        static readonly string[] AlmostDone =
        {
            "Осталось всего {0:N0} {1}!",
            "Финиш близко — ещё {0:N0} {1}.",
            "Ещё немного: {0:N0} {1} до цели.",
        };

        static readonly string[] GoalDone =
        {
            "Норма выполнена! Так держать!",
            "Цель достигнута — отличный день!",
            "Вы справились! Так держать!",
        };

        static readonly string[] GoalExceeded =
        {
            "{0}% нормы — вы превзошли себя!",
            "Уже {0}% нормы. Потрясающе!",
        };

        public static readonly string[] Celebration =
        {
            "Так держать!",
            "Вы молодец!",
            "Отличная работа!",
            "Так держать, чемпион!",
        };

        public static string ForDay(DateTime date, bool isToday, int steps, int goal)
        {
            int percent = goal > 0 ? (int)(100L * steps / goal) : 0;
            int salt = date.DayOfYear;

            if (!isToday)
            {
                if (percent >= 100)
                {
                    return "Норма была выполнена — отличный день!";
                }
                return string.Format("Пройдено {0}% нормы.", percent);
            }

            if (steps == 0)
            {
                return Pick(NoSteps, salt);
            }
            if (percent >= 150)
            {
                return string.Format(Pick(GoalExceeded, salt), percent);
            }
            if (percent >= 100)
            {
                return Pick(GoalDone, salt);
            }
            if (percent >= 75)
            {
                int left = goal - steps;
                return string.Format(Pick(AlmostDone, salt), left, Plural(left, "шаг", "шага", "шагов"));
            }
            if (percent >= 50)
            {
                return Pick(Half, salt);
            }
            if (percent >= 25)
            {
                return Pick(Quarter, salt);
            }
            return Pick(Started, salt);
        }

        /// <summary>Наглядное сравнение калорий, например «≈ 3 яблока».</summary>
        public static string KcalEquivalent(double kcal)
        {
            int apples = (int)Math.Round(kcal / KcalPerApple);
            if (apples < 1)
            {
                return string.Empty;
            }
            return string.Format("≈ {0} {1}", apples, Plural(apples, "яблоко", "яблока", "яблок"));
        }

        public static string Pick(string[] options, int salt)
        {
            return options[Math.Abs(salt) % options.Length];
        }

        /// <summary>Русское склонение по числу: 1 шаг, 2 шага, 5 шагов.</summary>
        public static string Plural(long n, string one, string few, string many)
        {
            n = Math.Abs(n);
            long lastTwo = n % 100;
            long last = n % 10;
            if (lastTwo >= 11 && lastTwo <= 14)
            {
                return many;
            }
            if (last == 1)
            {
                return one;
            }
            if (last >= 2 && last <= 4)
            {
                return few;
            }
            return many;
        }

        public static string FormatDuration(TimeSpan duration)
        {
            int totalMinutes = (int)duration.TotalMinutes;
            if (totalMinutes < 60)
            {
                return totalMinutes + " мин";
            }
            return string.Format("{0} ч {1:00} мин", totalMinutes / 60, totalMinutes % 60);
        }
    }
}
