// ================== SIMULATION MANAGER (FIXED) ==================
// Now tracked robots Y = digitalTwin Y height
// Attach to empty GameObject called "SimulationManager"

using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Commands;
using Robots;
using Random = UnityEngine.Random;

public class SimulationManager : MonoBehaviour
{
    public readonly CommandRouter CommandRouter = new();

    [Header("References")]
    [SerializeField] private GameObject digitalTwinPrefab;
    [SerializeField] private GameObject trackedRobotPrefab;
    [SerializeField] private GameObject simpleDiffDriveRobotPrefab;
    [SerializeField] private GameObject articulationBodyPrefab;

    [Header("Spawn Configuration")]
    [SerializeField] private int autoSpawnCount = 5;
    [SerializeField] private Vector2 arenaSize = new Vector2(3f, 3f);
    [SerializeField] private Vector2 arenaOrigin = Vector2.zero;
    [SerializeField] private float minSpawnDistance = 0.5f;

    [Header("Physics & Movement")]
    [SerializeField] private float robotMassGrams = 470f;
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float maxForce = 10f;

    [Header("Boid Behavior")]
    [SerializeField] private bool enableBoidBehavior = false;

    [SerializeField] private float cohesionRadius = 5f;
    [SerializeField] private float cohesionWeight = 1f;
    [SerializeField] private float separationRadius = 0.2f;
    [SerializeField] private float separationWeight = 2f;
    [SerializeField] private float alignmentRadius = 3f;
    [SerializeField] private float alignmentWeight = 1f;

    [Header("Debug")]
    [SerializeField] private bool drawBoidDebug = true;
    [SerializeField] private bool debugCollisions = true;

    private SimulationConfig Config { get; set; }
    private Dictionary<int, IDigitalTwin> digitalTwins = new Dictionary<int, IDigitalTwin>();
    private Dictionary<int, TrackedRobot> trackedRobots = new Dictionary<int, TrackedRobot>();
    private HashSet<(int, int)> collidedPairs = new HashSet<(int, int)>();

    private int nextTwinId = 1001;
    private int nextTrackedId = 2001;
    private bool boidBehaviorEnabled = false;
    private Coroutine serverCoroutine;
    private Coroutine unityRobotsCoroutine;
    private Coroutine mqttPosesCoroutine;
    private MqttBridge mqttBridge;
    private const string PoseTopicName = "arena/poses/sim";

    [System.Serializable]
    private class UnityRobotData { public int id; public float x; public float y; public float rotation; }
    [System.Serializable]
    private class UnityRobotsPayload { public UnityRobotData[] robots; }

    public void SpawnRobotsFromConfig(SimulationConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[SimulationManager] Config is null");
            return;
        }

        if (config.robots == null)
        {
            Debug.LogError("[SimulationManager] config: robots field is undefined");
            return;
        }

        // Update arena settings from config
        if (config.arena != null)
        {
            arenaSize = new Vector2(config.arena.sizeX, config.arena.sizeY);
            arenaOrigin = new Vector2(config.arena.originX, config.arena.originY);
            Debug.Log($"[SimulationManager] Arena updated: {arenaSize.x}m x {arenaSize.y}m, origin: {arenaOrigin}");
        }

        if (digitalTwinPrefab == null)
        {
            Debug.LogError("[SimulationManager] Digital Twin prefab not assigned!");
            return;
        }
        if (simpleDiffDriveRobotPrefab == null)
        {
            Debug.LogError("[SimulationManager] simpleDiffDriveRobotPrefab not assigned!");
            return;
        }

        if (articulationBodyPrefab == null)
        {
            Debug.LogError("[SimulationManager] articulationBodyPrefab not assigned!");
            return;
        }

