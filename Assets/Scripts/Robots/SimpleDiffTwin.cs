using Commands;
using UnityEngine;

namespace Robots
{
    public class SimpleDiffTwin : ICommandableTwin
    {
        public int Id { get; set; }
        public GameObject GameObject { get; set; }
        public Transform Transform { get; set; }
        public SimpleDiffDriveBotController SimpleController { get; set; }

        private ICommand LastCommand { get; set; }
        private double LastCommandEnd { get; set; }
        private double _lastUnhandledCommandLog;

        public SimpleDiffTwin(int id, GameObject go)
        {
            Id = id;
            GameObject = go;
        }

        public static ICommandableTwin Spawn(
            GameObject prefab,
            Vector3 spawnPosition,
            Transform parent,
            int id)
        {
            var gameObj = Object.Instantiate(prefab, spawnPosition, Quaternion.identity, parent);
            gameObj.name = $"DigitalTwin_{id}";

            var twin = new SimpleDiffTwin(id, gameObj)
            {
                SimpleController = gameObj.GetComponent<SimpleDiffDriveBotController>()
            };
            twin.Transform = twin.SimpleController.poseObject.transform;

            Debug.Log($"Spawned Digital Twin {id}");
            return twin;
        }

        public void SetCommand(ICommand cmd)
        {
            LastCommand = cmd;
            LastCommandEnd = LastCommand switch
            {
                DriveCommand d => Time.timeAsDouble + d.DurationMs / 1000.0,
                TurnCommand t => Time.timeAsDouble + t.DurationMs / 1000.0,
                StopCommand =>  Time.timeAsDouble + 100,
                _ => LastCommandEnd
            };

            Debug.Log($"Set command: {cmd}");
        }

        public void Handle(ICommand command)
        {
            SetCommand(command);
        }

        public void FixedUpdate()
        {
            if (Time.timeAsDouble > LastCommandEnd)
            {
                SimpleController.LeftDriveInput = 0.0f;
                SimpleController.RightDriveInput = 0.0f;
                return;
            }

            switch (LastCommand)
            {
                case DriveCommand d:
                    Drive(d);
                    break;
                case TurnCommand t:
                    Turn(t);
                    break;
                case StopCommand:
                    Stop();
                    break;
                default:
                    if (Time.timeAsDouble - _lastUnhandledCommandLog > 1.0)
                    {
                        Debug.LogError($"Unhandled command: {LastCommand}");
                        _lastUnhandledCommandLog =  Time.timeAsDouble;
                    }
                    break;
            }
        }

        private void Drive(DriveCommand cmd)
        {
            SimpleController.LeftDriveInput = cmd.Speed / 63.0f;
            SimpleController.RightDriveInput = cmd.Speed / 63.0f;
        }

        private void Turn(TurnCommand cmd)
        {
            SimpleController.LeftDriveInput = cmd.Speed / 63.0f;
            SimpleController.RightDriveInput = -cmd.Speed / 63.0f;
        }

        private void Stop()
        {
            SimpleController.LeftDriveInput = 0f;
            SimpleController.RightDriveInput = 0f;
        }
    }
}
