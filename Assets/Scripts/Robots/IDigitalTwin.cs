using Commands;
using UnityEngine;

namespace Robots
{
    public interface IDigitalTwin
    {
        public int Id { get; set; }
        public GameObject GameObject { get; set; }
        public Transform Transform { get; set; }
    }

    public interface ICommandableTwin : IDigitalTwin, ICommandReceiver
    {
    }
}