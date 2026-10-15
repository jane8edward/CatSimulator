using UnityEngine;
using UnityEngine.Rendering;

namespace CatSimulator
{
    // Read the mouse before the player moves. Obstacles change distance only, never heading.
    [DefaultExecutionOrder(-100)]
    public sealed class CatCamera : MonoBehaviour
    {
        public CatController cat;
        public float distance = 2.05f;
        public float yaw;
        public float pitch = 16;
        public bool Automated;
        float cameraDistance, distanceVelocity, clearFor;
        Transform cachedVisual;
        Renderer[] catRenderers;
        ShadowCastingMode[] shadowModes;
        bool[] forcedOff;
        public bool CloseView { get; private set; }
        public float ActualDistance => cameraDistance;
        public Quaternion ViewRotation => Quaternion.Euler(pitch,yaw,0);
        public Vector3 MovementForward => Vector3.ProjectOnPlane(ViewRotation*Vector3.forward,Vector3.up).normalized;
        public Vector3 MovementRight => Vector3.Cross(Vector3.up,MovementForward);
        public float AutomaticYawOffset => Mathf.DeltaAngle(yaw,transform.eulerAngles.y);
        public float CollisionRadius => .09f*cat.BodyScale;
        public Vector3 FollowTarget => cat.transform.position+Vector3.up*(.26f*cat.BodyScale);

        void Start()
        {
            GetComponent<Camera>().nearClipPlane=.025f*cat.BodyScale;
            SnapToTarget();
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        }

        void Update()
        {
            if(cat.Paused)return;
            if(!Automated)
            {
                yaw+=Input.GetAxis("Mouse X")*2.2f;
                pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*1.8f,-20,65);
                distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.15f,.65f,3.2f);
            }
            transform.rotation=ViewRotation;
        }

        void LateUpdate()
        {
            if(cat.Paused)return;
            var rotation=ViewRotation;
            var target=FollowTarget;
            float available=ClearDistance(target,rotation*Vector3.back);
            if(available<cameraDistance)
            {
                cameraDistance=available;distanceVelocity=0;clearFor=0;
            }
            else
            {
                // Wait for sustained clearance, so table edges do not repeatedly pump the zoom.
                clearFor=available>cameraDistance+.04f?clearFor+Time.unscaledDeltaTime:0;
                if(clearFor>=.18f)
                    cameraDistance=Mathf.Min(available,Mathf.SmoothDamp(cameraDistance,available,ref distanceVelocity,.25f,Mathf.Infinity,Time.unscaledDeltaTime));
            }
            transform.SetPositionAndRotation(target+rotation*Vector3.back*cameraDistance,rotation);
            SetCloseView(cameraDistance<(CloseView ? .58f : .42f)*cat.BodyScale);
        }

        public void SnapToTarget()
        {
            clearFor=distanceVelocity=0;
            cameraDistance=ClearDistance(FollowTarget,ViewRotation*Vector3.back);
            transform.SetPositionAndRotation(FollowTarget+ViewRotation*Vector3.back*cameraDistance,ViewRotation);
            SetCloseView(cameraDistance<.42f*cat.BodyScale);
        }

        float ClearDistance(Vector3 target,Vector3 direction)
        {
            if(Physics.SphereCast(target,CollisionRadius,direction,out var hit,distance,~(1<<8),QueryTriggerInteraction.Ignore))
                return Mathf.Max(0,hit.distance-.015f*cat.BodyScale);
            return distance;
        }

        void SetCloseView(bool close)
        {
            if(cachedVisual!=cat.visual)
            {
                RestoreRenderers();cachedVisual=cat.visual;
                catRenderers=cachedVisual.GetComponentsInChildren<Renderer>(true);
                shadowModes=new ShadowCastingMode[catRenderers.Length];forcedOff=new bool[catRenderers.Length];
                for(int i=0;i<catRenderers.Length;i++)
                {shadowModes[i]=catRenderers[i].shadowCastingMode;forcedOff[i]=catRenderers[i].forceRenderingOff;}
            }
            CloseView=close;
            for(int i=0;i<catRenderers.Length;i++)
            {
                if(!catRenderers[i])continue;
                catRenderers[i].shadowCastingMode=close && shadowModes[i]!=ShadowCastingMode.Off?ShadowCastingMode.ShadowsOnly:shadowModes[i];
                catRenderers[i].forceRenderingOff=forcedOff[i] || (close && shadowModes[i]==ShadowCastingMode.Off);
            }
        }

        void OnDisable()=>RestoreRenderers();
        void RestoreRenderers()
        {
            if(catRenderers==null)return;
            for(int i=0;i<catRenderers.Length;i++)if(catRenderers[i])
            {catRenderers[i].shadowCastingMode=shadowModes[i];catRenderers[i].forceRenderingOff=forcedOff[i];}
            CloseView=false;
        }
    }
}
