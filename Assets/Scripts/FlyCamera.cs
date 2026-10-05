using UnityEngine;

public class FlyCamera : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 0.1f;

    private UserActions input;

    private Vector2 moveHorizInput;
    private Vector2 lookInput;
    private float upDownInput;

    private float pitch;
    private float yaw;

    private void Update()
    {
        var speed = moveSpeed;

        var movement =
            transform.forward * moveHorizInput.y +
            transform.right * moveHorizInput.x +
            transform.up * upDownInput;

        transform.position += movement * (speed * Time.deltaTime);

        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void OnEnable()
    {
        input.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        input.Disable();
    }

    private void Awake()
    {
        // Initialize pitch/yaw from the editor-set rotation so the camera
        // doesn't snap to identity on play.
        var rot = transform.rotation.eulerAngles;
        pitch = rot.x;
        yaw = rot.y;

        input = new UserActions();

        input.Camera.MoveHorizontal.performed += ctx => moveHorizInput = ctx.ReadValue<Vector2>();
        input.Camera.MoveHorizontal.canceled += _ => moveHorizInput = Vector2.zero;

        input.Camera.MoveVertical.performed += ctx => upDownInput = ctx.ReadValue<float>();
        input.Camera.MoveVertical.canceled += _ => upDownInput = 0f;

        input.Camera.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Camera.Look.canceled += _ => lookInput = Vector2.zero;
    }
}
