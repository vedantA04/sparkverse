using System;

namespace Commands
{
    public record TurnCommand : ICommand
    {
        /// <summary>
        /// Motor speed? How does this correlate to each wheel speed? [-63, 63]
        /// </summary>
        public int Speed { get; private set; }
        public int DurationMs { get; private set; }

        public TurnCommand(int speed, int duration)
        {
            if (speed is < -63 or > 63)
            {
                throw new ArgumentException("Speed must be between -63 and 63.", nameof(speed));
            }

            Speed = speed;
            DurationMs = duration;
        }

        public override string ToString()
        {
            return  $"Turn {Speed:F1} {DurationMs} ms";
        }
    }
}
