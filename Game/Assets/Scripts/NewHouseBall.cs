using UnityEngine;

namespace CatSimulator
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(PushableProp))]
    public sealed class NewHouseBall : MonoBehaviour
    {
        Rigidbody body;
        Vector3 home;
        Quaternion homeRotation;
        Bounds recoveryBounds = new Bounds(new Vector3(-5, 1.5f, -4.5f), new Vector3(10, 4, 9));
        void Awake()
        {
            body = GetComponent<Rigidbody>(); home = body.position; homeRotation = body.rotation;
            body.maxAngularVelocity = 60;
            body.maxDepenetrationVelocity = .7f;
            body.solverIterations = 12; body.solverVelocityIterations = 8;
            var session = FindFirstObjectByType<LevelSession>();
            if (session && session.definition) { recoveryBounds = session.definition.activityBounds; recoveryBounds.Expand(.4f); }
        }
        void FixedUpdate()
        {
            var p = body.position;
            // Recover only from invalid physics or leaving the supported house, never during normal rolling.
            if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z) || p.y < -.3f || p.x < recoveryBounds.min.x || p.x > recoveryBounds.max.x || p.z < recoveryBounds.min.z || p.z > recoveryBounds.max.z)
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
