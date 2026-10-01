using UnityEngine;

namespace Robots
{
    public class TrackedRobot
    {
        public int id;
        public GameObject gameObject;
        public Transform transform;
        public Rigidbody rigidbody;
        public BoxCollider boxCollider;


        public TrackedRobot(int id, GameObject go)
        {
            this.id = id;
            this.gameObject = go;
            this.transform = go.transform;
            this.rigidbody = go.GetComponent<Rigidbody>();
            this.boxCollider = go.GetComponent<BoxCollider>();
        }
    }
}