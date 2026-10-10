using UnityEngine;

namespace CatSimulator
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ChargingCable : MonoBehaviour
    {
        public Transform wallAnchor, phoneAnchor;
        public float cableLength = 1.08f;
        const int Count = 28;
        readonly Vector3[] points = new Vector3[Count];
        readonly Vector3[] previous = new Vector3[Count];
        readonly float[] support = new float[Count];
        LineRenderer line;

        void Start()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = Count;
            line.useWorldSpace = true;
            for (var i = 0; i < Count; i++)
            {
                var t = i / (float)(Count - 1);
                points[i] = Vector3.Lerp(wallAnchor.position, phoneAnchor.position, t)
                    + Vector3.down * (.22f * Mathf.Sin(t * Mathf.PI));
                previous[i] = points[i];
            }
            line.SetPositions(points);
        }

        void FixedUpdate()
        {
            for (var i = 1; i < Count - 1; i++)
            {
                var origin = new Vector3(points[i].x, Mathf.Max(.95f, points[i].y + .20f), points[i].z);
                support[i] = Physics.Raycast(origin, Vector3.down, out var hit, origin.y + .05f,
                    ~(1 << 8), QueryTriggerInteraction.Ignore) ? hit.point.y + .004f : .025f;
                var next = points[i] + (points[i] - previous[i]) * .975f
                    + Physics.gravity * (Time.fixedDeltaTime * Time.fixedDeltaTime);
                previous[i] = points[i];
                points[i] = next;
            }
            var segment = cableLength / (Count - 1);
            for (var step = 0; step < 18; step++)
            {
                points[0] = wallAnchor.position;
                points[Count - 1] = phoneAnchor.position;
                for (var i = 0; i < Count - 1; i++)
                {
                    var delta = points[i + 1] - points[i];
                    var length = delta.magnitude;
                    if (length < .00001f) continue;
                    var correction = delta * ((length - segment) / length);
                    if (i == 0) points[i + 1] -= correction;
                    else if (i == Count - 2) points[i] += correction;
                    else { points[i] += correction * .5f; points[i + 1] -= correction * .5f; }
                }
                for (var i = 1; i < Count - 1; i++) points[i].y = Mathf.Max(support[i], points[i].y);
            }
            points[0] = wallAnchor.position;
            points[Count - 1] = phoneAnchor.position;
        }

        void LateUpdate()
        {
            points[0] = wallAnchor.position;
            points[Count - 1] = phoneAnchor.position;
            line.SetPositions(points);
        }
    }
}
