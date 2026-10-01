using System;
using System.Collections;
using Commands;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Robots
{
    public class OriginalTwin : ICommandableTwin
    {
        public int Id { get; set; }
        public GameObject GameObject { get; set; }
        public Transform Transform { get; set; }
        public Rigidbody rigidbody;
        public BoidController controller;
        private readonly MonoBehaviour _parent;
        public BoxCollider boxCollider;

        private float maxLinearSpeed = 0.3f; // m/s or units/s
        private float maxAngularSpeedDeg = 90f; // deg/s
        private float slope = 0.0152f; // For kinematic model
        private float constant = -0.1051f; // For kinematic model
        private float wheel_to_wheel = 0.165f; // For kinematic model

        public OriginalTwin(int id, GameObject go, BoidController ctrl, MonoBehaviour parent)
        {
            Id = id;
            GameObject = go;
            Transform = go.transform;
            rigidbody = go.GetComponent<Rigidbody>();
            boxCollider = go.GetComponent<BoxCollider>();
            controller = ctrl;
            _parent = parent;
        }

        public static ICommandableTwin Spawn(
            GameObject prefab,
            Vector3 spawnPosition,
            Transform parent,
            int id,
            float robotMassGrams,
            Vector2 arenaSize,
            Vector2 arenaOrigin,
            MonoBehaviour parentBehaviour)
        {
            GameObject twinGO = Object.Instantiate(prefab, spawnPosition, Quaternion.identity, parent);
            twinGO.name = $"DigitalTwin_{id}";

            Rigidbody rb = twinGO.GetComponent<Rigidbody>();
            if (rb == null) rb = twinGO.AddComponent<Rigidbody>();

            rb.mass = robotMassGrams / 1000f;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ |
                             RigidbodyConstraints.FreezePositionY;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 1f;

            BoxCollider boxCollider = twinGO.GetComponent<BoxCollider>();
            if (boxCollider == null) boxCollider = twinGO.AddComponent<BoxCollider>();
            boxCollider.isTrigger = false;

            BoidController boidController = twinGO.GetComponent<BoidController>();
            if (boidController == null) boidController = twinGO.AddComponent<BoidController>();
            boidController.Initialize(id, rb, arenaSize, arenaOrigin);

            Debug.Log($"Spawned Digital Twin {id}");

            return new OriginalTwin(id, twinGO, boidController, parentBehaviour);
        }

        public void Handle(ICommand command)
        {
            switch (command)
            {
                case DriveCommand cmd:
                    Drive(cmd);
                    break;
                case TurnCommand cmd:
                    Turn(cmd);
                    break;
                case RotateCommand cmd:
                    Rotate(cmd);
                    break;
                case ConfigCommand cmd:
                    Config(cmd);
                    break;
                default:
                    Debug.LogError($"Unsupported command: {command}");
                    break;
            }
        }

        private void Drive(DriveCommand cmd)
        {
            var normalizedSpeed = Math.Abs(cmd.Speed) * slope + constant;
            var moveDirection = cmd.Speed < 0
                ? -Transform.forward
                : Transform.forward;
            rigidbody.linearVelocity = moveDirection * normalizedSpeed
                                       + Vector3.up * rigidbody.linearVelocity.y;
            _parent.StartCoroutine(ScheduleStopAfter(cmd.DurationMs / 1000f));
        }

        private void Turn(TurnCommand cmd)
        {
            var rotationSpeed = (Math.Abs(cmd.Speed) * slope + constant) / wheel_to_wheel;
            if (cmd.Speed < 0) rotationSpeed = -rotationSpeed;
            rigidbody.angularVelocity = new Vector3(0, rotationSpeed * Mathf.Deg2Rad, 0);
            _parent.StartCoroutine(ScheduleStopAfter(cmd.DurationMs / 1000f));
        }

        private void Rotate(RotateCommand cmd)
        {
            _parent.StartCoroutine(RotateToAngle(cmd.Angle, cmd.Speed));
        }

        private IEnumerator RotateToAngle(int targetAngle, int rotSpeed)
        {
            var rotationSpeed = Mathf.Clamp01(rotSpeed / 63f) * maxAngularSpeedDeg;
            var tolerance = 5f;

            while (true)
            {
                var currentYaw = Transform.eulerAngles.y;
                var angleDifference = Mathf.DeltaAngle(currentYaw, targetAngle);

                if (Mathf.Abs(angleDifference) < tolerance)
                {
                    rigidbody.angularVelocity = Vector3.zero;
                    Debug.Log($"[ROBOT {Id}] Rotation complete - target: {targetAngle}°, current: {currentYaw:F1}°");
                    break;
                }

                float direction = angleDifference > 0 ? 1 : -1;
                rigidbody.angularVelocity = new Vector3(0, direction * rotationSpeed * Mathf.Deg2Rad, 0);

                yield return new WaitForSeconds(0.1f);
            }
        }

        private void Config(ConfigCommand cmd)
        {
            Debug.Log(cmd);
        }

        private IEnumerator ScheduleStopAfter(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            Stop();
        }

        private void Stop()
        {
            Debug.Log($"[ROBOT {Id}] STOP");

            rigidbody.linearVelocity = new Vector3(0, rigidbody.linearVelocity.y, 0);
            rigidbody.angularVelocity = Vector3.zero;
        }
    }
}
