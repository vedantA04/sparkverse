using System.Collections.Generic;
using System.Linq;

public record RobotPose(int Id, float X, float Y, float Rotation)
{
    public int Id { get; } = Id;
    public float X { get; } = X;
    public float Y { get; } = Y;
    public float Rotation { get; } = Rotation;
}

public static class MqttPoseConverter
{
    public static Result<List<RobotPose>> ParsePoses(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return Result.Fail<List<RobotPose>>("[MqttPoseParser] Empty message");
        }

        var poses = new List<RobotPose>();
        foreach (var segment in message.Split(','))
        {
            var poseResult = ParsePose(segment);
            if (poseResult.IsFailed)
            {
                return Result.Fail<List<RobotPose>>(poseResult.Error);
            }
            poses.Add(poseResult.Value);
        }

        return Result.Success(poses);
    }

    public static string ToString(IEnumerable<RobotPose> poses)
    {
        return string.Join(",", poses.Select(ToString));
    }

    private static string ToString(RobotPose pose)
    {
        return $"{pose.Id} {pose.X:0.0} {pose.Y:0.0} {pose.Rotation:0.0}";
    }

    private static Result<RobotPose> ParsePose(string segment)
    {
        var parts = segment.Split(' ');
        if (parts.Length < 4)
        {
            return Result.Fail<RobotPose>($"[MqttPoseParser] Invalid pose segment: '{segment}'");
        }

        if (!int.TryParse(parts[0], out var id))
        {
            return Result.Fail<RobotPose>($"[MqttPoseParser] Invalid robot id: '{parts[0]}'");
        }
        if (!float.TryParse(parts[1], out var x))
        {
            return Result.Fail<RobotPose>($"[MqttPoseParser] Invalid x: '{parts[1]}'");
        }
        if (!float.TryParse(parts[2], out var y))
        {
            return Result.Fail<RobotPose>($"[MqttPoseParser] Invalid y: '{parts[2]}'");
        }
        if (!float.TryParse(parts[3], out var rotation))
        {
            return Result.Fail<RobotPose>($"[MqttPoseParser] Invalid rotation: '{parts[3]}'");
        }

        return Result.Success(new RobotPose(id, x, y, rotation));
    }
}
