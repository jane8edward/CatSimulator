using UnityEngine;

namespace CatSimulator
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PushableProp : MonoBehaviour
    {
        Rigidbody body;
        Vector3 sourceVelocity, contactNormal, localPoint;
        float sourceMass;
        bool pending;
        bool traceContact;

        void Awake()
        {
            body=GetComponent<Rigidbody>();
            traceContact=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--trace-prop-contact")>=0 && name.Contains("Mouse");
            // A 3 cm ball rolling at walking speed already turns faster than
            // the engine's general-purpose 7 rad/s default.
            if(GetComponentInChildren<SphereCollider>())body.maxAngularVelocity=60;
        }

        // The CharacterController has no rigidbody mass. Resolve one inelastic
        // contact against a finite cat mass, then leave motion to the physics solver.
        public void RequestContact(Vector3 velocity, float mass, Vector3 point, Vector3 normal)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            var inward=-normal.normalized;
            if(traceContact)Debug.Log("MOUSE_CONTACT velocity="+velocity+" normal="+normal+" point="+point);
            if(inward.sqrMagnitude<.5f || Vector3.Dot(velocity,inward)<=0)return;
            sourceVelocity=Vector3.ProjectOnPlane(velocity,Vector3.up);
            sourceMass=Mathf.Max(.1f,mass);contactNormal=inward;
            localPoint=transform.InverseTransformPoint(point);pending=true;
        }

        void FixedUpdate()
        {
            if(!pending)return;
            pending=false;
            if(body.isKinematic)return;
            var point=transform.TransformPoint(localPoint);
            float closing=Vector3.Dot(sourceVelocity-body.GetPointVelocity(point),contactNormal);
            // Separating contacts transfer no energy, even if W is still held.
            if(closing<=0)return;
            var lever=point-body.worldCenterOfMass;
            var torque=Vector3.Cross(lever,contactNormal);
            var principal=body.rotation*body.inertiaTensorRotation;
            var localTorque=Quaternion.Inverse(principal)*torque;
            var inertia=body.inertiaTensor;
            var inverseTorque=new Vector3(localTorque.x/Mathf.Max(inertia.x,1e-7f),
                localTorque.y/Mathf.Max(inertia.y,1e-7f),localTorque.z/Mathf.Max(inertia.z,1e-7f));
            float inverseEffectiveMass=1/sourceMass+1/body.mass+Vector3.Dot(torque,principal*inverseTorque);
            float impulse=closing/inverseEffectiveMass;
            if(traceContact)Debug.Log("MOUSE_IMPULSE closing="+closing+" impulse="+impulse+" point="+point+" com="+body.worldCenterOfMass+" inertia="+body.inertiaTensor);
            body.AddForceAtPosition(contactNormal*impulse,point,ForceMode.Impulse);
        }
    }
}
