using System;

namespace Commands
{
    public record RotateCommand : ICommand
    {
        /// <summary>
        /// Target heading in degrees (0-360)
        /// </summary>
        public int Angle { get; private set; }

        /// <summary>
        /// Fixed rotation speed (0-63)
        /// </summary>
        public int Speed { get; private set; }

        /// <summary>
        /// Duration of each rotation step in milliseconds
        /// </summary>
        public int StepMs { get; private set; }

        /// <summary>
        /// Delay between heading checks in milliseconds
        /// </summary>
        public int DelayMs { get; private set; }

        /// <summary>
        /// Acceptable heading error in degrees
        /// </summary>
        public double Tolerance { get; private set; }

        public RotateCommand(int angle, int speed = 15, int stepMs = 10, int delayMs = 300, double tolerance = 5.0)
        {
            if (angle is < 0 or > 360)
            {
                throw new ArgumentException("Angle must be between 0 and 360.", nameof(angle));
            }

            if (speed is < 0 or > 63)
            {
                throw new ArgumentException("Speed must be between 0 and 63.", nameof(speed));
            }

            if (stepMs < 5)
            {
                throw new ArgumentException("StepMs must be at least 5.", nameof(stepMs));
            }

            if (delayMs < 100)
            {
                throw new ArgumentException("DelayMs must be at least 100.", nameof(delayMs));
            }

            if (tolerance < 5.0)
            {
                throw new ArgumentException("Tolerance must be at least 5.0.", nameof(tolerance));
            }

            Angle = angle;
            Speed = speed;
            StepMs = stepMs;
            DelayMs = delayMs;
            Tolerance = tolerance;
        }
    }
}