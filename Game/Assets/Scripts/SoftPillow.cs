using UnityEngine;

namespace CatSimulator
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
    public sealed class SoftPillow : MonoBehaviour
    {
        public float maximumSink = .045f;
        public float recoveryTime = .42f;
        public bool useSharedHeightRange;
        public float deformationBaseY,deformationTopY,activationCeilingY;
        // Optional contact mesh lets scanned furniture keep its detailed render geometry.
        public Mesh collisionTemplate;
        public float CurrentIndentation { get; private set; }
        public float CollisionIndentation { get; private set; }
        Mesh mesh;
        MeshCollider surface;
        Vector3[] rest, restWorld, deformed;
        readonly Vector3[] paws = new Vector3[4];
        Bounds restBounds;
        CatController cat;
        float pressure, pressureVelocity, nextCollisionUpdate, lastSurfaceUpdate;
        bool collisionDirty;
        Mesh contactMesh;
        Vector3[] contactRest,contactWorld,contactDeformed;

        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            mesh = Instantiate(filter.sharedMesh);
            mesh.name = name + "_SoftSurface";
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            rest = mesh.vertices;
            restWorld = new Vector3[rest.Length];
            for (var i = 0; i < rest.Length; i++) restWorld[i] = transform.TransformPoint(rest[i]);
            deformed = (Vector3[])rest.Clone();
            surface = GetComponent<MeshCollider>();
            surface.sharedMesh = mesh;
            restBounds = GetComponent<Renderer>().bounds;
            if(collisionTemplate)
            {
                contactMesh=Instantiate(collisionTemplate);contactMesh.MarkDynamic();
                contactRest=contactMesh.vertices;contactWorld=new Vector3[contactRest.Length];contactDeformed=(Vector3[])contactRest.Clone();
                for(int i=0;i<contactRest.Length;i++)contactWorld[i]=transform.TransformPoint(contactRest[i]);
                surface.sharedMesh=contactMesh;
            }
            cat = FindFirstObjectByType<CatController>();
        }

        void LateUpdate()
        {
            if (cat == null) return;
            var elapsed = Time.time - lastSurfaceUpdate;
            if (elapsed < 1f / 30) return;
            lastSurfaceUpdate = Time.time;
            var foot = cat.transform.position;
            var loaded = foot.y >= restBounds.min.y - .03f && foot.y <= Mathf.Max(restBounds.max.y + .12f,activationCeilingY)
                && foot.x > restBounds.min.x - .10f && foot.x < restBounds.max.x + .10f
                && foot.z > restBounds.min.z - .10f && foot.z < restBounds.max.z + .10f;
            if (loaded)
            {
                paws[0] = cat.transform.TransformPoint(new Vector3(-.075f, 0, .16f)*cat.BodyScale);
                paws[1] = cat.transform.TransformPoint(new Vector3(.075f, 0, .16f)*cat.BodyScale);
                paws[2] = cat.transform.TransformPoint(new Vector3(-.075f, 0, -.09f)*cat.BodyScale);
                paws[3] = cat.transform.TransformPoint(new Vector3(.075f, 0, -.09f)*cat.BodyScale);
            }
            var previous = pressure;
            pressure = Mathf.SmoothDamp(pressure, loaded ? 1 : 0, ref pressureVelocity,
                loaded ? .10f : recoveryTime, Mathf.Infinity, elapsed);
            if (pressure < .0005f && previous < .0005f && CurrentIndentation == 0) return;
            CurrentIndentation = 0;
            var localDown = transform.InverseTransformVector(Vector3.down);
            for (var i = 0; i < rest.Length; i++)
            {
                var world = restWorld[i];
                var height = Mathf.InverseLerp(useSharedHeightRange?deformationBaseY:restBounds.min.y,
                    useSharedHeightRange?deformationTopY:restBounds.max.y,world.y);
                var influence = 0f;
                for (var p = 0; p < paws.Length; p++)
                {
                    var dx = world.x - paws[p].x;
                    var dz = world.z - paws[p].z;
                    influence += Mathf.Exp(-(dx * dx + dz * dz) / (.145f * .145f));
                }
                var depth = maximumSink * pressure * Mathf.Min(1, influence / 1.8f)
                    * Mathf.SmoothStep(0, 1, height);
                if (pressure < .0005f) depth = 0;
                deformed[i] = rest[i] + localDown * depth;
                CurrentIndentation = Mathf.Max(CurrentIndentation, depth);
            }
            mesh.vertices = deformed;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if(contactMesh)
            {
                for(int i=0;i<contactRest.Length;i++)
                {
                    var world=contactWorld[i];
                    var height=Mathf.InverseLerp(useSharedHeightRange?deformationBaseY:restBounds.min.y,useSharedHeightRange?deformationTopY:restBounds.max.y,world.y);
                    float influence=0;for(int p=0;p<paws.Length;p++){float dx=world.x-paws[p].x,dz=world.z-paws[p].z;influence+=Mathf.Exp(-(dx*dx+dz*dz)/(.145f*.145f));}
                    float depth=pressure<.0005f?0:maximumSink*pressure*Mathf.Min(1,influence/1.8f)*Mathf.SmoothStep(0,1,height);
                    contactDeformed[i]=contactRest[i]+localDown*depth;
                }
                contactMesh.vertices=contactDeformed;contactMesh.RecalculateBounds();
            }
            collisionDirty = true;
        }

        void FixedUpdate()
        {
            if (!collisionDirty || Time.time < nextCollisionUpdate) return;
            // Re-cook only loaded/recovering cushions, at 15 Hz; both paws and render use this surface.
            surface.sharedMesh = null;
            surface.sharedMesh = contactMesh ? contactMesh : mesh;
            CollisionIndentation = CurrentIndentation;
            nextCollisionUpdate = Time.time + 1f / 15;
            collisionDirty = false;
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh);if(contactMesh)Destroy(contactMesh); }
    }
}
