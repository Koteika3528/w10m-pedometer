using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Storage;

namespace StepCounter.Services
{
    /// <summary>
    /// Хранит шаги по дням в LocalSettings. Используется, когда шаги считает
    /// само приложение (акселерометр), а не система.
    /// </summary>
    public sealed class StepHistoryStore
    {
        const string ContainerName = "Steps";
        const string KeyFormat = "yyyyMMdd";
        const int KeepDays = 90;

        readonly ApplicationDataContainer _container;

        public StepHistoryStore()
        {
            _container = ApplicationData.Current.LocalSettings.CreateContainer(
                ContainerName, ApplicationDataCreateDisposition.Always);
        }

        public int Get(DateTime date)
        {
            object value;
            if (_container.Values.TryGetValue(ToKey(date), out value) && value is int)
            {
                return (int)value;
            }
            return 0;
        }

        public void Set(DateTime date, int steps)
        {
            _container.Values[ToKey(date)] = steps;
            RemoveOldEntries();
        }

        void RemoveOldEntries()
        {
            string oldestKey = ToKey(DateTime.Today.AddDays(-KeepDays));
            List<string> oldKeys = _container.Values.Keys
                .Where(k => string.CompareOrdinal(k, oldestKey) < 0)
                .ToList();
            foreach (string key in oldKeys)
            {
                _container.Values.Remove(key);
            }
        }

        static string ToKey(DateTime date)
        {
            return date.ToString(KeyFormat, CultureInfo.InvariantCulture);
        }
    }
}
