// ================== DIGITAL TWIN MQTT CONTROLLER (HYBRID: INDIVIDUAL + BROADCAST) ==================
// Receives MQTT commands from Python and controls digital twin robots
// Uses M2MQTT library - extends M2MqttUnityClient
// 
// TOPICS:
//   - arena/sparknode1001/cmd → Control only robot 1001
//   - arena/sparknode1002/cmd → Control only robot 1002
//   - arena/all/cmd → Control ALL robots
//
// SPARKNODE NAMING: sparknode1001, sparknode1002, etc. (ID = 1000 + sparknode number)
// 
// Command format: "drive forward speed duration" / "turn left speed duration" / "rotate angle speed" / "stop"
// Attach to empty GameObject called "MQTTRobotController"

using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using M2MqttUnity;
using uPLibrary.Networking.M2Mqtt.Messages;

public class DigitalTwinMQTTController : M2MqttUnityClient
{
    [Header("MQTT Configuration")]
    [SerializeField] private string mqttTopicPattern = "arena/+/cmd";  // Wildcard subscription
    
    [Header("Robot Control")]
    [SerializeField] private SimulationManager simulationManager;
    [SerializeField] private bool autoAssignRobotsOnDemand = true;
    
    private Dictionary<int, SimulationManager.DigitalTwin> robotCache = new Dictionary<int, SimulationManager.DigitalTwin>();

    [Header("Speed Mapping")]
    [SerializeField] private float maxLinearSpeed = 0.3f;      // m/s or units/s
    [SerializeField] private float maxAngularSpeedDeg = 90f;   // deg/s
    [SerializeField] private float slope = 0.0152f; // For kinematic model
    [SerializeField] private float constant = -0.1051f; // For kinematic model
    [SerializeField] private float wheel_to_wheel = 0.165f; // For kinematic model
    private string overrideBrokerAddress = "";


    // ==================== MQTT CONNECTION ====================

