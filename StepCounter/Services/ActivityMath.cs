using System;
using System.Collections.Generic;
using System.Linq;
using StepCounter.Data;

namespace StepCounter.Services
{
    /// <summary>Расстояние, калории и тренировки.</summary>
    public static class ActivityMath
    {
        // При беге шаг длиннее, а расход энергии на километр выше.
        const double RunningStrideFactor = 1.3;
        const double WalkingKcalPerKgPerKm = 0.5;
        const double RunningKcalPerKgPerKm = 1.0;

        /// <summary>Перерыв, после которого начинается новая тренировка.</summary>
        static readonly TimeSpan WorkoutMaxGap = TimeSpan.FromMinutes(2);

        /// <summary>Сколько нужно двигаться без перерыва, чтобы это засчиталось как тренировка.</summary>
        public static readonly TimeSpan WorkoutMinDuration = TimeSpan.FromMinutes(10);

        public static double DistanceKm(int walkingSteps, int runningSteps)
        {
            double stepKm = AppSettings.StepLengthCm / 100000.0;
            return (walkingSteps + runningSteps * RunningStrideFactor) * stepKm;
        }

        public static double Kcal(int walkingSteps, int runningSteps)
        {
            double stepKm = AppSettings.StepLengthCm / 100000.0;
            double walkingKm = walkingSteps * stepKm;
            double runningKm = runningSteps * stepKm * RunningStrideFactor;
            return AppSettings.WeightKg * (walkingKm * WalkingKcalPerKgPerKm + runningKm * RunningKcalPerKgPerKm);
        }

        /// <summary>
        /// Тренировка — непрерывная активность не короче <see cref="WorkoutMinDuration"/>,
        /// перерывы внутри не длиннее <see cref="WorkoutMaxGap"/>.
        /// </summary>
        public static int CountWorkouts(IEnumerable<ActivitySegment> segments)
        {
            int workouts = 0;
            bool hasSession = false;
            long sessionEnd = 0;
            long sessionActive = 0;

            foreach (ActivitySegment segment in segments.OrderBy(s => s.EndTicks))
            {
                long start = segment.EndTicks - segment.ActiveTicks;
                if (hasSession && start - sessionEnd <= WorkoutMaxGap.Ticks)
                {
                    sessionActive += segment.ActiveTicks;
                    sessionEnd = Math.Max(sessionEnd, segment.EndTicks);
                    continue;
                }

                if (hasSession && sessionActive >= WorkoutMinDuration.Ticks)
                {
                    workouts++;
                }
                hasSession = true;
                sessionEnd = segment.EndTicks;
                sessionActive = segment.ActiveTicks;
            }

            if (hasSession && sessionActive >= WorkoutMinDuration.Ticks)
            {
                workouts++;
            }
            return workouts;
        }
    }
}
