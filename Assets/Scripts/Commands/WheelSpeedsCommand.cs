using System;

namespace Commands
{
    public record WheelSpeedsCommand : ICommand
    {
        /// <summary>
        /// Left wheel motor speed, [-63, 63]
        /// </summary>
        public int LeftSpeed { get; private set; }

        /// <summary>
        /// Right wheel motor speed, [-63, 63]
        /// </summary>
        public int RightSpeed { get; private set; }

        public int DurationMs { get; private set; }

        public WheelSpeedsCommand(int leftSpeed, int rightSpeed, int durationMs = 1000)
        {
            if (leftSpeed is < -63 or > 63)
            {
                throw new ArgumentException("LeftSpeed must be between -63 and 63.", nameof(leftSpeed));
            }

            if (rightSpeed is < -63 or > 63)
            {
                throw new ArgumentException("RightSpeed must be between -63 and 63.", nameof(rightSpeed));
            }

            if (durationMs < 0)
            {
                throw new ArgumentException("DurationMs must not be negative.", nameof(durationMs));
            }

            LeftSpeed = leftSpeed;
            RightSpeed = rightSpeed;
            DurationMs = durationMs;
        }

        public override string ToString()
        {
            return $"Wheels {LeftSpeed} {RightSpeed} {DurationMs} ms";
        }
    }
}
