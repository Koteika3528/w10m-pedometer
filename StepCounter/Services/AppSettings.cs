using Windows.Storage;

namespace StepCounter.Services
{
    /// <summary>Пользовательские настройки, хранятся в LocalSettings.</summary>
    public static class AppSettings
    {
        public const int DefaultDailyGoal = 10000;
        public const int DefaultStepLengthCm = 70;
        public const int DefaultWeightKg = 70;

        public static int DailyGoal
        {
            get => Get(nameof(DailyGoal), DefaultDailyGoal);
            set => Set(nameof(DailyGoal), value);
        }

        public static int StepLengthCm
        {
            get => Get(nameof(StepLengthCm), DefaultStepLengthCm);
            set => Set(nameof(StepLengthCm), value);
        }

        public static int WeightKg
        {
            get => Get(nameof(WeightKg), DefaultWeightKg);
            set => Set(nameof(WeightKg), value);
        }

        /// <summary>День (yyyyMMdd), за который уже показали поздравление.</summary>
        public static int LastCelebratedDay
        {
            get => Get(nameof(LastCelebratedDay), 0);
            set => Set(nameof(LastCelebratedDay), value);
        }

        static int Get(string key, int defaultValue)
        {
            object value;
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out value) && value is int)
            {
                return (int)value;
            }
            return defaultValue;
        }

        static void Set(string key, int value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
    }
}