        // Spawn each robot from config
        int spawnedCount = 0;
        foreach (var robotData in config.robots)
        {
            var spawnPos = UnityCoordMapper.ToUnity3D(robotData.x, robotData.y);

            if (IsValidSpawnPosition(spawnPos))
            {
                var twin = robotData.prefab.ToLowerInvariant() switch
                {
                    "digitaltwin" => OriginalTwin.Spawn(
                        digitalTwinPrefab,
                        spawnPos,
                        transform,
                        nextTwinId,
                        robotMassGrams,
                        arenaSize,
                        arenaOrigin,
                        this
                    ),
                    "simplediffdrivebot" => SimpleDiffTwin.Spawn(
                        simpleDiffDriveRobotPrefab,
                        spawnPos,
                        transform,
                        nextTwinId
                    ),
                    "articulationbody" => ArticulationBodyTwin.Spawn(
                        articulationBodyPrefab,
                        spawnPos,
                        transform,
                        nextTwinId),
                    _ => throw new ArgumentException($"Unsupported prefab for robot: {robotData.prefab}")
                };
                digitalTwins[nextTwinId] = twin;
                CommandRouter.RegisterReceiver(nextTwinId, twin);
                spawnedCount++;
                nextTwinId++;
                Debug.Log($"[SimulationManager] Spawned {robotData.name} (ID: {robotData.id}) at ({robotData.x}, {robotData.y})");
            }
            else
            {
                Debug.LogWarning($"[SimulationManager] Invalid spawn position for '{robotData.name}': {spawnPos}");
            }
        }

