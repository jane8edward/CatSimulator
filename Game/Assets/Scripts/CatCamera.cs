using UnityEngine;

namespace CatSimulator
{
    public sealed class CatCamera : MonoBehaviour
    {
        public CatController cat;
        public float distance = 2.05f;
        public float yaw;
        public float pitch = 16;
        Vector3 smoothedTarget;
        float avoidanceYaw, avoidanceElevation, renderedYaw, renderedElevation;
        float yawVelocity, elevationVelocity, preferredClearFor, nextAvoidanceSearch, cameraDistance;
        public bool Automated;
        public Vector3 MovementForward => Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        public Vector3 MovementRight => Quaternion.Euler(0, yaw, 0) * Vector3.right;
        public float AutomaticYawOffset => renderedYaw;

        void Start()
        {
            SnapToTarget();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void LateUpdate()
        {
            if (!cat.Paused && !Automated)
            {
                yaw += Input.GetAxis("Mouse X") * 2.2f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 1.8f, -8, 55);
                distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * .15f, 1.25f, 3.2f);
            }
            var target = cat.transform.position + Vector3.up * .26f;
            smoothedTarget = Vector3.Lerp(smoothedTarget, target, 1 - Mathf.Exp(-10 * Time.unscaledDeltaTime));
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            // Near a wall, orbit around the cat instead of pushing the camera into its body.
            rotation = FindClearView(rotation);
            var offset = rotation * Vector3.back;
            var actualDistance = ClearDistance(offset);
            // Retract immediately to stay outside geometry, then recover the distance gradually.
            cameraDistance = actualDistance < cameraDistance ? actualDistance : Mathf.Lerp(cameraDistance, actualDistance, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
            transform.position = smoothedTarget + offset * cameraDistance;
            transform.LookAt(smoothedTarget + Vector3.up * .04f);
        }

        public void SnapToTarget()
        {
            smoothedTarget = cat.transform.position + Vector3.up * .26f;
            avoidanceYaw = avoidanceElevation = renderedYaw = renderedElevation = 0;
            yawVelocity = elevationVelocity = preferredClearFor = 0;
            cameraDistance = distance;
            nextAvoidanceSearch = 0;
        }

        float ClearDistance(Vector3 direction)
        {
            if (Physics.SphereCast(smoothedTarget, .12f, direction, out var hit, distance, ~(1 << 8), QueryTriggerInteraction.Ignore))
                return Mathf.Max(.3f, hit.distance - .08f);
            return distance;
        }

        Quaternion FindClearView(Quaternion preferred)
        {
            var minimum = Mathf.Min(distance, 1.15f);
            if (ClearDistance(preferred * Vector3.back) >= minimum + .08f)
            {
                preferredClearFor += Time.unscaledDeltaTime;
                if (preferredClearFor >= .3f) avoidanceYaw = avoidanceElevation = 0;
            }
            else preferredClearFor = 0;

            var held = Quaternion.Euler(Mathf.Min(65, pitch + avoidanceElevation), yaw + avoidanceYaw, 0);
            var heldDistance = ClearDistance(held * Vector3.back);
            if (heldDistance < minimum && Time.unscaledTime >= nextAvoidanceSearch)
            {
                nextAvoidanceSearch = Time.unscaledTime + .18f;
                var bestScore = float.MaxValue;
                var bestAngle = avoidanceYaw; var bestElevation = avoidanceElevation;
                var farthest = heldDistance; var fallbackAngle = avoidanceYaw; var fallbackElevation = avoidanceElevation;
                for (var elevation = 0; elevation <= 2; elevation++)
                for (var side = 0; side <= 8; side++)
                {
                    var angle = side == 0 ? 0 : ((side + 1) / 2) * 40 * (side % 2 == 1 ? 1 : -1);
                    var candidate = Quaternion.Euler(Mathf.Min(65, pitch + elevation * 20), yaw + angle, 0);
                    var candidateDistance = ClearDistance(candidate * Vector3.back);
                    if (candidateDistance > farthest + .15f) { farthest = candidateDistance; fallbackAngle = angle; fallbackElevation = elevation * 20; }
                    var score = Mathf.Abs(Mathf.DeltaAngle(avoidanceYaw, angle)) * .7f + Mathf.Abs(angle) * .3f + elevation * 25;
                    if (candidateDistance >= minimum && score < bestScore) { bestAngle = angle; bestElevation = elevation * 20; bestScore = score; }
                }
                avoidanceYaw = bestScore < float.MaxValue ? bestAngle : fallbackAngle;
                avoidanceElevation = bestScore < float.MaxValue ? bestElevation : fallbackElevation;
            }
            renderedYaw = Mathf.SmoothDampAngle(renderedYaw, avoidanceYaw, ref yawVelocity, .16f, Mathf.Infinity, Time.unscaledDeltaTime);
            renderedElevation = Mathf.SmoothDamp(renderedElevation, avoidanceElevation, ref elevationVelocity, .16f, Mathf.Infinity, Time.unscaledDeltaTime);
            return Quaternion.Euler(Mathf.Min(65, pitch + renderedElevation), yaw + renderedYaw, 0);
        }
    }
}
