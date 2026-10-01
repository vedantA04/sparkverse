using Commands;
using NUnit.Framework;

namespace Tests
{
    public class MqttCommandParserTests
    {
        [Test]
        public void Parses_drive_command()
        {
            const string message = "drive forward 12 100";
            var expected = new DriveCommand(12, 100);

            var result = MqttCommandParser.ParseMessage(message);

            Assert.IsFalse(result.IsFailed);
            Assert.AreEqual(expected, result.Value);
        }

        [TestCase("left", 20, -20)]
        [TestCase("right", 20, 20)]
        public void Parses_turn_command(string direction, int speed, int expectedSpeed)
        {
            var message = $"turn {direction} {speed} 100";
            var expected = new TurnCommand(expectedSpeed, 100);

            var result = MqttCommandParser.ParseMessage(message);

            Assert.IsTrue(result.IsSuccess, result.Error);
            Assert.AreEqual(expected, result.Value);
        }

        [TestCase("rotate to 123", 123, 15, 10, 300, 5)]
        [TestCase("rotate to 123 11 12 100 10", 123, 11, 12, 100, 10)]
        public void Parses_rotate_command(
            string message,
            int expectedAngle,
            int expectedSpeed,
            int expectedStep,
            int expectedDelay,
            int expectedTolerance
        )
        {
            var expected = new RotateCommand(
                expectedAngle,
                expectedSpeed,
                expectedStep,
                expectedDelay,
                expectedTolerance
            );

            var result = MqttCommandParser.ParseMessage(message);

            Assert.IsTrue(result.IsSuccess, result.Error);
            Assert.AreEqual(expected, result.Value);
        }

        [TestCase("config set drivekick 63 500", "drivekick", null, null, 63, 500)]
        [TestCase("config set calibration 2.0 1.0", "calibration", 2.0f, 1.0f, null, null)]
        public void Parses_config_command(
            string message,
            string expectedType,
            float? expectedLeft,
            float? expectedRight,
            int? expectedSpeed,
            int? expectedDurationMs
        )
        {
            var expected = new ConfigCommand(
                expectedType,
                expectedLeft,
                expectedRight,
                expectedSpeed,
                expectedDurationMs
            );

            var result = MqttCommandParser.ParseMessage(message);

            Assert.IsTrue(result.IsSuccess, result.Error);
            Assert.AreEqual(expected, result.Value);
        }

        [Test]
        public void Gets_id_from_topic()
        {
            var result = MqttCommandParser.ParseTopic("arena/sparknode1001/cmd");

            Assert.IsTrue(result.IsSuccess, result.Error);
            Assert.AreEqual(new Individual(1001), result.Value);
        }

        [Test]
        public void Invalid_topic_is_error()
        {
            var result = MqttCommandParser.ParseTopic("not/a_topic");

            Assert.IsTrue(result.IsFailed);
        }
    }
}
