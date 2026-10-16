using UnityEngine;

namespace CatSimulator
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(PushableProp))]
    public sealed class NewHouseBall : MonoBehaviour
    {
        Rigidbody body;
        Vector3 home;
        Quaternion homeRotation;
        void Awake()
        {
            body = GetComponent<Rigidbody>(); home = body.position; homeRotation = body.rotation;
            body.maxAngularVelocity = 60;
            body.maxDepenetrationVelocity = 2;
            body.solverIterations = 12; body.solverVelocityIterations = 8;
        }
        void FixedUpdate()
        {
            var p = body.position;
            // Recover only from invalid physics or leaving the supported house, never during normal rolling.
            if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z) || p.y < -.3f || p.x < -10 || p.x > 0 || p.z < -9 || p.z > 0)
                ResetPose();
        }
        public void ResetPose()
        {
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.position = home; body.rotation = homeRotation;
            transform.SetPositionAndRotation(home, homeRotation);
            body.WakeUp();
        }
    }
}