    protected override void SubscribeTopics()
    {
        if (client == null) return;

        // Subscribe to wildcard: catches both "arena/sparknode1001/cmd" AND "arena/all/cmd"
        Debug.Log($"[MQTT] Subscribing to wildcard topic: {mqttTopicPattern}");
        
        try
        {
            client.Subscribe(
                new string[] { mqttTopicPattern },
                new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE }
            );
            Debug.Log($"[MQTT] Successfully subscribed to {mqttTopicPattern}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MQTT] Failed to subscribe: {e.Message}");
        }
    }

    protected override void UnsubscribeTopics()
    {
        if (client == null) return;

        try
        {
            client.Unsubscribe(new string[] { mqttTopicPattern });
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MQTT] Failed to unsubscribe: {e.Message}");
        }
    }

    protected override void OnConnected()
    {
        base.OnConnected();
        Debug.Log($"[MQTT] Connected! Ready to receive commands");
        SubscribeTopics();
    }

    protected override void OnConnectionFailed(string errorMessage)
    {
        base.OnConnectionFailed(errorMessage);
        Debug.LogError($"[MQTT] Connection failed: {errorMessage}");
    }

    protected override void OnDisconnected()
    {
        base.OnDisconnected();
        Debug.Log("[MQTT] Disconnected from broker");
    }

    protected override void OnConnectionLost()
    {
        base.OnConnectionLost();
        Debug.LogWarning("[MQTT] Connection lost!");
    }


    // ==================== MESSAGE DECODING ====================

    protected override void DecodeMessage(string topic, byte[] message)
    {
        string msg = System.Text.Encoding.UTF8.GetString(message);
        Debug.Log($"[MQTT] Received on {topic}: {msg}");
        
        // Parse topic and execute command
        ParseTopicAndExecute(topic, msg);
    }

    private void ParseTopicAndExecute(string topic, string command)
    {
        // Topic format: arena/sparknode1001/cmd or arena/all/cmd
        string[] topicParts = topic.Split('/');
        
        if (topicParts.Length < 3)
        {
            Debug.LogWarning($"[MQTT] Invalid topic format: {topic}");
            return;
        }

        string identifier = topicParts[1];  // "sparknode1001" or "all"

        List<int> targetRobotIds = new List<int>();

        if (identifier == "all")
        {
            // Broadcast: execute on ALL robots
            targetRobotIds.AddRange(GetAllRobotIds());
            Debug.Log($"[MQTT] Broadcast mode - targeting {targetRobotIds.Count} robots");
        }
        else if (identifier.StartsWith("sparknode"))
        {
            // Individual: extract robot ID from sparknode1001 -> 1001
            if (int.TryParse(identifier.Substring(9), out int robotId))  // "sparknode" = 9 chars
            {
                targetRobotIds.Add(robotId);
                Debug.Log($"[MQTT] Individual mode - targeting robot {robotId}");
            }
            else
            {
                Debug.LogWarning($"[MQTT] Could not parse robot ID from: {identifier}");
                return;
            }
        }
        else
        {
            Debug.LogWarning($"[MQTT] Unknown identifier: {identifier}");
            return;
        }

        // Execute command on target robots
        ExecuteCommandOnRobots(targetRobotIds, command);
    }

    private List<int> GetAllRobotIds()
    {
        RefreshRobotCache();
        return new List<int>(robotCache.Keys);
    }

    private void RefreshRobotCache()
    {
        if (simulationManager == null) return;

        var digitalTwinField = typeof(SimulationManager).GetField("digitalTwins", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (digitalTwinField == null) return;
        
        var digitalTwins = digitalTwinField.GetValue(simulationManager) as Dictionary<int, SimulationManager.DigitalTwin>;
        if (digitalTwins == null) return;

        robotCache.Clear();
        foreach (var kvp in digitalTwins)
        {
            robotCache[kvp.Key] = kvp.Value;
        }
    }


    // ==================== COMMAND EXECUTION ====================

    private void ExecuteCommandOnRobots(List<int> robotIds, string command)
    {
        if (robotIds.Count == 0)
        {
            Debug.LogWarning("[MQTT] No target robots found");
            return;
        }

        // Parse command format
        string[] parts = command.Split(' ');
        if (parts.Length < 1)
        {
            Debug.LogWarning($"[MQTT] Invalid command format: {command}");
            return;
        }

        string action = parts[0].ToLower();
        
        Debug.Log($"[MQTT] Executing action '{action}' on {robotIds.Count} robot(s)");

        // Execute command on each target robot
        foreach (int robotId in robotIds)
        {
            ExecuteCommandForRobot(robotId, action, parts);
        }
    }

    private void ExecuteCommandForRobot(int robotId, string action, string[] parts)
    {
        switch (action)
        {
            case "drive":
                if (parts.Length >= 4 && int.TryParse(parts[2], out int driveSpeed) && int.TryParse(parts[3], out int driveDuration))
                {
                    string direction = parts[1].ToLower();
                    ExecuteDrive(robotId, direction, driveSpeed, driveDuration);
                }
                else
                {
                    Debug.LogWarning($"[MQTT] Invalid drive command format: {string.Join(" ", parts)}");
                }
                break;

            case "turn":
                if (parts.Length >= 4 && int.TryParse(parts[2], out int turnSpeed) && int.TryParse(parts[3], out int turnDuration))
                {
                    string direction = parts[1].ToLower();
                    ExecuteTurn(robotId, direction, turnSpeed, turnDuration);
                }
                else
                {
                    Debug.LogWarning($"[MQTT] Invalid turn command format: {string.Join(" ", parts)}");
                }
                break;

            case "rotate":
                if (parts.Length >= 4 && int.TryParse(parts[1], out int angle) && int.TryParse(parts[2], out int rotSpeed))
                {
                    ExecuteRotate(robotId, angle, rotSpeed);
                }
                else
                {
                    Debug.LogWarning($"[MQTT] Invalid rotate command format: {string.Join(" ", parts)}");
                }
                break;

            case "stop":
                ExecuteStop(robotId);
                break;

            case "config":
                if (parts.Length >= 3 && parts[1].ToLower() == "set")
                {
                    ExecuteConfig(robotId, parts);
                }
                else
                {
                    Debug.LogWarning($"[MQTT] Invalid config command format: {string.Join(" ", parts)}");
                }
                break;

            default:
                Debug.LogWarning($"[MQTT] Unknown action: {action}");
                break;
        }
    }


    // ==================== MOVEMENT COMMANDS ====================

    private void ExecuteDrive(int robotId, string direction, int speed, int durationMs)
    {
        RefreshRobotCache();
        
        if (!robotCache.TryGetValue(robotId, out var twin))
        {
            Debug.LogWarning($"[MQTT] Robot {robotId} not found in cache");
            return;
        }

        float normalizedSpeed = speed * slope + constant;
        Vector3 moveDirection = direction == "reverse" ? -twin.transform.forward : twin.transform.forward;

        twin.rigidbody.linearVelocity = moveDirection * normalizedSpeed + Vector3.up * twin.rigidbody.linearVelocity.y;

        Debug.Log($"[ROBOT {robotId}] DRIVE {direction.ToUpper()} - speed: {normalizedSpeed:F2} m/s for {durationMs}ms");

        StartCoroutine(ScheduleStopAfter(robotId, durationMs / 1000f));
    }

    private void ExecuteTurn(int robotId, string direction, int speed, int durationMs)
    {
        RefreshRobotCache();
        
        if (!robotCache.TryGetValue(robotId, out var twin))
        {
            Debug.LogWarning($"[MQTT] Robot {robotId} not found in cache");
            return;
        }

        float rotationSpeed = (speed * slope + constant) / wheel_to_wheel;
        if (direction == "left") rotationSpeed = -rotationSpeed;

        twin.rigidbody.angularVelocity = new Vector3(0, rotationSpeed * Mathf.Deg2Rad, 0);

        Debug.Log($"[ROBOT {robotId}] TURN {direction.ToUpper()} - speed: {rotationSpeed:F1}°/s for {durationMs}ms");

        StartCoroutine(ScheduleStopAfter(robotId, durationMs / 1000f));
    }

    private void ExecuteRotate(int robotId, int targetAngle, int rotSpeed)
    {
        RefreshRobotCache();
        
        if (!robotCache.TryGetValue(robotId, out var twin))
        {
            Debug.LogWarning($"[MQTT] Robot {robotId} not found in cache");
            return;
        }

        StartCoroutine(RotateToAngle(twin, targetAngle, rotSpeed));
    }

    private void ExecuteStop(int robotId)
    {
        RefreshRobotCache();
        
        if (!robotCache.TryGetValue(robotId, out var twin))
        {
            Debug.LogWarning($"[MQTT] Robot {robotId} not found in cache");
            return;
        }

        twin.rigidbody.linearVelocity = new Vector3(0, twin.rigidbody.linearVelocity.y, 0);
        twin.rigidbody.angularVelocity = Vector3.zero;

        Debug.Log($"[ROBOT {robotId}] STOP");
    }


    // ==================== CONFIGURATION COMMANDS ====================

    private void ExecuteConfig(int robotId, string[] parts)
    {
        if (parts.Length < 3) return;

        string configType = parts[2].ToLower();

        switch (configType)
        {
            case "calibration":
                if (parts.Length >= 5 && float.TryParse(parts[3], out float left) && float.TryParse(parts[4], out float right))
                {
                    Debug.Log($"[ROBOT {robotId}] CONFIG calibration - LEFT: {left:F2}, RIGHT: {right:F2}");
                }
                break;

            case "drivekick":
                if (parts.Length >= 5 && int.TryParse(parts[3], out int kickSpeed) && int.TryParse(parts[4], out int kickDuration))
                {
                    Debug.Log($"[ROBOT {robotId}] CONFIG drivekick - speed: {kickSpeed}, duration: {kickDuration}ms");
                }
                break;

            case "turnkick":
                if (parts.Length >= 5 && int.TryParse(parts[3], out int turnKickSpeed) && int.TryParse(parts[4], out int turnKickDuration))
                {
                    Debug.Log($"[ROBOT {robotId}] CONFIG turnkick - speed: {turnKickSpeed}, duration: {turnKickDuration}ms");
                }
                break;

            case "defaultspeed":
                if (parts.Length >= 4 && int.TryParse(parts[3], out int defaultSpeed))
                {
                    Debug.Log($"[ROBOT {robotId}] CONFIG defaultspeed: {defaultSpeed}");
                }
                break;

            default:
                Debug.LogWarning($"[MQTT] Unknown config type: {configType}");
                break;
        }
    }


    // ==================== COROUTINES ====================

    private System.Collections.IEnumerator ScheduleStopAfter(int robotId, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        ExecuteStop(robotId);
    }

    private System.Collections.IEnumerator RotateToAngle(SimulationManager.DigitalTwin twin, int targetAngle, int rotSpeed)
    {
        float rotationSpeed = Mathf.Clamp01(rotSpeed / 63f) * maxAngularSpeedDeg;
        float tolerance = 5f;

        while (true)
        {
            float currentYaw = twin.transform.eulerAngles.y;
            float angleDifference = Mathf.DeltaAngle(currentYaw, targetAngle);

            if (Mathf.Abs(angleDifference) < tolerance)
            {
                twin.rigidbody.angularVelocity = Vector3.zero;
                Debug.Log($"[ROBOT {twin.id}] Rotation complete - target: {targetAngle}°, current: {currentYaw:F1}°");
                break;
            }

            float direction = angleDifference > 0 ? 1 : -1;
            twin.rigidbody.angularVelocity = new Vector3(0, direction * rotationSpeed * Mathf.Deg2Rad, 0);

            yield return new WaitForSeconds(0.1f);
        }
    }


    // ==================== INITIALIZATION ====================

    // protected override void Start()
    // {
    //     if (simulationManager == null)
    //     {
    //         simulationManager = FindObjectOfType<SimulationManager>();
    //     }

    //     // Cache robots on startup
    //     RefreshRobotCache();
        
    //     base.Start();
    // }
    protected override void Start()
    {
        // Check if broker address was set from StartScreen
        if (!string.IsNullOrEmpty(StartScreenManager.LastBrokerAddress))
        {
            overrideBrokerAddress = StartScreenManager.LastBrokerAddress;
            Debug.Log($"[MQTT] Broker address from StartScreen: {overrideBrokerAddress}");
            
            // Parse and set broker address
            SetBrokerAddress(overrideBrokerAddress);
        }

        if (simulationManager == null)
        {
            simulationManager = FindObjectOfType<SimulationManager>();
        }

        // Cache robots on startup
        RefreshRobotCache();

        // Call base Start() which handles MQTT connection
        base.Start();
    }

    public void SetBrokerAddress(string brokerID)
    {
        if (string.IsNullOrEmpty(brokerID))
        {
            Debug.LogWarning("[MQTT] Broker address is empty, using default");
            return;
        }

        // Parse broker address
        string[] parts = brokerID.Split(':');
        
        if (parts.Length == 2 && int.TryParse(parts[1], out int port))
        {
            // Format: "address:port"
            brokerAddress = parts[0];
            brokerPort = port;
            Debug.Log($"[MQTT] Broker set to {brokerAddress}:{brokerPort}");
        }
        else if (parts.Length == 1)
        {
            // Format: "address" only (use default port)
            brokerAddress = brokerID;
            // Keep default port (usually 1883)
            Debug.Log($"[MQTT] Broker set to {brokerAddress} (default port)");
        }
        else
        {
            Debug.LogWarning($"[MQTT] Invalid broker format: {brokerID}. Expected 'address:port' or 'address'");
        }
    }


    public string GetBrokerConfig()
    {
        return $"Broker: {brokerAddress}:{brokerPort}";
    }

}
