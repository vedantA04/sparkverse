using Commands;
using UnityEngine;

namespace Robots
{
    public class ArticulationBodyTwin : ICommandableTwin
    {
        public int Id { get; set; }
        public GameObject GameObject { get; set; }
        public Transform Transform { get; set; }

        private DifferentialDriveController Controller { get; set; }
        private ICommand LastCommand { get; set; }
        private double LastCommandEnd { get; set; }

        public void Handle(ICommand command)
        {
            LastCommand = command;
            LastCommandEnd = LastCommand switch
            {
                DriveCommand d => Time.timeAsDouble + d.DurationMs / 1000.0,
                TurnCommand t => Time.timeAsDouble + t.DurationMs / 1000.0,
                _ => LastCommandEnd
            };

            Debug.Log($"Set command: {command}");
        }

        public static ICommandableTwin Spawn(
            GameObject prefab,
            Vector3 spawnPosition,
            Transform parent,
            int id)
        {
            var gameObj = Object.Instantiate(prefab, spawnPosition, Quaternion.identity, parent);
            gameObj.name = $"DigitalTwin_{id}";

            var twin = new ArticulationBodyTwin
            {
                Id = id,
                GameObject = gameObj,
                Controller = gameObj.GetComponentInChildren<DifferentialDriveController>(),
            };
            twin.Transform = twin.Controller.poseObject.transform;

            Debug.Log($"[SimulationManager] Spawned Digital Twin {id}");
            return twin;
        }

        public void FixedUpdate()
        {
            if (Time.timeAsDouble > LastCommandEnd)
            {
                Controller.SetTargets(0f, 0f);
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
            }
        }

        private void Drive(DriveCommand cmd)
        {
            Controller.SetTargets(cmd.Speed / 63.0f, 0f);
        }

        private void Turn(TurnCommand cmd)
        {
            Controller.SetTargets(0f, cmd.Speed / 63.0f);
        }
    }
}