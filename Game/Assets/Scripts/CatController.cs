using System.Collections;
using UnityEngine;

namespace CatSimulator
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CatController : MonoBehaviour
    {
        public Animator animator;
        public Transform visual;
        public Transform cameraTransform;
        public LevelSession session;
        public float walkSpeed = 1.15f;
        public float runSpeed = 2.4f;
        public float jumpHeight = .68f;
        public bool IsBusy { get; private set; }
        public bool Paused { get; set; }
        public CatInteractable Nearest { get; private set; }
        public string MovementBlocker { get; private set; }
        CharacterController controller;
        float verticalSpeed;
        float lastGrounded;
        bool wasGrounded;
        CatCamera cameraRig;
        public bool Automated { get; set; }
        public Vector2 VerificationMovement { get; set; }
        bool requestedJump;
        const float BodyRadius = .1215f;
        public float BodyFrontReach => .2025f + BodyRadius;
        public void RequestJump() => requestedJump = true;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            cameraRig = cameraTransform.GetComponent<CatCamera>();
        }

        void Update()
        {
            if (Paused || IsBusy) return;
            var grounded = controller.isGrounded;
            if (grounded)
            {
                lastGrounded = Time.time;
                if (verticalSpeed < 0) verticalSpeed = -1.4f;
                if (!wasGrounded) animator.SetTrigger("Land");
            }
            wasGrounded = grounded;
            var x = Automated ? VerificationMovement.x : (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            var z = Automated ? VerificationMovement.y : (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            // Only mouse-controlled yaw changes steering; automatic camera avoidance must not.
            var forward = cameraRig.MovementForward;
            var right = cameraRig.MovementRight;
            var direction = Vector3.ClampMagnitude(forward * z + right * x, 1);
            var speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            if (direction.sqrMagnitude > .001f)
            {
                var rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1 - Mathf.Exp(-12 * Time.deltaTime));
                BodyPoints(rotation, out var rear, out var front);
                if (!Physics.CheckCapsule(rear, front, BodyRadius, ~(1 << 8), QueryTriggerInteraction.Ignore)) transform.rotation = rotation;
            }
            if (((!Automated && Input.GetKeyDown(KeyCode.Space)) || requestedJump) && Time.time - lastGrounded < .12f)
            {
                verticalSpeed = Mathf.Sqrt(jumpHeight * 2 * 12);
                lastGrounded = -100;
                animator.SetTrigger("Jump");
            }
            requestedJump=false;
            verticalSpeed -= 12 * Time.deltaTime;
            var horizontalMove = direction * speed * Time.deltaTime;
            MovementBlocker = null;
            BodyPoints(transform.rotation, out var bodyRear, out var bodyFront);
            if (horizontalMove.sqrMagnitude > .0000001f && Physics.CapsuleCast(bodyRear, bodyFront, BodyRadius,
                horizontalMove.normalized, out var bodyHit, horizontalMove.magnitude + .012f, ~(1 << 8), QueryTriggerInteraction.Ignore))
            {
                MovementBlocker = bodyHit.collider.name;
                horizontalMove = horizontalMove.normalized * Mathf.Max(0, bodyHit.distance - .012f);
            }
            controller.Move(horizontalMove + Vector3.up * verticalSpeed * Time.deltaTime);
            var bounds = session.definition.activityBounds;
            var position = transform.position;
            position.x = Mathf.Clamp(position.x, bounds.min.x + .16f, bounds.max.x - .16f);
            position.z = Mathf.Clamp(position.z, bounds.min.z + .16f, bounds.max.z - .16f);
            transform.position = position;
            animator.SetFloat("Speed", direction.magnitude * (speed / walkSpeed), .12f, Time.deltaTime);
            UpdateNearest();
            if (!Automated && Input.GetKeyDown(KeyCode.E) && Nearest != null) BeginInteraction(Nearest);
        }

        void BodyPoints(Quaternion rotation, out Vector3 rear, out Vector3 front)
        {
            // The cat's body is longer than a vertical controller capsule; protect its nose and rump too.
            rear = transform.position + rotation * new Vector3(0, .1935f, -.072f);
            front = transform.position + rotation * new Vector3(0, .1935f, .2025f);
        }

        void UpdateNearest()
        {
            CatInteractable next = null;
            var best = float.MaxValue;
            foreach (var candidate in FindObjectsByType<CatInteractable>(FindObjectsSortMode.None))
            {
                if (!candidate.Available) continue;
                var distance = Vector3.Distance(transform.position, candidate.Anchor.position);
                if (distance <= candidate.radius && distance < best)
                { best = distance; next = candidate; }
            }
            if (next == Nearest) return;
            if (Nearest != null) Nearest.Highlight(false);
            Nearest = next;
            if (Nearest != null) Nearest.Highlight(true);
        }

        public void BeginInteraction(CatInteractable target)
        {
            if (!IsBusy && target.Available) StartCoroutine(Interact(target));
        }

        IEnumerator Interact(CatInteractable target)
        {
            IsBusy = true;
            if (Nearest != null) Nearest.Highlight(false);
            Nearest = null;
            animator.SetFloat("Speed", 0);
            yield return target.Perform(this);
            IsBusy = false;
            verticalSpeed = -1.4f;
        }

        public IEnumerator MoveTo(Vector3 destination, float duration, float arc = 0)
        {
            var start = transform.position;
            var horizontal = Vector3.ProjectOnPlane(destination - start, Vector3.up);
            if (horizontal.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(horizontal);
            controller.enabled = false;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var ease = t * t * (3 - 2 * t);
                transform.position = Vector3.Lerp(start, destination, ease) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * arc);
                yield return null;
            }
            transform.position = destination;
            controller.enabled = true;
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalSpeed = -1;
            cameraRig.SnapToTarget();
        }
    }
}
