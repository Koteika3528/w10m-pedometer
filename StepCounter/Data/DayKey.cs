using System;

namespace StepCounter.Data
{
    /// <summary>День в виде числа yyyyMMdd — удобно хранить и сравнивать в базе.</summary>
    public static class DayKey
    {
        public static int From(DateTime date)
        {
            return date.Year * 10000 + date.Month * 100 + date.Day;
        }

        public static DateTime ToDate(int key)
        {
            return new DateTime(key / 10000, key / 100 % 100, key % 100);
        }
    }
}
