// ================== BOID CONTROLLER (2WD ROBOT CONSTRAINT - FIXED) ==================
// Fixed: Proper velocity handling, alignment calculation, and Y axis preservation
// Robots rotate first, then move along forward axis
// Attach this to each digital twin GameObject

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BoidController : MonoBehaviour
{
    private int boidId;
    private Rigidbody rb;
    private Vector3 horizontalVelocity = Vector3.zero;  // Only X and Z
    private Vector3 acceleration = Vector3.zero;
    private Vector3 cohesionForce = Vector3.zero;
    private Vector3 separationForce = Vector3.zero;
    private Vector3 alignmentForce = Vector3.zero;
    private Vector2 arenaSize;
    private Vector3 arenaOrigin;

    [SerializeField] private float turningForce = 5f;
    [SerializeField] private float maxRotationSpeed = 180f;  // degrees per second

    public void Initialize(int id, Rigidbody rigidbody, Vector2 aSize, Vector3 aOrigin)
    {
        boidId = id;
        rb = rigidbody;
        arenaSize = aSize;
        arenaOrigin = aOrigin;
        horizontalVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        Debug.Log($"[Boid {boidId}] Initialized (2WD mode)");
    }


    public void UpdateBehavior(
        List<SimulationManager.DigitalTwin> allTwins,
        float maxSpeed,
        float maxForce,
        float cohesionRadius,
        float cohesionWeight,
        float separationRadius,
        float separationWeight,
        float alignmentRadius,
        float alignmentWeight
    )
    {
        if (rb == null) return;

        acceleration = Vector3.zero;
        cohesionForce = Vector3.zero;
        separationForce = Vector3.zero;
        alignmentForce = Vector3.zero;

        // FIND NEARBY BOIDS
        List<SimulationManager.DigitalTwin> nearbyTwins = new List<SimulationManager.DigitalTwin>();
        float maxRadius = Mathf.Max(cohesionRadius, separationRadius, alignmentRadius);
        
        foreach (var twin in allTwins)
        {
            if (twin.id != boidId)
            {
                float distance = Vector3.Distance(transform.position, twin.transform.position);
                if (distance < maxRadius)
                {
                    nearbyTwins.Add(twin);
                }
            }
        }

        if (nearbyTwins.Count > 0)
        {
            // COHESION
            List<SimulationManager.DigitalTwin> cohesionNeighbors = nearbyTwins
                .Where(t => Vector3.Distance(transform.position, t.transform.position) < cohesionRadius)
                .ToList();
            
            if (cohesionNeighbors.Count > 0)
            {
                cohesionForce = CalculateCohesion(cohesionNeighbors, maxForce) * cohesionWeight;
            }

            // SEPARATION
            List<SimulationManager.DigitalTwin> separationNeighbors = nearbyTwins
                .Where(t => Vector3.Distance(transform.position, t.transform.position) < separationRadius)
                .ToList();
            
            if (separationNeighbors.Count > 0)
            {
                separationForce = CalculateSeparation(separationNeighbors, maxForce) * separationWeight;
            }

            // ALIGNMENT
            List<SimulationManager.DigitalTwin> alignmentNeighbors = nearbyTwins
                .Where(t => Vector3.Distance(transform.position, t.transform.position) < alignmentRadius)
                .ToList();
            
            if (alignmentNeighbors.Count > 0)
            {
                alignmentForce = CalculateAlignment(alignmentNeighbors, maxForce) * alignmentWeight;
            }
        }

        acceleration += cohesionForce;
        acceleration += separationForce;
        acceleration += alignmentForce;

        if (acceleration.magnitude > maxForce)
        {
            acceleration = acceleration.normalized * maxForce;
        }

        // ✅ 2WD CONSTRAINT: Only move along robot's forward axis
        // Step 1: Rotate toward desired direction
        // Step 2: Only apply velocity along forward axis
        
        Vector3 desiredDirection = acceleration.normalized;
        Vector3 currentForward = transform.forward;
        
        // Calculate rotation needed
        if (desiredDirection.magnitude > 0.1f)
        {
            // Angle between current forward and desired direction (only Y rotation matters)
            float angle = Vector3.SignedAngle(currentForward, desiredDirection, Vector3.up);
            
            // Rotate toward desired direction (limited by maxRotationSpeed)
            float maxRotationThisFrame = maxRotationSpeed * Time.deltaTime;
            float rotationAmount = Mathf.Clamp(angle, -maxRotationThisFrame, maxRotationThisFrame);
            
            rb.angularVelocity = new Vector3(0, rotationAmount / Time.deltaTime * Mathf.Deg2Rad, 0);
            
            // Only move forward if roughly aligned (within ~45 degrees)
            if (Mathf.Abs(angle) < 45f)
            {
                // Move only along robot's forward axis
                float forwardSpeed = acceleration.magnitude;
                forwardSpeed = Mathf.Min(forwardSpeed, maxSpeed);
                
                horizontalVelocity = transform.forward * forwardSpeed;
            }
            else
            {
                // Rotating - don't move forward yet (but smooth stop)
                horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, Time.deltaTime * 5f);
            }
        }
        else
        {
            // No acceleration - coast to stop smoothly
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, Time.deltaTime * 3f);
            rb.angularVelocity = Vector3.zero;
        }

        // ✅ Preserve Y velocity (gravity)
        float gravityVelocity = rb.linearVelocity.y;
        Vector3 finalVelocity = new Vector3(horizontalVelocity.x, gravityVelocity, horizontalVelocity.z);
        
        rb.linearVelocity = finalVelocity;
    }


    private Vector3 CalculateCohesion(List<SimulationManager.DigitalTwin> neighbors, float maxForce)
    {
        if (neighbors.Count == 0) return Vector3.zero;
        
        Vector3 centerOfMass = Vector3.zero;
        
        foreach (var neighbor in neighbors)
        {
            centerOfMass += neighbor.transform.position;
        }

        centerOfMass /= neighbors.Count;
        Vector3 steeringForce = centerOfMass - transform.position;
        
        // Only horizontal component
        steeringForce = new Vector3(steeringForce.x, 0, steeringForce.z);

        if (steeringForce.magnitude > 0)
        {
            steeringForce = steeringForce.normalized * maxForce;
        }

        return steeringForce;
    }


    private Vector3 CalculateSeparation(List<SimulationManager.DigitalTwin> neighbors, float maxForce)
    {
        if (neighbors.Count == 0) return Vector3.zero;
        
        Vector3 steeringForce = Vector3.zero;
        
        foreach (var neighbor in neighbors)
        {
            Vector3 diff = transform.position - neighbor.transform.position;
            // Only horizontal component
            diff = new Vector3(diff.x, 0, diff.z);
            float distance = diff.magnitude;
            
            if (distance > 0)
            {
                diff = diff.normalized / distance;
                steeringForce += diff;
            }
        }

        steeringForce /= neighbors.Count;
        steeringForce = new Vector3(steeringForce.x, 0, steeringForce.z);

        if (steeringForce.magnitude > 0)
        {
            steeringForce = steeringForce.normalized * maxForce;
        }

        return steeringForce;
    }


    private Vector3 CalculateAlignment(List<SimulationManager.DigitalTwin> neighbors, float maxForce)
    {
        if (neighbors.Count == 0) return Vector3.zero;
        
        Vector3 avgVelocity = Vector3.zero;
        
        foreach (var neighbor in neighbors)
        {
            // Only get horizontal velocity
            Vector3 neighborHorizVel = neighbor.rigidbody.linearVelocity;
            neighborHorizVel = new Vector3(neighborHorizVel.x, 0, neighborHorizVel.z);
            avgVelocity += neighborHorizVel;
        }

        avgVelocity /= neighbors.Count;
        avgVelocity = new Vector3(avgVelocity.x, 0, avgVelocity.z);
        
        // Desired direction is average direction of neighbors
        Vector3 steeringForce = avgVelocity;

        if (steeringForce.magnitude > 0)
        {
            steeringForce = steeringForce.normalized * maxForce;
        }

        return steeringForce;
    }
}
