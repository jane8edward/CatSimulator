using System;
using System.Collections;
using UnityEngine;

namespace CatSimulator
{
    // One kinematic owner per leaf: no springs, joint feedback, or accumulated transforms.
    public sealed class NewHouseDoor : CatInteractable
    {
        [Serializable] public class Panel
        {
            public Rigidbody body;
            public BoxCollider shape;
            public Vector3 closedPosition, openPosition;
            public Quaternion closedRotation = Quaternion.identity, openRotation = Quaternion.identity;
        }
        public string displayName;
        public Panel[] panels;
        public bool startsOpen = true;
        public float duration = 1.25f;
        public bool IsMoving { get; private set; }
        public bool IsOpen => Progress >= .99999f;
        public float Progress { get; private set; }
        public int ObstructionCount { get; private set; }
        public override bool Available => isActiveAndEnabled && !IsMoving;
        readonly Collider[] candidates = new Collider[64];
        CatController player;
        float from, target, elapsed;

        void Awake()
        {
            player = FindFirstObjectByType<CatController>();
            Progress = startsOpen ? 1 : 0;
            foreach (var p in panels)
            {
                p.body.position = Position(p, Progress);
                p.body.rotation = Rotation(p, Progress);
            }
            RefreshPrompt();
        }

        public bool RequestToggle()
        {
            if (!Available || (player && player.Paused) || ContentPreviewMenu.GameplayInputBlocked) return false;
            Begin(IsOpen ? 0 : 1);
            return true;
        }

        void Begin(float destination)
        {
            from = Progress; target = destination; elapsed = 0;
            IsMoving = Mathf.Abs(target - from) > .00001f;
            RefreshPrompt();
        }

        public override IEnumerator Perform(CatController cat)
        {
            if (!RequestToggle()) yield break;
            cat.TriggerAnimation("Paw");
            while (IsMoving) yield return null;
        }

        void FixedUpdate()
        {
            if (!IsMoving || (player && player.Paused) || ContentPreviewMenu.GameplayInputBlocked) return;
            elapsed += Time.fixedDeltaTime / Mathf.Max(.2f, duration);
            float t = Mathf.Clamp01(elapsed);
            // Quintic easing has zero velocity and acceleration at both ends.
            float ease = t * t * t * (t * (t * 6 - 15) + 10);
            float next = Mathf.Lerp(from, target, ease);
            if (Obstructed(next))
            {
                ObstructionCount++;
                // Closing reverses gently; an obstructed opening stops and can be retried.
                if (target < from) Begin(1);
                else { IsMoving = false; RefreshPrompt(); }
                return;
            }
            Progress = next;
            foreach (var p in panels)
            {
                p.body.MovePosition(Position(p, next));
                p.body.MoveRotation(Rotation(p, next));
            }
            if (t >= 1) { Progress = target; IsMoving = false; RefreshPrompt(); }
        }

        bool Obstructed(float next)
        {
            foreach (var p in panels)
            {
                var position = Position(p, next);
                var rotation = Rotation(p, next);
                var shape = p.shape;
                var half = Vector3.Scale(shape.size, shape.transform.lossyScale) * .5f;
                var center = position + rotation * shape.center;
                int count = Physics.OverlapBoxNonAlloc(center, half + Vector3.one * .035f, candidates, rotation, ~0, QueryTriggerInteraction.Ignore);
                if (count == candidates.Length) return true; // Conservatively stop if the query fills.
                for (int i = 0; i < count; i++)
                {
                    var other = candidates[i];
                    if (!other || other == shape || other.GetComponentInParent<NewHouseDoor>() == this) continue;
                    var cat = other.GetComponentInParent<CatController>();
                    var body = other.attachedRigidbody;
                    // Frames, tracks and other static architecture intentionally overlap the leaf edges.
                    if (!cat && (!body || body.isKinematic)) continue;
                    if (!Physics.ComputePenetration(shape, position, rotation, other, other.transform.position,
                        other.transform.rotation, out _, out float depth)) continue;
                    bool already = Physics.ComputePenetration(shape, p.body.position, p.body.rotation, other,
                        other.transform.position, other.transform.rotation, out _, out float previousDepth);
                    // Permit escape from an existing overlap, never deepen it.
                    if (!already || depth > previousDepth + .0001f) return true;
                }
                // Protect the longer nose/rump in addition to the vertical CharacterController.
                if (player && !player.Paused)
                {
                    var a = player.transform.TransformPoint(new Vector3(0, .1935f, -.072f) * player.BodyScale);
                    var b = player.transform.TransformPoint(new Vector3(0, .1935f, .2025f) * player.BodyScale);
                    float radius = .1215f * player.BodyScale;
                    if (CapsuleTouchesBox(a, b, radius, center, half, rotation) &&
                        !CapsuleTouchesBox(a, b, radius, p.body.position + p.body.rotation * shape.center, half, p.body.rotation)) return true;
                }
            }
            return false;
        }

        static bool CapsuleTouchesBox(Vector3 a, Vector3 b, float radius, Vector3 center, Vector3 half, Quaternion rotation)
        {
            var inverse = Quaternion.Inverse(rotation);
            a = inverse * (a - center); b = inverse * (b - center);
            // The cat's capsule is short; samples at 1 cm also cover its rounded ends.
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .01f));
            for (int i = 0; i <= steps; i++)
            {
                var q = Vector3.Lerp(a, b, i / (float)steps);
                var nearest = new Vector3(Mathf.Clamp(q.x, -half.x, half.x), Mathf.Clamp(q.y, -half.y, half.y), Mathf.Clamp(q.z, -half.z, half.z));
                if ((q - nearest).sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }

        void RefreshPrompt() => prompt = (IsOpen ? "关闭" : "打开") + displayName;
        static Vector3 Position(Panel p, float t) => Vector3.Lerp(p.closedPosition, p.openPosition, t);
        static Quaternion Rotation(Panel p, float t) => Quaternion.Slerp(p.closedRotation, p.openRotation, t);
    }
}
