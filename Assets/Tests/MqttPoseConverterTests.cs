using System.Linq;
using NUnit.Framework;

namespace Tests
{
    public class MqttPoseConverterTests
    {
        [Test]
        public void Parses_pose()
        {
            const string message = "123 1.1 2.2 222.2";
            var expected = new RobotPose(123, 1.1f, 2.2f, 222.2f);

            var (parsedPoses, isSuccess, _) = MqttPoseConverter.ParsePoses(message);

            Assert.IsTrue(isSuccess);
            Assert.AreEqual(1, parsedPoses.Count);
            var parsedPose = parsedPoses.Single();
            Assert.AreEqual(expected, parsedPose);
        }
    }
}
