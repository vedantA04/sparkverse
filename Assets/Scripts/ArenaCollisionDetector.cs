// ================== ARENA COLLISION DETECTOR (USING BOX COLLIDERS) ==================
// Detects collisions between digital twin robots using their BoxColliders
// Logs to console with robot IDs
// Attach to empty GameObject called "CollisionDetector"


using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;


public class ArenaCollisionDetector : MonoBehaviour
{
    [Header("Collision Detection")]
    [SerializeField] private SimulationManager simulationManager;
    [SerializeField] private bool enableCollisionDetection = true;


    private HashSet<string> activeCollisions = new HashSet<string>();  // Track ongoing collisions



    // ==================== INITIALIZATION ====================


    private void Start()
    {
        if (simulationManager == null)
        {
            simulationManager = FindObjectOfType<SimulationManager>();
        }


        if (simulationManager != null)
        {
            Debug.Log("[COLLISION] Arena Collision Detector initialized (BoxCollider-based)");
        }
        else
        {
            Debug.LogError("[COLLISION] SimulationManager not found!");
        }
    }



    // ==================== MAIN UPDATE LOOP ====================


    private void Update()
    {
        if (!enableCollisionDetection || simulationManager == null) return;


        DetectCollisions();
        DetectTrackedRobotCollisions();
    }



    // ==================== COLLISION DETECTION USING BOX COLLIDERS ====================


    private void DetectCollisions()
    {
        // Get all digital twins from SimulationManager using reflection
        FieldInfo digitalTwinField = typeof(SimulationManager).GetField("digitalTwins",
            BindingFlags.NonPublic | BindingFlags.Instance);


        if (digitalTwinField == null)
        {
            Debug.LogError("[COLLISION] Could not find digitalTwins field");
            return;
        }


        object digitalTwinsObj = digitalTwinField.GetValue(simulationManager);
        
        // Cast to Dictionary<int, DigitalTwin>
        var digitalTwins = digitalTwinsObj as Dictionary<int, object>;


        if (digitalTwins == null || digitalTwins.Count == 0)
        {
            return;
        }


        var robotList = new List<int>(digitalTwins.Keys);


        // Check all pairs for collisions
        for (int i = 0; i < robotList.Count; i++)
        {
            for (int j = i + 1; j < robotList.Count; j++)
            {
                int robotIdA = robotList[i];
                int robotIdB = robotList[j];


                object twinAObj = digitalTwins[robotIdA];
                object twinBObj = digitalTwins[robotIdB];


                if (twinAObj == null || twinBObj == null) continue;


                // Get boxCollider from each twin
                FieldInfo boxColliderField = twinAObj.GetType().GetField("boxCollider",
                    BindingFlags.Public | BindingFlags.Instance);


                if (boxColliderField == null) continue;


                BoxCollider boxColliderA = boxColliderField.GetValue(twinAObj) as BoxCollider;
                BoxCollider boxColliderB = boxColliderField.GetValue(twinBObj) as BoxCollider;


                if (boxColliderA == null || boxColliderB == null) continue;


                // Check if colliders are overlapping
                bool isColliding = boxColliderA.bounds.Intersects(boxColliderB.bounds);


                string collisionKey = GetCollisionKey(robotIdA, robotIdB);
                bool wasColliding = activeCollisions.Contains(collisionKey);


                if (isColliding && !wasColliding)
                {
                    // COLLISION START
                    Debug.LogWarning($"[COLLISION] DigitalTwin {robotIdA} ↔ DigitalTwin {robotIdB}");
                    activeCollisions.Add(collisionKey);
                }
                else if (!isColliding && wasColliding)
                {
                    // COLLISION END
                    Debug.Log($"[SEPARATED] DigitalTwin {robotIdA} ↔ DigitalTwin {robotIdB}");
                    activeCollisions.Remove(collisionKey);
                }
            }
        }
    }



    // ==================== TRACKED ROBOT COLLISION DETECTION ====================


    private void DetectTrackedRobotCollisions()
    {
        // Get all tracked robots from SimulationManager using reflection
        FieldInfo trackedRobotField = typeof(SimulationManager).GetField("trackedRobots",
            BindingFlags.NonPublic | BindingFlags.Instance);


        if (trackedRobotField == null) return;


        object trackedRobotsObj = trackedRobotField.GetValue(simulationManager);
        var trackedRobots = trackedRobotsObj as Dictionary<int, object>;


        if (trackedRobots == null || trackedRobots.Count == 0) return;


        var robotList = new List<int>(trackedRobots.Keys);


        // Check all pairs for collisions
        for (int i = 0; i < robotList.Count; i++)
        {
            for (int j = i + 1; j < robotList.Count; j++)
            {
                int robotIdA = robotList[i];
                int robotIdB = robotList[j];


                object robotAObj = trackedRobots[robotIdA];
                object robotBObj = trackedRobots[robotIdB];


                if (robotAObj == null || robotBObj == null) continue;


                // Get boxCollider from each tracked robot
                FieldInfo boxColliderField = robotAObj.GetType().GetField("boxCollider",
                    BindingFlags.Public | BindingFlags.Instance);


                if (boxColliderField == null) continue;


                BoxCollider boxColliderA = boxColliderField.GetValue(robotAObj) as BoxCollider;
                BoxCollider boxColliderB = boxColliderField.GetValue(robotBObj) as BoxCollider;


                if (boxColliderA == null || boxColliderB == null) continue;


                // Check if colliders are overlapping
                bool isColliding = boxColliderA.bounds.Intersects(boxColliderB.bounds);


                string collisionKey = GetTrackedCollisionKey(robotIdA, robotIdB);
                bool wasColliding = activeCollisions.Contains(collisionKey);


                if (isColliding && !wasColliding)
                {
                    // COLLISION START
                    Debug.LogWarning($"[COLLISION] TrackedRobot {robotIdA} ↔ TrackedRobot {robotIdB}");
                    activeCollisions.Add(collisionKey);
                }
                else if (!isColliding && wasColliding)
                {
                    // COLLISION END
                    Debug.Log($"[SEPARATED] TrackedRobot {robotIdA} ↔ TrackedRobot {robotIdB}");
                    activeCollisions.Remove(collisionKey);
                }
            }
        }
    }



    // ==================== UTILITY METHODS ====================


    private string GetCollisionKey(int robotIdA, int robotIdB)
    {
        // Ensure consistent ordering so A-B and B-A are the same collision
        if (robotIdA > robotIdB)
        {
            int temp = robotIdA;
            robotIdA = robotIdB;
            robotIdB = temp;
        }
        return $"DT_{robotIdA}_{robotIdB}";
    }


    private string GetTrackedCollisionKey(int robotIdA, int robotIdB)
    {
        // Ensure consistent ordering so A-B and B-A are the same collision
        if (robotIdA > robotIdB)
        {
            int temp = robotIdA;
            robotIdA = robotIdB;
            robotIdB = temp;
        }
        return $"TR_{robotIdA}_{robotIdB}";
    }


    public int GetActiveCollisionCount()
    {
        return activeCollisions.Count;
    }


    public List<string> GetActiveCollisions()
    {
        return new List<string>(activeCollisions);
    }


    public void ClearCollisionHistory()
    {
        activeCollisions.Clear();
        Debug.Log("[COLLISION] Collision history cleared");
    }
}
