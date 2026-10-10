using UnityEngine;

namespace CatSimulator
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PushableProp : MonoBehaviour
    {
        Rigidbody body;
        Vector3 pushDirection, pushPoint;
        float pushForce, lastContact = -100;

        void Awake() => body = GetComponent<Rigidbody>();

        // A contact request is consumed at the physics rate, independent of rendering FPS.
        public void RequestPush(Vector3 direction, Vector3 point, float force)
        {
            pushDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            pushPoint = point;
            pushForce = force;
            lastContact = Time.time;
        }

        void FixedUpdate()
        {
            if (body.isKinematic || Time.time - lastContact > .08f) return;
            body.AddForceAtPosition(pushDirection * pushForce, pushPoint, ForceMode.Force);
        }
    }
}
