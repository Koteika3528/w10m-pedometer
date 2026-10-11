using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StepCounter.Services;
using SQLite;
using Windows.Storage;

namespace StepCounter.Data
{
    /// <summary>
    /// База SQLite с историей активности (LocalFolder\steps.db).
    /// Методы синхронные и потокобезопасные; из UI вызывайте через Task.Run.
    /// </summary>
    public sealed class StepDatabase
    {
        const string FileName = "steps.db";

        readonly SQLiteConnection _connection;
        readonly object _lock = new object();

        StepDatabase(string path)
        {
            _connection = new SQLiteConnection(path);
            _connection.CreateTable<ActivitySegment>();
            _connection.CreateTable<DaySummary>();
        }

        public static StepDatabase Open()
        {
            return new StepDatabase(Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName));
        }

        public void AddSegments(IList<ActivitySegment> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return;
            }

            lock (_lock)
            {
                _connection.RunInTransaction(() =>
                {
                    _connection.InsertAll(segments, false);
                    foreach (int day in segments.Select(s => s.Day).Distinct())
                    {
                        RecomputeDay(day);
                    }
                });
            }
        }

        public DaySummary GetDay(int day)
        {
            lock (_lock)
            {
                return _connection.Find<DaySummary>(day);
            }
        }

        public List<ActivitySegment> GetSegments(int day)
        {
            lock (_lock)
            {
                return _connection.Table<ActivitySegment>().Where(s => s.Day == day).ToList();
            }
        }

        public List<DaySummary> GetDays(int fromDay, int toDay)
        {
            lock (_lock)
            {
                return _connection.Table<DaySummary>()
                    .Where(d => d.Day >= fromDay && d.Day <= toDay)
                    .ToList();
            }
        }

        // Вызывать под _lock.
        void RecomputeDay(int day)
        {
            List<ActivitySegment> segments = _connection.Table<ActivitySegment>().Where(s => s.Day == day).ToList();
            if (segments.Count == 0)
            {
                _connection.Delete<DaySummary>(day);
                return;
            }

            _connection.InsertOrReplace(new DaySummary
            {
                Day = day,
                Steps = segments.Sum(s => s.Steps),
                RunningSteps = segments.Where(s => s.IsRunning).Sum(s => s.Steps),
                ActiveSeconds = (int)TimeSpan.FromTicks(segments.Sum(s => s.ActiveTicks)).TotalSeconds,
                Workouts = ActivityMath.CountWorkouts(segments),
            });
        }
    }
}
