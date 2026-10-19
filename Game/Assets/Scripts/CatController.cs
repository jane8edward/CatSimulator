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
        public float chargedJumpHeight = 1.45f;
        public float fullChargeSeconds = .85f;
        public bool IsRiding => Ride != null;
        public RobotVacuum Ride { get; private set; }
        public RobotVacuum SupportVehicle {get;private set;}
        public Vector3 PlatformMotion {get;private set;}
        Vector3 platformPosition;
        Quaternion platformRotation;
        public float JumpCharge => jumpHeld ? Mathf.Clamp01((Time.time-jumpPressedAt-.16f)/Mathf.Max(.1f,fullChargeSeconds-.16f)) : 0;
        bool jumpHeld;
        float jumpPressedAt;
        float requestedHeight;
        float jumpRiseUntil;
        public bool IsBusy { get; private set; }
        public bool Paused { get; set; }
        public CatInteractable Nearest { get; private set; }
        public string MovementBlocker { get; private set; }
        public string HouseAction {get;set;}
        public float HouseActionProgress {get;set;}
        public Vector3 HouseActionTarget {get;set;}
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
        public void SetJumpHeld(bool held)
        {
            if (held)
            {
                if (!jumpHeld && !Paused && !IsBusy && !IsRiding && (controller.isGrounded || Time.time-lastGrounded<.12f))
                { jumpHeld=true; jumpPressedAt=Time.time; }
            }
            else if (jumpHeld)
            {
                requestedHeight=Mathf.Lerp(jumpHeight,chargedJumpHeight,JumpCharge);
                requestedJump=true; jumpHeld=false;
            }
        }
        public void SetRide(RobotVacuum ride)
        {
            Ride=ride; jumpHeld=requestedJump=false; requestedHeight=0; verticalSpeed=-1.4f;
            controller.enabled=ride==null;
            if(SupportVehicle){platformPosition=SupportVehicle.transform.position;platformRotation=SupportVehicle.transform.rotation;}
            if(Nearest)Nearest.Highlight(false);Nearest=null;
            SetMovementAnimation(0);
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            cameraRig = cameraTransform.GetComponent<CatCamera>();
            tracePropContacts=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--trace-prop-contact")>=0;
        }

        void OnApplicationFocus(bool focused)
        { if(!focused){jumpHeld=requestedJump=false;requestedHeight=0;} }

        void Update()
        {
            if (Paused || IsBusy || ContentPreviewMenu.GameplayInputBlocked)
            { jumpHeld=requestedJump=false; requestedHeight=0; return; }
            if (IsRiding)
            {
                if(!Automated && Input.GetKeyDown(KeyCode.Space)) { Ride.TryDismount();SetJumpHeld(true); }
                else { jumpHeld=requestedJump=false; return; }
            }
            CarryWithPlatform();
            var grounded = controller.isGrounded;
            if (grounded)
            {
                lastGrounded = Time.time;
                if (verticalSpeed < 0) verticalSpeed = -1.4f;
                if (!wasGrounded) TriggerAnimation("Land");
            }
            wasGrounded = grounded;
            if (!Automated)
            {
                if(Input.GetKeyDown(KeyCode.Space))SetJumpHeld(true);
                if(Input.GetKeyUp(KeyCode.Space))SetJumpHeld(false);
            }
            if(jumpHeld && !grounded && Time.time-lastGrounded>=.12f)jumpHeld=false;
            var x = Automated ? VerificationMovement.x : (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            var z = Automated ? VerificationMovement.y : (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            // The camera has one heading shared by rendering and movement, sampled before this Update.
            var forward = cameraRig.MovementForward;
            var right = cameraRig.MovementRight;
            var direction = Vector3.ClampMagnitude(forward * z + right * x, 1);
            var speed = (Automated ? VerificationRun : Input.GetKey(KeyCode.LeftShift)) ? runSpeed : walkSpeed;
            if(JumpCharge>0)speed*=.35f;
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
            if (requestedJump && Time.time - lastGrounded < .12f)
            {
                float height=requestedHeight>0?requestedHeight:jumpHeight;
                verticalSpeed = Mathf.Sqrt(height * 2 * 12);
                // A strong crouched launch rises clear of a counter lip before advancing.
                // Ordinary short jumps retain their full horizontal movement.
                jumpRiseUntil=height>jumpHeight+.15f?Time.time+.22f:0;
                lastGrounded = -100;
                SupportVehicle=null;
                TriggerAnimation("Jump");
            }
            requestedJump=false;
            requestedHeight=0;
            verticalSpeed -= 12 * Time.deltaTime;
            var horizontalMove = direction * speed * Time.deltaTime;
            if(Time.time<jumpRiseUntil)horizontalMove=Vector3.zero;
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
                    // Only a pushable contact needs a closest point; non-convex static meshes do not support this query.
                    bool closestPointSupported = hit.collider is BoxCollider || hit.collider is SphereCollider || hit.collider is CapsuleCollider ||
                        hit.collider is MeshCollider mesh && mesh.convex;
                    if(hit.distance==0 && closestPointSupported && hit.collider.GetComponentInParent<PushableProp>() != null)
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
            var flags=controller.Move(horizontalMove + Vector3.up * verticalSpeed * Time.deltaTime);
            if((flags&CollisionFlags.Above)!=0 && verticalSpeed>0)verticalSpeed=0;
            var bounds = session.definition.activityBounds;
            var position = transform.position;
            position.x = Mathf.Clamp(position.x, bounds.min.x + .16f*BodyScale, bounds.max.x - .16f*BodyScale);
            position.z = Mathf.Clamp(position.z, bounds.min.z + .16f*BodyScale, bounds.max.z - .16f*BodyScale);
            transform.position = position;
            RefreshPlatform();
            SetMovementAnimation(Vector3.ProjectOnPlane(transform.position-beforeMove,Vector3.up).magnitude/Mathf.Max(.001f,Time.deltaTime)/walkSpeed);
            UpdateNearest();
            if (!Automated && Input.GetKeyDown(KeyCode.E) && Nearest != null) BeginInteraction(Nearest);
        }

        void CarryWithPlatform()
        {
            PlatformMotion=Vector3.zero;
            if(!SupportVehicle)return;
            var platform=SupportVehicle.transform;
            var turn=platform.rotation*Quaternion.Inverse(platformRotation);
            var destination=platform.position+turn*(transform.position-platformPosition);
            var before=transform.position;
            controller.Move(destination-before);
            transform.rotation=turn*transform.rotation;
            PlatformMotion=transform.position-before;
        }
        void RefreshPlatform()
        {
            SupportVehicle=null;
            if(verticalSpeed<=0 && Physics.Raycast(transform.position+Vector3.up*.06f,Vector3.down,out var hit,.13f,~(1<<8),QueryTriggerInteraction.Ignore)
                && hit.normal.y>.65f)
                SupportVehicle=hit.collider.GetComponentInParent<RobotVacuum>();
            if(SupportVehicle){platformPosition=SupportVehicle.transform.position;platformRotation=SupportVehicle.transform.rotation;}
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
            SupportVehicle=null;
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
            if(Ride)Ride.ReleaseForTeleport();
            SupportVehicle=null;PlatformMotion=Vector3.zero;
            jumpHeld=requestedJump=false;requestedHeight=0;
            jumpRiseUntil=0;
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
