using System;

namespace Commands
{
    public record ConfigCommand : ICommand
    {
        public string ConfigType { get; private set; }
        public float? Left { get; private set; }
        public float? Right { get; private set; }
        public int? Speed { get; private set; }
        public int? DurationMs { get; private set; }

        public ConfigCommand(string configType, float? left = null, float? right = null, int? speed = null, int? durationMs = null)
        {
            if (string.IsNullOrEmpty(configType))
            {
                throw new ArgumentException("Config type must not be null or empty.", nameof(configType));
            }

            ConfigType = configType;
            Left = left;
            Right = right;
            Speed = speed;
            DurationMs = durationMs;
        }

        public override string ToString()
        {
            return ConfigType switch
            {
                "calibration" => $"Config calibration - LEFT: {Left:F2}, RIGHT: {Right:F2}",
                "drivekick" => $"Config drivekick - speed: {Speed}, duration: {DurationMs}ms",
                "turnkick" => $"Config turnkick - speed: {Speed}, duration: {DurationMs}ms",
                "defaultspeed" => $"Config defaultspeed: {Speed}",
                _ => $"Config {ConfigType}"
            };
        }
    }
}