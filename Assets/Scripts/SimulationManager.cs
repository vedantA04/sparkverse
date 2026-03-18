// ================== SIMULATION MANAGER (FIXED) ==================
// Now tracked robots Y = digitalTwin Y height
// Attach to empty GameObject called "SimulationManager"

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;


public class SimulationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject digitalTwinPrefab;
    [SerializeField] private GameObject trackedRobotPrefab;
    
    [Header("Spawn Configuration")]
    [SerializeField] private int autoSpawnCount = 5;
    [SerializeField] private Vector2 arenaSize = new Vector2(3f, 3f);
    [SerializeField] private Vector3 arenaOrigin = Vector3.zero;
    [SerializeField] private float minSpawnDistance = 0.5f;
    
    [Header("Physics & Movement")]
    [SerializeField] private float robotMassGrams = 470f;
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float maxForce = 10f;

    [Header("Unity Robots Output (Outgoing - Digital Twins for Feedback Loop)")]
    [SerializeField] private string unityRobotsServerURL = "http://localhost:8001";
    [SerializeField] private float unityRobotsUpdateInterval = 0.0625f; // ~16Hz
    [SerializeField] private bool enableUnityRobotsTracking = true;

    
    [Header("Boid Behavior")]
    [SerializeField] private bool enableBoidBehavior = false;

    [SerializeField] private float cohesionRadius = 5f;
    [SerializeField] private float cohesionWeight = 1f;
    [SerializeField] private float separationRadius = 0.2f;
    [SerializeField] private float separationWeight = 2f;
    [SerializeField] private float alignmentRadius = 3f;
    [SerializeField] private float alignmentWeight = 1f;
    
    [Header("Server Tracking (Optional)")]
    [SerializeField] private string pythonServerURL = "http://localhost:8000";
    [SerializeField] private float serverUpdateInterval = 0.0625f; // ~16Hz
    
    [Header("Debug")]
    [SerializeField] private bool drawBoidDebug = true;
    [SerializeField] private bool debugCollisions = true;
    [SerializeField] private bool enableServerTracking = false;
    
    private Dictionary<int, DigitalTwin> digitalTwins = new Dictionary<int, DigitalTwin>();
    private Dictionary<int, TrackedRobot> trackedRobots = new Dictionary<int, TrackedRobot>();
    private HashSet<(int, int)> collidedPairs = new HashSet<(int, int)>();
    
    private int nextTwinId = 1001;
    private int nextTrackedId = 2001;
    private bool boidBehaviorEnabled = false;
    private Coroutine serverCoroutine;
    private Coroutine unityRobotsCoroutine;



    public class DigitalTwin
    {
        public int id;
        public GameObject gameObject;
        public Transform transform;
        public Rigidbody rigidbody;
        public BoidController controller;
        public BoxCollider boxCollider;
        public Vector3 velocity;


        public DigitalTwin(int id, GameObject go, BoidController ctrl)
        {
            this.id = id;
            this.gameObject = go;
            this.transform = go.transform;
            this.rigidbody = go.GetComponent<Rigidbody>();
            this.boxCollider = go.GetComponent<BoxCollider>();
            this.controller = ctrl;
        }
    }


    public class TrackedRobot
    {
        public int id;
        public GameObject gameObject;
        public Transform transform;
        public Rigidbody rigidbody;
        public BoxCollider boxCollider;


        public TrackedRobot(int id, GameObject go)
        {
            this.id = id;
            this.gameObject = go;
            this.transform = go.transform;
            this.rigidbody = go.GetComponent<Rigidbody>();
            this.boxCollider = go.GetComponent<BoxCollider>();
        }
    }


    [System.Serializable]
    private class RobotData { public int id; public float x; public float y; public float rotation; }
    [System.Serializable]
    private class RobotsResponse { public RobotData[] robots; }

    [System.Serializable]
    private class UnityRobotData { public int id; public float x; public float z; public float rotation; }
    [System.Serializable]
    private class UnityRobotsPayload { public UnityRobotData[] robots; }


    public void SpawnRobotsFromConfig(SimulationConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[SimulationManager] Config is null");
            return;
        }

        if (config.robots == null || config.robots.Count == 0)
        {
            Debug.LogError("[SimulationManager] No robots in config");
            return;
        }

        // Update arena settings from config
        if (config.arena != null)
        {
            arenaSize = new Vector2(config.arena.sizeX, config.arena.sizeZ);
            arenaOrigin = new Vector3(config.arena.originX, config.arena.originY, config.arena.originZ);
            Debug.Log($"[SimulationManager] Arena updated: {arenaSize.x}m x {arenaSize.y}m, origin: {arenaOrigin}");
        }

        // Spawn each robot from config
        int spawnedCount = 0;
        foreach (var robotData in config.robots)
        {
            Vector3 spawnPos = new Vector3(robotData.x, arenaOrigin.y, robotData.z);
            int spawnedId = SpawnDigitalTwin(spawnPos);

            if (spawnedId >= 0)
            {
                spawnedCount++;
                Debug.Log($"[SimulationManager] Spawned {robotData.name} (ID: {robotData.id}) at ({robotData.x}, {robotData.z})");
            }
            else
            {
                Debug.LogWarning($"[SimulationManager] Failed to spawn {robotData.name}");
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

        if (config.robots == null || config.robots.Count == 0)
        {
            errorMessage = "No robots in config";
            return false;
        }

        foreach (var robot in config.robots)
        {
            if (!IsValidSpawnPosition(new Vector3(robot.x, arenaOrigin.y, robot.z)))
            {
                errorMessage = $"Robot {robot.name} spawn position invalid: ({robot.x}, {robot.z})";
                return false;
            }
        }

        return true;
    }



    // void Start()
    // {
    //     Debug.Log("[SimulationManager] Started. Auto-spawning " + autoSpawnCount + " digital twins...");                 
    //     for (int i = 0; i < autoSpawnCount; i++)
    //     {
    //         SpawnDigitalTwinRandom();
    //     }
    //     EnableBoidBehavior(enableBoidBehavior);

        
    //     if (enableServerTracking)
    //         serverCoroutine = StartCoroutine(ServerTrackingCoroutine());
    // }
    void Start()
    {
        Debug.Log("[SimulationManager] Started");

        // Check if we're coming from StartScreen with a config
        if (StartScreenManager.LastLoadedConfig != null)
        {
            Debug.Log("[SimulationManager] Loading robots from config");
            SpawnRobotsFromConfig(StartScreenManager.LastLoadedConfig);
        }
        else
        {
            // Fallback: Auto-spawn for testing (if no config provided)
            Debug.LogWarning("[SimulationManager] No config provided - using default spawn");
            autoSpawnCount = 5; // Set a default
            for (int i = 0; i < autoSpawnCount; i++)
            {
                SpawnDigitalTwinRandom();
            }
        }

        EnableBoidBehavior(enableBoidBehavior);

        if (enableServerTracking)
            serverCoroutine = StartCoroutine(ServerTrackingCoroutine());
        
        if (enableUnityRobotsTracking)
            unityRobotsCoroutine = StartCoroutine(SendUnityRobotPosesCoroutine());

    }


    void Update()
    {
        if (boidBehaviorEnabled)
        {
            foreach (var twin in digitalTwins.Values)
            {
                if (twin.controller != null)
                {
                    twin.controller.UpdateBehavior(
                        digitalTwins.Values.ToList(),
                        maxSpeed, maxForce,
                        cohesionRadius, cohesionWeight,
                        separationRadius, separationWeight,
                        alignmentRadius, alignmentWeight
                    );
                }
            }
        }

        CheckCollisions();
    }


    // ==================== DIGITAL TWINS ====================


    public int SpawnDigitalTwin(Vector3 spawnPosition)
    {
        if (digitalTwinPrefab == null) { Debug.LogError("[SimulationManager] Digital Twin prefab not assigned!"); return -1; }
        if (!IsValidSpawnPosition(spawnPosition)) { Debug.LogWarning("[SimulationManager] Invalid spawn position"); return -1; }


        GameObject twinGO = Instantiate(digitalTwinPrefab, spawnPosition, Quaternion.identity, transform);
        int twinId = nextTwinId++;
        twinGO.name = $"DigitalTwin_{twinId}";


        Rigidbody rb = twinGO.GetComponent<Rigidbody>();
        if (rb == null) rb = twinGO.AddComponent<Rigidbody>();


        rb.mass = robotMassGrams / 1000f;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 1f;


        BoxCollider boxCollider = twinGO.GetComponent<BoxCollider>();
        if (boxCollider == null) boxCollider = twinGO.AddComponent<BoxCollider>();
        boxCollider.isTrigger = false;


        BoidController boidController = twinGO.GetComponent<BoidController>();
        if (boidController == null) boidController = twinGO.AddComponent<BoidController>();
        boidController.Initialize(twinId, rb, arenaSize, arenaOrigin);


        DigitalTwin twin = new DigitalTwin(twinId, twinGO, boidController);
        digitalTwins[twinId] = twin;
        
        Debug.Log($"[SimulationManager] Spawned Digital Twin {twinId}");
        return twinId;
    }


    public int SpawnDigitalTwinRandom() => SpawnDigitalTwin(GetRandomSpawnPosition());


    public void DestroyDigitalTwin(int twinId)
    {
        if (digitalTwins.TryGetValue(twinId, out DigitalTwin twin))
        {
            Destroy(twin.gameObject);
            digitalTwins.Remove(twinId);
        }
    }


    public void EnableBoidBehavior(bool enable)
    {
        boidBehaviorEnabled = enable;
        foreach (var twin in digitalTwins.Values)
        {
            if (twin.controller != null) twin.controller.enabled = enable;
        }
        Debug.Log($"[SimulationManager] Boid behavior {(enable ? "enabled" : "disabled")}");
    }


    // ==================== SERVER TRACKING ====================


    private IEnumerator ServerTrackingCoroutine()
    {
        while (enableServerTracking)
        {
            yield return StartCoroutine(FetchTrackedRobots());
            yield return new WaitForSeconds(serverUpdateInterval);
        }
    }


    private IEnumerator FetchTrackedRobots()
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{pythonServerURL}/robot/data"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    RobotsResponse data = JsonUtility.FromJson<RobotsResponse>(request.downloadHandler.text);
                    if (data.robots != null)
                    {
                        var activeIds = new HashSet<int>();
                        foreach (RobotData robotData in data.robots)
                        {
                            activeIds.Add(robotData.id);
                            if (!trackedRobots.ContainsKey(robotData.id))
                                CreateTrackedRobot(robotData.id);


                            UpdateTrackedRobotPosition(robotData);
                        }
                        
                        var idsToRemove = trackedRobots.Keys.Where(id => !activeIds.Contains(id)).ToList();
                        foreach (int id in idsToRemove)
                            DestroyTrackedRobot(id);
                    }
                }
                catch { }
            }
        }
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


    private void UpdateTrackedRobotPosition(RobotData robotData)
    {
        if (trackedRobots.TryGetValue(robotData.id, out TrackedRobot robot))
        {
            // ✅ Get ANY digital twin's Y to match height
            float digitalTwinY = digitalTwins.Count > 0 ? digitalTwins.Values.First().transform.position.y : 0f;
            
            // ✅ COORDINATE MAPPING: server.x → Unity.X, server.y → Unity.Z, Y → digitalTwin Y
            float unityX = robotData.x;
            float unityY = digitalTwinY;  // ← Match digital twin Y
            float unityZ = robotData.y;
            
            robot.transform.position = new Vector3(unityX, unityY, unityZ);
            robot.transform.rotation = Quaternion.Euler(0, -robotData.rotation, 0);
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




    // ==================== SENDING UNITY ROBOT POSES (OUTGOING) ====================

    private IEnumerator SendUnityRobotPosesCoroutine()
    {
        while (enableUnityRobotsTracking)
        {
            yield return StartCoroutine(PostUnityRobotPoses());
            yield return new WaitForSeconds(unityRobotsUpdateInterval);
        }
    }

    private IEnumerator PostUnityRobotPoses()
    {
        // Make copies to avoid concurrent modification
        var twins = new List<DigitalTwin>(digitalTwins.Values);
        var robots = new List<TrackedRobot>(trackedRobots.Values);
        
        int totalRobots = twins.Count + robots.Count;
        
        if (totalRobots == 0)
            yield break;
        
        UnityRobotData[] robotsData = new UnityRobotData[totalRobots];
        int index = 0;
        
        // Add digital twins from copy
        foreach (var twin in twins)
        {
            robotsData[index++] = new UnityRobotData
            {
                id = twin.id,
                x = twin.transform.position.x,
                z = twin.transform.position.z,
                rotation = twin.transform.eulerAngles.y
            };
        }
        
        // Add tracked robots from copy
        foreach (var robot in robots)
        {
            robotsData[index++] = new UnityRobotData
            {
                id = robot.id,
                x = robot.transform.position.x,
                z = robot.transform.position.z,
                rotation = robot.transform.eulerAngles.y
            };
        }

            
        UnityRobotsPayload payload = new UnityRobotsPayload { robots = robotsData };
        string jsonPayload = JsonUtility.ToJson(payload);
        
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
        
        using (UnityWebRequest request = new UnityWebRequest($"{unityRobotsServerURL}/unityrobots/data", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 2;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                // Silent success for performance
            }
            else if (request.result == UnityWebRequest.Result.ConnectionError || 
                    request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogWarning($"[UnityRobots] Server error: {request.error}");
            }
        }
    }

    // ==================== COLLISION DETECTION ====================


    private void CheckCollisions()
    {
        // Digital twins vs digital twins
        var twinList = digitalTwins.Values.ToList();
        for (int i = 0; i < twinList.Count; i++)
        {
            for (int j = i + 1; j < twinList.Count; j++)
            {
                if (twinList[i].boxCollider != null && twinList[j].boxCollider != null)
                {
                    if (twinList[i].boxCollider.bounds.Intersects(twinList[j].boxCollider.bounds))
                    {
                        int id1 = Mathf.Min(twinList[i].id, twinList[j].id);
                        int id2 = Mathf.Max(twinList[i].id, twinList[j].id);
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
        foreach (var dTwin in digitalTwins.Values)
        {
            foreach (var tRobot in trackedRobots.Values)
            {
                if (dTwin.boxCollider != null && tRobot.boxCollider != null)
                {
                    if (dTwin.boxCollider.bounds.Intersects(tRobot.boxCollider.bounds))
                    {
                        if (debugCollisions) Debug.Log($"[COLLISION] DigitalTwin {dTwin.id} <-> TrackedRobot {tRobot.id}");
                    }
                }
            }
        }
    }


    // ==================== UTILITIES ====================


    private bool IsValidSpawnPosition(Vector3 position)
    {
        float maxX = arenaOrigin.x + arenaSize.x / 2;
        float minX = arenaOrigin.x - arenaSize.x / 2;
        float maxZ = arenaOrigin.z + arenaSize.y / 2;
        float minZ = arenaOrigin.z - arenaSize.y / 2;


        if (position.x < minX || position.x > maxX || position.z < minZ || position.z > maxZ)
            return false;


        foreach (var twin in digitalTwins.Values)
        {
            if (Vector3.Distance(position, twin.transform.position) < minSpawnDistance)
                return false;
        }
        return true;
    }


    private Vector3 GetRandomSpawnPosition()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float randomX = Random.Range(arenaOrigin.x - arenaSize.x / 2, arenaOrigin.x + arenaSize.x / 2);
            float randomZ = Random.Range(arenaOrigin.z - arenaSize.y / 2, arenaOrigin.z + arenaSize.y / 2);
            Vector3 spawnPos = new Vector3(randomX, arenaOrigin.y, randomZ);
            
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
    }
}