        Debug.Log($"[SimulationManager] Successfully spawned {spawnedCount}/{config.robots.Count} robots from config");
    }

    public bool ValidateConfig(SimulationConfig config, out string errorMessage)
    {
        errorMessage = "";

        if (config == null)
        {
            errorMessage = "Config is null";
            return false;
        }

        if (config.robots == null)
        {
            errorMessage = "config: robots field is undefined";
            return false;
        }

        foreach (var robot in config.robots)
        {
            if (!IsValidSpawnPosition(UnityCoordMapper.ToUnity3D(robot.x, robot.y)))
            {
                errorMessage = $"Robot {robot.name} spawn position invalid: ({robot.x}, {robot.y})";
                return false;
            }
        }

        return true;
    }

    void Start()
    {
        // Ensures background processing such as MQTT queue processing
        // keeps running while the simulation is not in the foreground
        Application.runInBackground = true;

        Debug.Log("[SimulationManager] Started");

        if (StartScreenManager.LastLoadedConfig == null)
            throw new InvalidOperationException($"[SimulationManager] config is null");

        Config = StartScreenManager.LastLoadedConfig;

        Debug.Log("[SimulationManager] Loading robots from config");
        SpawnRobotsFromConfig(Config);

        EnableBoidBehavior(enableBoidBehavior || Config.enableBoidBehaviour);

        if (Config.mqtt.enablePublishPoses)
        {
            mqttBridge = FindFirstObjectByType<MqttBridge>();
            if (mqttBridge == null)
            {
                Debug.LogWarning("[SimulationManager] MqttBridge not found - cannot publish poses over MQTT");
            }
            else
            {
                mqttPosesCoroutine = StartCoroutine(SendMqttPosesCoroutine());
            }
        }
    }

    private void FixedUpdate()
    {
        if (boidBehaviorEnabled) return;

        foreach (var twin in digitalTwins.Values)
        {
            if (twin is SimpleDiffTwin sdTwin)
            {
                sdTwin.FixedUpdate();
            }

            if (twin is ArticulationBodyTwin articulationBodyTwin)
            {
                articulationBodyTwin.FixedUpdate();
            }
        }
    }

    void Update()
    {
        if (boidBehaviorEnabled)
        {
            foreach (var twin in digitalTwins.Values)
            {
                if (twin is OriginalTwin oTwin)
                {
                    if (oTwin.controller != null)
                    {
                        oTwin.controller.UpdateBehavior(
                            digitalTwins.Values.OfType<OriginalTwin>().ToList(),
                            maxSpeed, maxForce,
                            cohesionRadius, cohesionWeight,
                            separationRadius, separationWeight,
                            alignmentRadius, alignmentWeight
                        );
                    }
                }
            }
        }

        CheckCollisions();
    }

    public ICommandableTwin SpawnArticulationBodyRobotAsTwin(Vector3 spawnPosition)
    {
        var gameObj = Instantiate(articulationBodyPrefab, spawnPosition, Quaternion.identity, transform);
        var twinId = nextTwinId++;
        gameObj.name = $"DigitalTwin_{twinId}";

        var twin = new ArticulationBodyTwin {Id =  twinId, GameObject =  gameObj};
        digitalTwins[twinId] = twin;

        Debug.Log($"[SimulationManager] Spawned Digital Twin {twinId}");
        return twin;
    }

    public void DestroyDigitalTwin(int twinId)
    {
        if (digitalTwins.TryGetValue(twinId, out var twin))
        {
            Destroy(twin.GameObject);
            digitalTwins.Remove(twinId);
        }
    }


    public void EnableBoidBehavior(bool enable)
    {
        boidBehaviorEnabled = enable;
        foreach (var twin in digitalTwins.Values)
        {
            if (twin is OriginalTwin ogTwin)
            {
                if (ogTwin.controller != null) ogTwin.controller.enabled = enable;
            }
        }
        Debug.Log($"[SimulationManager] Boid behavior {(enable ? "enabled" : "disabled")}");
    }

    private void CreateTrackedRobot(int robotId)
    {
        if (trackedRobotPrefab == null) { Debug.LogError("[SimulationManager] Tracked Robot prefab not assigned!"); return; }

        GameObject robotGO = Instantiate(trackedRobotPrefab, transform);
        robotGO.name = $"TrackedRobot_{robotId}";


        Rigidbody rb = robotGO.GetComponent<Rigidbody>();
        if (rb == null) rb = robotGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;


        BoxCollider boxCollider = robotGO.GetComponent<BoxCollider>();
        if (boxCollider == null) boxCollider = robotGO.AddComponent<BoxCollider>();
        boxCollider.isTrigger = false;


        TrackedRobot trackedRobot = new TrackedRobot(robotId, robotGO);
        trackedRobots[robotId] = trackedRobot;
    }


    private void UpdateTrackedRobotPosition(RobotPose robotPose)
    {
        if (trackedRobots.TryGetValue(robotPose.Id, out var robot))
        {
            var unityX = robotPose.X;
            var unityZ = robotPose.Y;

            robot.transform.position = new Vector3(unityX, 0f, unityZ);
            robot.transform.rotation = Quaternion.Euler(0, -robotPose.Rotation, 0);
        }
    }

    private void DestroyTrackedRobot(int robotId)
    {
        if (trackedRobots.TryGetValue(robotId, out TrackedRobot robot))
        {
            Destroy(robot.gameObject);
            trackedRobots.Remove(robotId);
        }
    }

    private void ProcessTrackedRobotData(List<RobotPose> poseList)
    {
        if (poseList == null) return;

        var activeIds = new HashSet<int>();
        foreach (var pose in poseList)
        {
            activeIds.Add(pose.Id);
            if (!trackedRobots.ContainsKey(pose.Id))
                CreateTrackedRobot(pose.Id);

            UpdateTrackedRobotPosition(pose);
        }

        var idsToRemove = trackedRobots.Keys.Where(id => !activeIds.Contains(id)).ToList();
        foreach (int id in idsToRemove)
            DestroyTrackedRobot(id);
    }

    public void ProcessIncomingPoses(string rawPoseMessage)
    {
        if (!Config.mqtt.enableTrackPoses) return;

        var parseResult = MqttPoseConverter.ParsePoses(rawPoseMessage);
        if (parseResult.IsFailed)
        {
            Debug.LogWarning($"[SimulationManager] {parseResult.Error}");
            return;
        }

        ProcessTrackedRobotData(parseResult.Value);
    }

    private IEnumerator SendMqttPosesCoroutine()
    {
        while (Config.mqtt.enablePublishPoses)
        {
            PublishMqttPoses();
            yield return new WaitForSeconds(Config.mqtt.posePublishInterval);
        }
    }

    private void PublishMqttPoses()
    {
        if (mqttBridge == null) return;

        var twins = new List<IDigitalTwin>(digitalTwins.Values);
        var robots = new List<TrackedRobot>(trackedRobots.Values);

        if (twins.Count + robots.Count == 0)
            return;

        var twinPoses = twins.Select(twin => ToPose(twin.Id, twin.Transform)).ToList();
        var trackedPoses = robots.Select(twin => ToPose(twin.id, twin.transform)).ToList();

        var posesMsg = MqttPoseConverter.ToString(twinPoses.Concat(trackedPoses));

        mqttBridge.Publish(PoseTopicName, posesMsg);
    }

    private static RobotPose ToPose(int id, Transform transform)
    {
        var (x, y) = UnityCoordMapper.ToExternalXy(transform.position);
        var rotation = transform.eulerAngles.y;
        return new RobotPose(id, x, y, rotation);
    }

    private void CheckCollisions()
    {
        // Digital twins vs digital twins
        var twinList = digitalTwins.Values.OfType<OriginalTwin>().ToList();
        for (int i = 0; i < twinList.Count; i++)
        {
            for (int j = i + 1; j < twinList.Count; j++)
            {
                if (twinList[i].boxCollider != null && twinList[j].boxCollider != null)
                {
                    if (twinList[i].boxCollider.bounds.Intersects(twinList[j].boxCollider.bounds))
                    {
                        int id1 = Mathf.Min(twinList[i].Id, twinList[j].Id);
                        int id2 = Mathf.Max(twinList[i].Id, twinList[j].Id);
                        var pair = (id1, id2);

                        if (!collidedPairs.Contains(pair))
                        {
                            if (debugCollisions) Debug.Log($"[COLLISION] DigitalTwin {id1} <-> DigitalTwin {id2}");
                            collidedPairs.Add(pair);
                        }
                    }
                }
            }
        }
        collidedPairs.Clear();


        // Digital twins vs tracked robots
        foreach (var dTwin in digitalTwins.Values.OfType<OriginalTwin>())
        {
            foreach (var tRobot in trackedRobots.Values)
            {
                if (dTwin.boxCollider != null && tRobot.boxCollider != null)
                {
                    if (dTwin.boxCollider.bounds.Intersects(tRobot.boxCollider.bounds))
                    {
                        if (debugCollisions) Debug.Log($"[COLLISION] DigitalTwin {dTwin.Id} <-> TrackedRobot {tRobot.id}");
                    }
                }
            }
        }
    }

    private bool IsValidSpawnPosition(Vector2 position)
    {
        float maxX = arenaOrigin.x + arenaSize.x / 2;
        float minX = arenaOrigin.x - arenaSize.x / 2;
        float maxY = arenaOrigin.y + arenaSize.y / 2;
        float minY = arenaOrigin.y - arenaSize.y / 2;

        if (position.x < minX || position.x > maxX || position.y < minY || position.y > maxY)
            return false;

        foreach (var twin in digitalTwins.Values)
        {
            var twin2D = UnityCoordMapper.ToExternalVec2D(twin.Transform.position);
            if (Vector2.Distance(position, twin2D) < minSpawnDistance)
                return false;
        }
        return true;
    }

    private bool IsValidSpawnPosition(Vector3 unityPosition)
    {
        return unityPosition.y == 0f && IsValidSpawnPosition(UnityCoordMapper.ToExternalVec2D(unityPosition));
    }


    private Vector2 GetRandomSpawnPosition()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float randomX = Random.Range(arenaOrigin.x - arenaSize.x / 2, arenaOrigin.x + arenaSize.x / 2);
            float randomY = Random.Range(arenaOrigin.y - arenaSize.y / 2, arenaOrigin.y + arenaSize.y / 2);
            var spawnPos = new Vector2(randomX, randomY);

            if (IsValidSpawnPosition(spawnPos)) return spawnPos;
        }
        return arenaOrigin;
    }


    public int GetDigitalTwinCount() => digitalTwins.Count;
    public int GetTrackedRobotCount() => trackedRobots.Count;


    void OnDestroy()
    {
        if (serverCoroutine != null) StopCoroutine(serverCoroutine);
        if (unityRobotsCoroutine != null) StopCoroutine(unityRobotsCoroutine);
        if (mqttPosesCoroutine != null) StopCoroutine(mqttPosesCoroutine);
    }
}
