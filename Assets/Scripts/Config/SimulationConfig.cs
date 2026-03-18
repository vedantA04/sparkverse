// ================== SIMULATION CONFIG (DATA STRUCTURES) ==================
// Pure data classes for JSON deserialization - NO MonoBehaviour
// Place this in Assets/Scripts/Config/SimulationConfig.cs

using System;
using System.Collections.Generic;

[System.Serializable]
public class RobotConfigData
{
    public int id;
    public string name;
    public float x;
    public float z;
}

[System.Serializable]
public class ArenaConfigData
{
    public float sizeX = 3.0f;
    public float sizeZ = 3.0f;
    public float originX = 0.0f;
    public float originY = 0.0f;
    public float originZ = 0.0f;
}

[System.Serializable]
public class SimulationConfig
{
    public List<RobotConfigData> robots = new List<RobotConfigData>();
    public ArenaConfigData arena = new ArenaConfigData();

    /// <summary>
    /// Load config from JSON string
    /// </summary>
    public static SimulationConfig FromJson(string jsonString)
    {
        try
        {
            return UnityEngine.JsonUtility.FromJson<SimulationConfig>(jsonString);
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

        if (robots == null || robots.Count == 0)
        {
            errorMessage = "No robots defined in config";
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

        if (arena.sizeX <= 0 || arena.sizeZ <= 0)
        {
            errorMessage = $"Arena size must be positive, got: {arena.sizeX}x{arena.sizeZ}";
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
        output += $"  Arena: {arena.sizeX}m x {arena.sizeZ}m\n";
        output += $"  Origin: ({arena.originX}, {arena.originY}, {arena.originZ})\n";
        output += $"  Robots: {robots.Count}\n";

        foreach (var robot in robots)
        {
            output += $"    - {robot.name} (ID: {robot.id}) at ({robot.x}, {robot.z})\n";
        }

        return output;
    }
}
