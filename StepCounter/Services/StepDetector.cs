using System;

namespace StepCounter.Services
{
    /// <summary>
    /// Простой детектор шагов по данным акселерометра.
    /// Из модуля ускорения вычитается медленно меняющаяся составляющая (гравитация),
    /// остаток сглаживается, а шаг засчитывается при пересечении порога с гистерезисом.
    /// Чтобы не считать случайные встряхивания, шаги засчитываются только после
    /// серии из нескольких шагов с правдоподобным ритмом.
    /// </summary>
    public sealed class StepDetector
    {
        // Коэффициенты фильтров рассчитаны на частоту ~50 Гц.
        const double GravityAlpha = 0.02;
        const double SmoothAlpha = 0.35;

        // Пороги в g.
        const double HighThreshold = 0.10;
        const double LowThreshold = 0.03;

        static readonly TimeSpan MinStepInterval = TimeSpan.FromMilliseconds(250);
        static readonly TimeSpan MaxStepInterval = TimeSpan.FromMilliseconds(2000);

        // Сколько шагов подряд нужно, чтобы начать засчитывать ходьбу.
        const int StepsToConfirm = 4;

        double _gravity = double.NaN;
        double _smoothed;
        bool _armed;
        bool _hasLastStep;
        DateTimeOffset _lastStep;
        int _pendingSteps;
        bool _walking;

        /// <summary>
        /// Обрабатывает одно показание акселерометра (в g).
        /// Возвращает количество новых засчитанных шагов (обычно 0 или 1,
        /// в момент подтверждения серии — сразу несколько).
        /// </summary>
        public int AddSample(double x, double y, double z, DateTimeOffset timestamp)
        {
            double magnitude = Math.Sqrt(x * x + y * y + z * z);
            if (double.IsNaN(_gravity))
            {
                _gravity = magnitude;
                return 0;
            }

            _gravity += GravityAlpha * (magnitude - _gravity);
            _smoothed += SmoothAlpha * ((magnitude - _gravity) - _smoothed);

            if (_hasLastStep && timestamp - _lastStep > MaxStepInterval)
            {
                // Слишком долгая пауза — серия прервалась.
                _pendingSteps = 0;
                _walking = false;
            }

            if (!_armed)
            {
                if (_smoothed < LowThreshold)
                {
                    _armed = true;
                }
                return 0;
            }

            if (_smoothed < HighThreshold)
            {
                return 0;
            }

            if (_hasLastStep && timestamp - _lastStep < MinStepInterval)
            {
                return 0;
            }

            _armed = false;
            _hasLastStep = true;
            _lastStep = timestamp;

            if (_walking)
            {
                return 1;
            }

            _pendingSteps++;
            if (_pendingSteps < StepsToConfirm)
            {
                return 0;
            }

            _walking = true;
            int confirmed = _pendingSteps;
            _pendingSteps = 0;
            return confirmed;
        }

        public void Reset()
        {
            _gravity = double.NaN;
            _smoothed = 0;
            _armed = false;
            _hasLastStep = false;
            _pendingSteps = 0;
            _walking = false;
        }
    }
}
