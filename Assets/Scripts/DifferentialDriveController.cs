using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// WASD-driven differential drive using ArticulationBody revolute joints.
/// Setup: chassis = root ArticulationBody, wheels = child ArticulationBodies
/// (Joint Type: Revolute, anchor X axis aligned with the axle,
/// X Drive: stiffness 0 / damping > 0).
/// Attach this to the chassis root. Works with both the old Input Manager
/// and the new Input System (Unity 6).
/// </summary>
public class DifferentialDriveController : MonoBehaviour
{
    [Header("Wheel articulation bodies")]
    public ArticulationBody leftWheel;
    public ArticulationBody rightWheel;

    [Header("Robot geometry (meters)")]
    public float wheelRadius = 0.033f;
    public float trackWidth  = 0.165f;   // center-to-center distance between wheels

    [Header("Velocity limits")]
    public float maxLinearVelocity  = 0.20f;  // m/s
    public float maxAngularVelocity = 2.5f;   // rad/s (2*maxWheelSpeed/trackWidth is the physical cap)

    [Header("Command ramping")]
    public float linearAcceleration  = 0.5f;  // m/s^2  - how fast commands ramp up/down
    public float angularAcceleration = 6.0f;  // rad/s^2

    [Header("Drive tuning")]
    public float damping    = 100f;  // velocity gain of the joint drive
    public float forceLimit = 15f;   // max torque the drive may apply

    [Header("Direction fixes")]
    public bool invertLeftWheel  = true;  // tick if a wheel spins the wrong way
    public bool invertRightWheel = true;  // (common when a wheel model is mirrored)

    [Header("Pose tracking")]
    [SerializeField] public GameObject poseObject;

    [Header("Input")]
    public bool useKeyboardInput = true;   // untick when driving from UI/ROS via SetCommand()

    float _targetLinear, _targetAngular;   // what the keys are asking for
    float _cmdLinear,    _cmdAngular;      // ramped commands actually applied

    void Start()
    {
        ConfigureDrive(leftWheel);
        ConfigureDrive(rightWheel);
    }

    void ConfigureDrive(ArticulationBody wheel)
    {
        var d = wheel.xDrive;
        d.stiffness  = 0f;        // 0 stiffness + positive damping = pure velocity control
        d.damping    = damping;
        d.forceLimit = forceLimit;
        wheel.xDrive = d;         // ArticulationDrive is a struct: must write it back!
        wheel.jointFriction = 0f;
    }

    void Update()
    {
        if (useKeyboardInput) ReadKeyboard();
    }

    void ReadKeyboard()
    {
        float fwd = 0f, turn = 0f;   // turn: positive = counter-clockwise (left)

#if ENABLE_LEGACY_INPUT_MANAGER
        fwd  =  Input.GetAxisRaw("Vertical");     // W/S (and arrow keys)
        turn = -Input.GetAxisRaw("Horizontal");   // A = +1 (left), D = -1 (right)
#elif ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) fwd  += 1f;
            if (kb.sKey.isPressed) fwd  -= 1f;
            if (kb.aKey.isPressed) turn += 1f;    // left = CCW = positive
            if (kb.dKey.isPressed) turn -= 1f;
        }
#endif
        _targetLinear  = fwd  * maxLinearVelocity;
        _targetAngular = turn * maxAngularVelocity;
    }

    /// <summary>Drive programmatically (UI buttons, ROS, AI). Accepts inputs in
    /// the range [-1,1].</summary>
    public void SetTargets(float linear, float angular)
    {
        if (linear is < -1.0f or > 1.0f)
        {
            Debug.LogWarning("SetTargets: linear is < -1.0f or > 1.0f");
            return;
        }

        if (angular is < -1.0f or > 1.0f)
        {
            Debug.LogWarning("SetTargets: angular is < -1.0f or > 1.0f");
            return;
        }

        _targetLinear = linear * maxLinearVelocity;
        _targetAngular = angular * maxAngularVelocity;

        Debug.Log("SetTargets: linear = " + linear + ", angular = " + angular);
    }

    void FixedUpdate()
    {
        // Ramp toward the requested command so the robot doesn't jerk or slip.
        _cmdLinear  = Mathf.MoveTowards(_cmdLinear,  _targetLinear,  linearAcceleration  * Time.fixedDeltaTime);
        _cmdAngular = Mathf.MoveTowards(_cmdAngular, _targetAngular, angularAcceleration * Time.fixedDeltaTime);

        // Differential drive kinematics.
        float vLeft  = _cmdLinear - _cmdAngular * trackWidth * 0.5f;
        float vRight = _cmdLinear + _cmdAngular * trackWidth * 0.5f;

        SetWheelLinearVelocity(leftWheel,  invertLeftWheel  ? -vLeft  : vLeft);
        SetWheelLinearVelocity(rightWheel, invertRightWheel ? -vRight : vRight);
    }

    void SetWheelLinearVelocity(ArticulationBody wheel, float v)
    {
        // Drive targets are in DEGREES/sec (jointVelocity, by contrast, is rad/s).
        float degPerSec = (v / wheelRadius) * Mathf.Rad2Deg;

        var d = wheel.xDrive;
        d.targetVelocity = degPerSec;
        wheel.xDrive = d;
    }

    // ---------- Odometry helpers (e.g. for a UI speedometer) ----------

    /// <summary>Measured forward speed in m/s, from the wheel joints.</summary>
    public float MeasuredLinearVelocity()
    {
        float wl = leftWheel.jointVelocity[0]  * (invertLeftWheel  ? -1f : 1f);
        float wr = rightWheel.jointVelocity[0] * (invertRightWheel ? -1f : 1f);
        return (wl + wr) * 0.5f * wheelRadius;
    }

    /// <summary>Measured yaw rate in rad/s, from the wheel joints.</summary>
    public float MeasuredAngularVelocity()
    {
        float wl = leftWheel.jointVelocity[0]  * (invertLeftWheel  ? -1f : 1f);
        float wr = rightWheel.jointVelocity[0] * (invertRightWheel ? -1f : 1f);
        return (wr - wl) * wheelRadius / trackWidth;
    }
}