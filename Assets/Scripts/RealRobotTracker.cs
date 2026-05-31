using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Text;
using System.Collections.Concurrent; // Required for thread-safe queues

public class RealRobotTracker : MonoBehaviour
{
    [Header("MQTT Settings")]
    public string brokerIP = "127.0.0.1";
    public string robotName = "robot1"; // e.g., robot1, robot2, robot3

    private MqttClient client;
    private float targetX = 0f;
    private float targetY = 0f;
    private float targetTheta = 0f;
    private bool hasReceivedData = false;

    // Thread-safe queue for incoming MQTT messages
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    void Start()
    {
        client = new MqttClient(brokerIP);
        client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
        
        string clientId = System.Guid.NewGuid().ToString() + "_tracker_" + robotName;
        client.Connect(clientId);

        client.Subscribe(new string[] { $"turtle_verse/{robotName}/pose" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
    }

    // Runs on a BACKGROUND thread - Keep this extremely lightweight
    private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string jsonMsg = Encoding.UTF8.GetString(e.Message);
        messageQueue.Enqueue(jsonMsg); // Safely push string to the queue
    }

    // Runs on Unity's MAIN thread - Safe to use JsonUtility
    void Update()
    {
        // 1. Process all new poses that arrived since the last frame
        while (messageQueue.TryDequeue(out string jsonMsg))
        {
            PosePayload pose = JsonUtility.FromJson<PosePayload>(jsonMsg);
            
            targetX = pose.x;
            targetY = pose.y;
            targetTheta = pose.theta;
            
            hasReceivedData = true;
        }

        // 2. Visually update the ghost robot's position
        if (hasReceivedData) 
        {
            // Exact visual mapping used by the simulated bots (Math X -> Unity X, Math Y -> Unity Z)
            Vector3 targetPosition = new Vector3(targetX, transform.position.y, targetY);
            Quaternion targetRotation = Quaternion.Euler(0, 90f - (targetTheta * Mathf.Rad2Deg), 0);
            
            // Lerp for smooth visual tracking, masking any QTM or network jitter
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 15f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
        }
    }

    void OnApplicationQuit()
    {
        if (client != null && client.IsConnected) client.Disconnect();
    }
}