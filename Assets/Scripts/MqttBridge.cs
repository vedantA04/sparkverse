using UnityEngine;
using M2MqttUnity;
using uPLibrary.Networking.M2Mqtt.Messages;

public class MqttBridge : M2MqttUnityClient
{
    [Header("MQTT Configuration")]
    [SerializeField] private string mqttTopicPattern = "arena/+/cmd";  // Wildcard subscription
    [SerializeField] private string mqttRealPoseTopic = "arena/poses/real";

    [Header("Robot Control")]
    [SerializeField] private SimulationManager simulationManager;

    private string overrideBrokerAddress = "";


    public void Publish(string topic, string message)
    {
        if (client is not { IsConnected: true })
        {
            Debug.LogWarning("[MQTT] not connected");
            return;
        }

        client.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message));
    }

    // ==================== MQTT CONNECTION ====================

    protected override void SubscribeTopics()
    {
        if (client == null) return;

        // Subscribe to wildcard: catches both "arena/sparknode1001/cmd" AND "arena/all/cmd"
        Debug.Log($"[MQTT] Subscribing to wildcard topic: {mqttTopicPattern}");

        try
        {
client.Subscribe(
            new string[] { mqttTopicPattern, mqttRealPoseTopic },
            new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE }
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
            client.Unsubscribe(new string[] { mqttTopicPattern, mqttRealPoseTopic });
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

        if (!string.IsNullOrEmpty(mqttRealPoseTopic) && topic == mqttRealPoseTopic)
        {
            simulationManager.ProcessIncomingPoses(msg);
            return;
        }

        var parsedTopic = MqttCommandParser.ParseTopic(topic);
        var parsedCommand = MqttCommandParser.ParseMessage(msg);

        if (parsedTopic.IsFailed)
        {
            Debug.LogError($"[MQTT] Error parsing topic: {parsedTopic.Error}");
            return;
        }

        if (parsedCommand.IsFailed)
        {
            Debug.LogError($"[MQTT] Error parsing message: {parsedCommand.Error}");
            return;
        }

        Debug.Log($"[MQTT] Routing {parsedCommand.Value} to {parsedTopic.Value}");
        simulationManager.CommandRouter.RouteCommand(parsedTopic.Value, parsedCommand.Value);
    }

    // ==================== INITIALIZATION ====================

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
