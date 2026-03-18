using UnityEngine;

/// <summary>
/// Main Camera Controller Script
/// Features:
/// - Arrow Keys: Move camera forward/backward/left/right
/// - Shift + Arrow Keys: Rotate view up/down/left/right (without changing position)
/// - Adjustable speeds and sensitivities via Inspector
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float moveAcceleration = 0f;
    
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 40f;
    [SerializeField] private float maxVerticalAngle = 89f;
    
    private float currentVerticalRotation = 0f;
    private float currentHorizontalRotation = 0f;
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 cameraPosition = Vector3.zero;
    
    private void Update()
    {
        HandleMovement();
        HandleRotation();
    }
    
    private void HandleMovement()
    {
        Vector3 moveDirection = Vector3.zero;
        
        // Get input for movement (Arrow Keys)
        if (Input.GetKey(KeyCode.UpArrow))
            moveDirection += transform.forward;
        if (Input.GetKey(KeyCode.DownArrow))
            moveDirection -= transform.forward;
        if (Input.GetKey(KeyCode.RightArrow))
            moveDirection += transform.right;
        if (Input.GetKey(KeyCode.LeftArrow))
            moveDirection -= transform.right;
        
        // Normalize to prevent faster diagonal movement
        moveDirection = moveDirection.normalized;
        
        // Apply movement with optional acceleration
        if (moveAcceleration > 0)
        {
            currentVelocity = Vector3.Lerp(currentVelocity, moveDirection * moveSpeed, moveAcceleration * Time.deltaTime);
            cameraPosition += currentVelocity * Time.deltaTime;
        }
        else
        {
            cameraPosition += moveDirection * moveSpeed * Time.deltaTime;
        }
        
        // Update world position while maintaining rotation
        transform.position = cameraPosition;
    }
    
    private void HandleRotation()
    {
        // Only rotate if Shift is held + Arrow Keys
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            return;
        
        float horizontalRotationInput = 0f;
        float verticalRotationInput = 0f;
        
        // Get rotation input (Shift + Arrow Keys)
        if (Input.GetKey(KeyCode.UpArrow))
            verticalRotationInput = 1f;
        if (Input.GetKey(KeyCode.DownArrow))
            verticalRotationInput = -1f;
        if (Input.GetKey(KeyCode.RightArrow))
            horizontalRotationInput = 1f;
        if (Input.GetKey(KeyCode.LeftArrow))
            horizontalRotationInput = -1f;
        
        // Apply horizontal rotation (around Y axis)
        currentHorizontalRotation += horizontalRotationInput * rotationSpeed * Time.deltaTime;
        
        // Apply vertical rotation (around X axis) with clamping to prevent flipping
        currentVerticalRotation += verticalRotationInput * rotationSpeed * Time.deltaTime;
        currentVerticalRotation = Mathf.Clamp(currentVerticalRotation, -maxVerticalAngle, maxVerticalAngle);
        
        // Apply rotations ONLY - position stays locked
        transform.localRotation = Quaternion.Euler(-currentVerticalRotation, currentHorizontalRotation, 0f);
    }
    
    /// <summary>
    /// Public method to reset camera position and rotation
    /// </summary>
    public void ResetCamera()
    {
        cameraPosition = Vector3.zero;
        transform.position = cameraPosition;
        transform.rotation = Quaternion.identity;
        currentVerticalRotation = 0f;
        currentHorizontalRotation = 0f;
        currentVelocity = Vector3.zero;
    }
    
    /// <summary>
    /// Public method to set camera position
    /// </summary>
    public void SetCameraPosition(Vector3 newPosition)
    {
        cameraPosition = newPosition;
        transform.position = cameraPosition;
    }
}
