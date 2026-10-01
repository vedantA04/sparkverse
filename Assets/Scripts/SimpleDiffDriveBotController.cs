using UnityEngine;
using UnityEngine.Serialization;

public class SimpleDiffDriveBotController : MonoBehaviour
{
    /// <summary>
    /// Controls left wheel speed. Can be any float but assumes a range of [-1,1].
    /// </summary>
    public float LeftDriveInput { get; set; }

    /// <summary>
    /// Controls right wheel speed. Can be any float but assumes a range of [-1,1].
    /// </summary>
    public float RightDriveInput { get; set; }

    [SerializeField] public GameObject poseObject;

    [SerializeField] private HingeJoint leftWheel;
    [SerializeField] private HingeJoint rightWheel;

    [FormerlySerializedAs("motorSpeed")]
    [SerializeField]
    private float maxMotorSpeed = 300f;

    private const float MotorForce = 1000f;

    private void FixedUpdate()
    {
        SetMotor(leftWheel, LeftDriveInput);
        SetMotor(rightWheel, RightDriveInput);
    }

    private void SetMotor(HingeJoint joint, float input)
    {
        var motor = joint.motor;
        motor.force = MotorForce;
        motor.targetVelocity = input * maxMotorSpeed;

        joint.motor = motor;
        joint.useMotor = true;
    }
}
