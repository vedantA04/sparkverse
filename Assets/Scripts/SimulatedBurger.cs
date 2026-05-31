using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Text;
using System.Collections.Concurrent; // Required for thread-safe queues

public class SimulatedBurger : MonoBehaviour
{
    [Header("MQTT Settings")]
    public string brokerIP = "127.0.0.1";
    public string robotName = "simulated1";
    
    [Header("Starting Position (Meters)")]
    public float startX = 0f;
    public float startY = 0f;

    private MqttClient client;
    private float currentLinearVel = 0f;
    private float currentAngularVel = 0f;

    // Thread-safe queue for incoming MQTT messages
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    
    // Safety timeout
    private float lastCommandTime = 0f;
    private float commandTimeout = 0.5f; // Brake if no command received for 0.5 seconds

    // Pure mathematical state
    private float simX;
    private float simY;
    private float simTheta;

    void Start()
    {
        simX = startX;
        simY = startY;
        simTheta = 0f;

        client = new MqttClient(brokerIP);
        client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
        string clientId = System.Guid.NewGuid().ToString() + "_sim_" + robotName;
        client.Connect(clientId);

        client.Subscribe(new string[] { $"turtle_verse/{robotName}/cmd_vel" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        
        InvokeRepeating("PublishPose", 0.1f, 0.05f); 
    }

    // Runs on a BACKGROUND thread - Keep this extremely lightweight
    private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string jsonMsg = Encoding.UTF8.GetString(e.Message);
        messageQueue.Enqueue(jsonMsg); // Safely push to the queue
    }

    // Runs on Unity's MAIN thread - Safe to use JsonUtility
    void Update()
    {
        // Process all messages that arrived since the last frame
        while (messageQueue.TryDequeue(out string jsonMsg))
        {
            TwistPayload cmd = JsonUtility.FromJson<TwistPayload>(jsonMsg);
            currentLinearVel = cmd.linear;
            currentAngularVel = cmd.angular; 
            lastCommandTime = Time.time;
        }

        // Deadman's Switch: Stop moving if connection to Python is lost
        if (Time.time - lastCommandTime > commandTimeout)
        {
            currentLinearVel = 0f;
            currentAngularVel = 0f;
        }
    }

    void FixedUpdate()
    {
        // 1. PURE KINEMATIC INTEGRATION
        simX += currentLinearVel * Mathf.Cos(simTheta) * Time.fixedDeltaTime;
        simY += currentLinearVel * Mathf.Sin(simTheta) * Time.fixedDeltaTime;
        simTheta += currentAngularVel * Time.fixedDeltaTime;

        if (simTheta > Mathf.PI) simTheta -= 2f * Mathf.PI;
        if (simTheta < -Mathf.PI) simTheta += 2f * Mathf.PI;

        // 2. VISUAL MAPPING 
        transform.position = new Vector3(simX, transform.position.y, simY);
        transform.rotation = Quaternion.Euler(0, 90f - (simTheta * Mathf.Rad2Deg), 0);
    }

    void PublishPose()
    {
        PosePayload pose = new PosePayload();
        pose.x = simX;
        pose.y = simY;
        pose.theta = simTheta;

        string jsonPose = JsonUtility.ToJson(pose);
        client.Publish($"turtle_verse/{robotName}/pose", Encoding.UTF8.GetBytes(jsonPose));
    }

    void OnApplicationQuit()
    {
        if (client != null && client.IsConnected) client.Disconnect();
    }
}