using System;
using Commands;

public interface IAddresee
{
}

public record Broadcast : IAddresee
{
}

public record Individual(int Id) : IAddresee
{
    public int Id { get; } = Id;
}

public static class MqttCommandParser
{
    public static Result<ICommand> ParseMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return Result.Fail<ICommand>("[MqttCommandParser] Empty message");
        }

        var parts = message.Split(' ');
        if (parts.Length < 1)
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] Invalid command format: {message}");
        }

        var action = parts[0].ToLower();

        return action switch
        {
            "drive" => ParseDrive(parts),
            "turn" => ParseTurn(parts),
            "rotate" => ParseRotate(parts),
            "stop" => Result.Success<ICommand>(new StopCommand()),
            "config" => ParseConfig(parts),
            _ => Result.Fail<ICommand>($"Unknown action {action}")
        };
    }

    private static Result<ICommand> ParseDrive(string[] parts)
    {
        if (parts.Length < 4)
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] Invalid drive command format: {string.Join(" ", parts)}");
        }

        var direction = parts[1].ToLower();
        if (!int.TryParse(parts[2], out var speed) || !int.TryParse(parts[3], out var durationMs))
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] Invalid drive command params: {string.Join(" ", parts)}");
        }

        return direction switch
        {
            "reverse" => Result.Success<ICommand>(new DriveCommand(-speed, durationMs)),
            "forward" => Result.Success<ICommand>(new DriveCommand(speed, durationMs)),
            _ => Result.Fail<ICommand>("Invalid direction: " + direction)
        };
    }

    private static Result<ICommand> ParseTurn(string[] parts)
    {
        if (parts.Length >= 4 && int.TryParse(parts[2], out var turnSpeed) && int.TryParse(parts[3], out var turnDuration))
        {
            var direction = parts[1].ToLower();
            return direction switch
            {
                "left" => Result.Success<ICommand>(new TurnCommand(-turnSpeed, turnDuration)),
                "right" => Result.Success<ICommand>(new TurnCommand(turnSpeed, turnDuration)),
                _ => Result.Fail<ICommand>($"Unknown direction: {direction}")
            };
        }

        return Result.Fail<ICommand>($"[MQTT] Invalid turn command format: {string.Join(" ", parts)}");
    }

    private static Result<ICommand> ParseRotate(string[] parts)
    {
        if (parts.Length < 3 || parts[1].ToLower() != "to")
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] Invalid rotate command format: {string.Join(" ", parts)}");
        }

        if (!int.TryParse(parts[2], out var angle))
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] Invalid rotate angle: {parts[2]}");
        }

        var speed = 15;
        if (parts.Length >= 4 && !int.TryParse(parts[3], out speed))
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] Invalid rotate speed: {parts[3]}");
        }

        var stepMs = 10;
        if (parts.Length >= 5 && !int.TryParse(parts[4], out stepMs))
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] Invalid rotate step_ms: {parts[4]}");
        }

        var delayMs = 300;
        if (parts.Length >= 6 && !int.TryParse(parts[5], out delayMs))
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] Invalid rotate delay_ms: {parts[5]}");
        }

        var tolerance = 5.0;
        if (parts.Length >= 7 && !double.TryParse(parts[6], out tolerance))
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] Invalid rotate tolerance: {parts[6]}");
        }

        try
        {
            return Result.Success<ICommand>(new RotateCommand(angle, speed, stepMs, delayMs, tolerance));
        }
        catch (ArgumentException ex)
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] {ex.Message}");
        }
    }

    private static Result<ICommand> ParseConfig(string[] parts)
    {
        if (parts.Length < 3 || parts[1].ToLower() != "set")
        {
            return Result.Fail<ICommand>($"[MqttCommandParser] Invalid config command format: {string.Join(" ", parts)}");
        }

        var configType = parts[2].ToLower();

        switch (configType)
        {
            case "calibration":
                if (parts.Length >= 5 && float.TryParse(parts[3], out var left) && float.TryParse(parts[4], out var right))
                {
                    return Result.Success<ICommand>(new ConfigCommand(configType, left: left, right: right));
                }
                return Result.Fail<ICommand>($"[MqttCommandParser] Invalid calibration params: {string.Join(" ", parts)}");

            case "drivekick":
                if (parts.Length >= 5 && int.TryParse(parts[3], out var kickSpeed) && int.TryParse(parts[4], out var kickDuration))
                {
                    return Result.Success<ICommand>(new ConfigCommand(configType, speed: kickSpeed, durationMs: kickDuration));
                }
                return Result.Fail<ICommand>($"[MqttCommandParser] Invalid drivekick params: {string.Join(" ", parts)}");

            case "turnkick":
                if (parts.Length >= 5 && int.TryParse(parts[3], out var turnKickSpeed) && int.TryParse(parts[4], out var turnKickDuration))
                {
                    return Result.Success<ICommand>(new ConfigCommand(configType, speed: turnKickSpeed, durationMs: turnKickDuration));
                }
                return Result.Fail<ICommand>($"[MqttCommandParser] Invalid turnkick params: {string.Join(" ", parts)}");

            case "defaultspeed":
                if (parts.Length >= 4 && int.TryParse(parts[3], out var defaultSpeed))
                {
                    return Result.Success<ICommand>(new ConfigCommand(configType, speed: defaultSpeed));
                }
                return Result.Fail<ICommand>($"[MqttCommandParser] Invalid defaultspeed params: {string.Join(" ", parts)}");

            default:
                return Result.Fail<ICommand>($"[MqttCommandParser] Unknown config type: {configType}");
        }
    }

    public static Result<IAddresee> ParseTopic(string topic)
    {
        var topicParts = topic.Split('/');

        if (topicParts.Length < 3)
        {
            return Result.Fail<IAddresee>($"Invalid topic: {topic}");
        }

        var identifier = topicParts[1];

        if (identifier == "all")
        {
            return Result.Success<IAddresee>(new Broadcast());
        }

        if (identifier.StartsWith("sparknode"))
        {
            if (int.TryParse(identifier[9..], out var robotId))
            {
                return Result.Success<IAddresee>(new Individual(robotId));
            }

            return Result.Fail<IAddresee>($"Invalid robot id: {identifier}");
        }

        return Result.Fail<IAddresee>($"Unknown identifier: {identifier}");
    }
}
