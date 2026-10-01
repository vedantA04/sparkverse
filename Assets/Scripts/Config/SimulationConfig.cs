// ================== SIMULATION CONFIG (DATA STRUCTURES) ==================
// Pure data classes for JSON deserialization - NO MonoBehaviour
// Place this in Assets/Scripts/Config/SimulationConfig.cs

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[System.Serializable]
public class RobotConfigData
{
    public int id;
    public string name;
    public string prefab;
    public float x;
    public float y;
}

[System.Serializable]
public class ArenaConfigData
{
    public float sizeX = 3.0f;
    public float sizeY = 3.0f;
    public float originX = 0.0f;
    public float originY = 0.0f;
}

[System.Serializable]
public class MqttConfigData
{
    public bool enablePublishPoses = true;
    public float posePublishInterval = 0.5f;
    public bool enableTrackPoses = false;
}

[System.Serializable]
public class SimulationConfig
{
    public List<RobotConfigData> robots = new List<RobotConfigData>();
    public ArenaConfigData arena = new ArenaConfigData();
    public MqttConfigData mqtt = new MqttConfigData();
    public bool enableBoidBehaviour;

    /// <summary>
    /// Load config from JSON string
    /// </summary>
    public static SimulationConfig FromJson(string jsonString)
    {
        try
        {
            return JsonConvert.DeserializeObject<SimulationConfig>(jsonString);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[CONFIG] Failed to parse JSON: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Load config from file path
    /// </summary>
    public static SimulationConfig FromFile(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                UnityEngine.Debug.LogError($"[CONFIG] File not found: {filePath}");
                return null;
            }

            string json = System.IO.File.ReadAllText(filePath);
            return FromJson(json);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[CONFIG] Failed to load file: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validate config - check for required fields
    /// </summary>
    public bool IsValid(out string errorMessage)
    {
        errorMessage = "";

        if (robots == null)
        {
            errorMessage = "Robots is undefined in config";
            return false;
        }

        foreach (var robot in robots)
        {
            if (robot.id <= 0)
            {
                errorMessage = $"Robot must have positive ID, got: {robot.id}";
                return false;
            }

            if (string.IsNullOrEmpty(robot.name))
            {
                errorMessage = $"Robot {robot.id} must have a name";
                return false;
            }
        }

        if (arena == null)
        {
            errorMessage = "Arena configuration missing";
            return false;
        }

        if (arena.sizeX <= 0 || arena.sizeY <= 0)
        {
            errorMessage = $"Arena size must be positive, got: {arena.sizeX}x{arena.sizeY}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Pretty print config for debugging
    /// </summary>
    public string ToDebugString()
    {
        string output = "[CONFIG] Simulation Configuration:\n";
        output += $"  Arena: {arena.sizeX}m x {arena.sizeY}m\n";
        output += $"  Origin: ({arena.originX}, {arena.originY})\n";
        output += $"  Robots: {robots.Count}\n";

        foreach (var robot in robots)
        {
            output += $"    - {robot.name} (ID: {robot.id}) at ({robot.x}, {robot.y})\n";
        }

        return output;
    }
}
