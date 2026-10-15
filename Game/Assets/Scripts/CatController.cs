using System.Collections;
using UnityEngine;

namespace CatSimulator
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CatController : MonoBehaviour
    {
        public Animator animator;
        [System.NonSerialized] public NativeCatAnimation animationDriver;
        public Transform visual;
        public Vector3 visualOffset;
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
        public bool VerificationRun { get; set; }
        bool requestedJump;
        Vector3 contactVelocity;
        readonly RaycastHit[] pawContacts=new RaycastHit[16];
        bool tracePropContacts;float nextContactTrace;
        public float BodyScale { get; private set; } = 1;
        float BodyRadius => .1215f*BodyScale;
        public float BodyFrontReach => .2025f*BodyScale + BodyRadius;
        public void RequestJump() => requestedJump = true;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            cameraRig = cameraTransform.GetComponent<CatCamera>();
            tracePropContacts=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--trace-prop-contact")>=0;
        }

        void Update()
        {
            if (Paused || IsBusy) return;
            var grounded = controller.isGrounded;
            if (grounded)
            {
                lastGrounded = Time.time;
                if (verticalSpeed < 0) verticalSpeed = -1.4f;
                if (!wasGrounded) TriggerAnimation("Land");
            }
            wasGrounded = grounded;
            var x = Automated ? VerificationMovement.x : (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            var z = Automated ? VerificationMovement.y : (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            // The camera has one heading shared by rendering and movement, sampled before this Update.
            var forward = cameraRig.MovementForward;
            var right = cameraRig.MovementRight;
            var direction = Vector3.ClampMagnitude(forward * z + right * x, 1);
            var speed = (Automated ? VerificationRun : Input.GetKey(KeyCode.LeftShift)) ? runSpeed : walkSpeed;
            contactVelocity=direction*speed;
            if (direction.sqrMagnitude > .001f)
            {
                var targetRotation=Quaternion.LookRotation(direction);
                var rotation = Quaternion.RotateTowards(transform.rotation,targetRotation,720*Time.deltaTime);
                BodyPoints(rotation, out var rear, out var front);
                if (!Physics.CheckCapsule(rear, front, BodyRadius, ~(1 << 8), QueryTriggerInteraction.Ignore)) transform.rotation = rotation;
                else
                {
                    // In a tight gap the intermediate pose may be blocked while the final facing is clear.
                    BodyPoints(targetRotation,out rear,out front);
                    if(!Physics.CheckCapsule(rear,front,BodyRadius,~(1<<8),QueryTriggerInteraction.Ignore))transform.rotation=targetRotation;
                }
            }
            if (((!Automated && Input.GetKeyDown(KeyCode.Space)) || requestedJump) && Time.time - lastGrounded < .12f)
            {
                verticalSpeed = Mathf.Sqrt(jumpHeight * 2 * 12);
                lastGrounded = -100;
                TriggerAnimation("Jump");
            }
            requestedJump=false;
            verticalSpeed -= 12 * Time.deltaTime;
            var horizontalMove = direction * speed * Time.deltaTime;
            if (horizontalMove.sqrMagnitude > .0000001f)
            {
                // Low objects sit below the nose capsule. Front paws can nudge them before
                // the character controller treats a mouse or book as a small step.
                float pawRadius=.006f*BodyScale;
                float sole=transform.position.y+controller.center.y-controller.height*.5f;
                Collider support=null;
                if(Physics.Raycast(transform.position+Vector3.up*.12f*BodyScale,Vector3.down,out var ground,
                    .20f*BodyScale,~(1<<8),QueryTriggerInteraction.Ignore) && ground.normal.y>.65f)
                {sole=ground.point.y;support=ground.collider;}
                var paw=transform.TransformPoint(new Vector3(0,0,.16f*BodyScale));
                paw.y=sole+pawRadius+.0005f*BodyScale;
                int contacts=Physics.SphereCastNonAlloc(paw,pawRadius,direction,pawContacts,
                    horizontalMove.magnitude+.008f*BodyScale,~(1<<8),QueryTriggerInteraction.Ignore);
                int closest=-1;float distance=float.PositiveInfinity;
                for(int i=0;i<contacts;i++)
                {
                    var hit=pawContacts[i];
                    // The controller handles supporting floors/mats. They must
                    // not hide a mouse or book in the horizontal paw query.
                    if(hit.collider==support || hit.collider.bounds.max.y<sole+.001f*BodyScale || Vector3.Dot(direction,-hit.normal)<.02f)continue;
                    if(hit.distance<distance){closest=i;distance=hit.distance;}
                }
                if(closest>=0)
                {
                    var hit=pawContacts[closest];var point=hit.point;var normal=hit.normal;
                    if(hit.distance==0)
                    {
                        point=hit.collider.ClosestPoint(paw);var outward=paw-point;
                        if(outward.sqrMagnitude>1e-8f)normal=outward.normalized;
                    }
                    PushProp(hit.collider,point,normal);
                }
                if(tracePropContacts && transform.position.x>7.8f && transform.position.y>.7f && Time.time>=nextContactTrace)
                {
                    nextContactTrace=Time.time+.025f;
                    string trace="PAW_TRACE cat="+transform.position.ToString("F4")+" paw="+paw.ToString("F4")+" count="+contacts+" closest="+closest;
                    for(int i=0;i<contacts;i++)trace+=" | "+pawContacts[i].collider.name+" n="+pawContacts[i].normal.ToString("F3")+" d="+pawContacts[i].distance.ToString("F4")+" top="+pawContacts[i].collider.bounds.max.y.ToString("F4");
                    Debug.Log(trace);
                }
            }
            MovementBlocker = null;
            BodyPoints(transform.rotation, out var bodyRear, out var bodyFront);
            if (horizontalMove.sqrMagnitude > .0000001f && Physics.CapsuleCast(bodyRear, bodyFront, BodyRadius,
                horizontalMove.normalized, out var bodyHit, horizontalMove.magnitude + .012f*BodyScale, ~(1 << 8), QueryTriggerInteraction.Ignore))
            {
                MovementBlocker = bodyHit.collider.name;
                PushProp(bodyHit.collider, bodyHit.point, bodyHit.normal);
                // Objects below the belly are handled by the paws and the actual
                // controller capsule; the longer nose guard must not hold them away.
                bool underBelly=bodyHit.collider.GetComponentInParent<PushableProp>() &&
                    bodyHit.collider.bounds.max.y<transform.position.y+.09f*BodyScale;
                if(!underBelly)horizontalMove = horizontalMove.normalized * Mathf.Max(0, bodyHit.distance - .012f*BodyScale);
            }
            var beforeMove=transform.position;
            controller.Move(horizontalMove + Vector3.up * verticalSpeed * Time.deltaTime);
            var bounds = session.definition.activityBounds;
            var position = transform.position;
            position.x = Mathf.Clamp(position.x, bounds.min.x + .16f*BodyScale, bounds.max.x - .16f*BodyScale);
            position.z = Mathf.Clamp(position.z, bounds.min.z + .16f*BodyScale, bounds.max.z - .16f*BodyScale);
            transform.position = position;
            SetMovementAnimation(Vector3.ProjectOnPlane(transform.position-beforeMove,Vector3.up).magnitude/Mathf.Max(.001f,Time.deltaTime)/walkSpeed);
            UpdateNearest();
            if (!Automated && Input.GetKeyDown(KeyCode.E) && Nearest != null) BeginInteraction(Nearest);
        }

        void BodyPoints(Quaternion rotation, out Vector3 rear, out Vector3 front)
        {
            // The cat's body is longer than a vertical controller capsule; protect its nose and rump too.
            rear = transform.position + rotation * (new Vector3(0, .1935f, -.072f)*BodyScale);
            front = transform.position + rotation * (new Vector3(0, .1935f, .2025f)*BodyScale);
        }

        public void ApplyScale(float scale)
        {
            scale=Mathf.Clamp(scale,.25f,2);
            float ratio=scale/BodyScale;
            if(Mathf.Approximately(ratio,1))return;
            bool enabledBefore=controller.enabled;controller.enabled=false;
            float height=controller.height*ratio;
            controller.radius*=ratio;controller.height=height;controller.center*=ratio;
            controller.stepOffset*=ratio;controller.skinWidth*=ratio;controller.minMoveDistance*=ratio;
            visualOffset*=ratio;visual.localPosition=visualOffset;visual.localScale*=ratio;
            walkSpeed*=ratio;runSpeed*=ratio;BodyScale=scale;
            controller.enabled=enabledBefore;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) > .65f) return;
            PushProp(hit.collider, hit.point, hit.normal);
        }

        void PushProp(Collider collider, Vector3 point, Vector3 normal)
        {
            var prop = collider.GetComponentInParent<PushableProp>();
            if (prop != null) prop.RequestContact(contactVelocity,3.5f*BodyScale*BodyScale*BodyScale,point,normal);
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
            SetMovementAnimation(0);
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

        public void TriggerAnimation(string trigger)
        {
            if (animationDriver != null) animationDriver.Trigger(trigger);
            else animator.SetTrigger(trigger);
        }
        void SetMovementAnimation(float speed)
        {
            if (animationDriver != null) animationDriver.Locomotion(speed);
            else animator.SetFloat("Speed", speed, .12f, Time.deltaTime);
        }
    }
}
