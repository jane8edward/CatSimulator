using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public static class CatWalkAuthoring
{
    public const string SourcePath = "Assets/Cat/Source/Abyssinian_Cat_WalkRig.fbx";
    public const string ClipPath = "Assets/Cat/Animations/Abyssinian_Walk.anim";
    public const string ControllerPath = "Assets/Cat/Animations/Abyssinian_Walk.controller";
    public const string PrefabPath = "Assets/Cat/Prefabs/Abyssinian_Walking_Cat.prefab";
    public const string ScenePath = "Assets/Cat/Scenes/Walk_Preview.unity";
    public const float Period = 1.2f, Stride = 0.28f, Duty = 0.65f, Lift = 0.030f;
    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    sealed class SavedTransform
    {
        public Transform t;
        public Vector3 p, s, worldP;
        public Quaternion q, worldQ;
        public SavedTransform(Transform v) { t=v; p=v.localPosition; q=v.localRotation; s=v.localScale; worldP=v.position; worldQ=v.rotation; }
        public void Restore() { t.localPosition=p; t.localRotation=q; t.localScale=s; }
    }

    sealed class Leg
    {
        public string label;
        public Transform upper, lower, end, paw;
        public Vector3 initialPaw, footVector, bend;
        public Quaternion pawRotation;
        public float phase, a, b;
        public bool hind;
    }

    sealed class Rig
    {
        public GameObject go;
        public Transform[] bones;
        public Dictionary<string,SavedTransform> rest;
        public Vector3 forward, right;
        public Leg[] legs;
        public float goalError;
        public Rig(GameObject instance)
        {
            go=instance;
            bones=go.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("bone_")).ToArray();
            if (bones.Length!=38) throw new Exception("Expected 38 imported rig bones, found "+bones.Length);
            rest=bones.ToDictionary(t=>t.name,t=>new SavedTransform(t));
            forward=Vector3.ProjectOnPlane(B(9).position-B(1).position,Vector3.up).normalized;
            right=Vector3.Cross(Vector3.up,forward).normalized;
            legs=new[] { Make("LH",27,28,29,30,0,true), Make("LF",17,18,19,20,.25f,false), Make("RH",32,33,34,35,.5f,true), Make("RF",21,22,23,24,.75f,false) };
        }
        public Transform B(int n) => rest["bone_"+n].t;
        Leg Make(string name,int u,int l,int e,int p,float phase,bool hind)
        {
            var leg=new Leg { label=name,upper=B(u),lower=B(l),end=B(e),paw=B(p),phase=phase,hind=hind };
            leg.initialPaw=leg.paw.position;
            leg.footVector=leg.paw.position-leg.end.position;
            leg.pawRotation=leg.paw.rotation;
            leg.a=Vector3.Distance(leg.upper.position,leg.lower.position);
            leg.b=Vector3.Distance(leg.lower.position,leg.end.position);
            Vector3 axis=(leg.end.position-leg.upper.position).normalized;
            leg.bend=Vector3.ProjectOnPlane(leg.lower.position-leg.upper.position,axis).normalized;
            if (leg.bend.sqrMagnitude<.1f) leg.bend=hind?forward:-forward;
            return leg;
        }
        public void Reset() { foreach(var b in bones) rest[b.name].Restore(); }
        public void Pose(float t)
        {
            Reset();
            float cycle=t/Period, w=2*Mathf.PI*cycle;
            // Slightly flex the near-straight source bind pose to allow the walking reach.
            B(1).position += Vector3.up*(-.034f+.003f*Mathf.Sin(2*w+.5f)) + right*(.0015f*Mathf.Sin(w));
            Rotate(B(1),forward,.65f*Mathf.Sin(w));
            Rotate(B(1),Vector3.up,.65f*Mathf.Sin(w+.5f));
            foreach(int n in new[]{7,8,9})
            {
                Rotate(B(n),Vector3.up,.45f*Mathf.Sin(w-(n-7)*.3f));
                Rotate(B(n),right,.50f*Mathf.Sin(2*w-(n-7)*.2f));
            }
            Rotate(B(10),right,-.6f*Mathf.Sin(2*w+.1f));
            Rotate(B(11),right,-.6f*Mathf.Sin(2*w-.1f));
            B(12).rotation=Quaternion.AngleAxis(.35f*Mathf.Sin(2*w-.2f),right)*rest[B(12).name].worldQ;
            for(int j=0;j<5;j++)
            {
                Rotate(B(2+j),Vector3.up,(1.25f-j*.14f)*Mathf.Sin(w-j*.40f));
                Rotate(B(2+j),right,.55f*Mathf.Sin(w-j*.40f+.8f));
            }
            foreach(var leg in legs)
            {
                float p=Mathf.Repeat(cycle-leg.phase,1);
                if (!leg.hind)
                    leg.upper.position += forward*(.007f*Mathf.Cos(2*Mathf.PI*p)) + Vector3.up*(.001f*Mathf.Sin(2*Mathf.PI*p));
                float step, rise, roll;
                FootPath(p,out step,out rise,out roll);
                Vector3 pawTarget=leg.initialPaw+forward*step+Vector3.up*rise;
                Vector3 foot=Quaternion.AngleAxis(roll,right)*leg.footVector;
                Solve(leg,pawTarget-foot);
                leg.end.rotation=Quaternion.FromToRotation(leg.paw.position-leg.end.position,foot)*leg.end.rotation;
                leg.paw.rotation=Quaternion.AngleAxis(roll*.25f,right)*leg.pawRotation;
                goalError=Mathf.Max(goalError,Vector3.Distance(leg.paw.position,pawTarget));
            }
        }
        static void Solve(Leg leg,Vector3 target)
        {
            Vector3 origin=leg.upper.position;
            Vector3 v=target-origin;
            float d=Mathf.Clamp(v.magnitude,Mathf.Abs(leg.a-leg.b)+.00001f,leg.a+leg.b-.00001f);
            Vector3 axis=v.normalized;
            float x=(leg.a*leg.a-leg.b*leg.b+d*d)/(2*d);
            float h=Mathf.Sqrt(Mathf.Max(0,leg.a*leg.a-x*x));
            Vector3 bend=Vector3.ProjectOnPlane(leg.bend,axis).normalized;
            Vector3 joint=origin+axis*x+bend*h;
            leg.upper.rotation=Quaternion.FromToRotation(leg.lower.position-origin,joint-origin)*leg.upper.rotation;
            leg.lower.rotation=Quaternion.FromToRotation(leg.end.position-leg.lower.position,target-leg.lower.position)*leg.lower.rotation;
        }
    }

    static void Rotate(Transform t,Vector3 axis,float degrees) => t.rotation=Quaternion.AngleAxis(degrees,axis)*t.rotation;
    static void FootPath(float p,out float x,out float y,out float roll)
    {
        float reach=Stride*Duty*.5f;
        if (p<Duty) { x=reach-Stride*p; y=0; roll=0; return; }
        float s=(p-Duty)/(1-Duty);
        float m=-Stride*(1-Duty);
        x=(2*s*s*s-3*s*s+1)*(-reach)+(s*s*s-2*s*s+s)*m+(-2*s*s*s+3*s*s)*reach+(s*s*s-s*s)*m;
        y=Lift*Mathf.Pow(Mathf.Sin(Mathf.PI*s),2);
        roll=6*Mathf.Sin(Mathf.PI*s);
    }

    [MenuItem("Cat/Build Walking Animation")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before rebuilding animation.");
        foreach(string dir in new[]{"Assets/Cat/Animations","Assets/Cat/Materials","Assets/Cat/Prefabs","Assets/Cat/Scenes","Assets/Cat/Reports"}) Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();
        ConfigureImporter();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (!source) throw new Exception("FBX model could not be loaded.");
        var go=UnityEngine.Object.Instantiate(source);
        go.name="Abyssinian_Cat";
        var animator=go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        animator.enabled=false;
        var rig=new Rig(go);
        File.WriteAllText(Path.Combine(ProjectRoot,"Assets/Cat/Reports/Rig_Mapping.json"),JsonUtility.ToJson(Audit(rig),true));
        var clip=Bake(rig);
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (existing) { EditorUtility.CopySerialized(clip,existing); UnityEngine.Object.DestroyImmediate(clip); clip=existing; }
        else AssetDatabase.CreateAsset(clip,ClipPath);
        Material material=CreateCatMaterial();
        foreach(var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.sharedMaterials=Enumerable.Repeat(material,smr.sharedMaterials.Length).ToArray();
            smr.updateWhenOffscreen=true;
            smr.localBounds=new Bounds(smr.localBounds.center,smr.localBounds.size+Vector3.one*.5f);
        }
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller) controller=AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine=controller.layers[0].stateMachine;
        var state=machine.states.FirstOrDefault(s=>s.state.name=="Walk").state;
        if (!state) state=machine.AddState("Walk");
        state.motion=clip; machine.defaultState=state; state.speed=1;
        animator.runtimeAnimatorController=controller;
        animator.applyRootMotion=false;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        animator.enabled=true;
        rig.Reset();
        var playback=go.GetComponent<CatWalkPlayback>() ?? go.AddComponent<CatWalkPlayback>();
        playback.animator=animator; playback.subject=go.transform; playback.forward=rig.forward;
        playback.cycleSeconds=Period; playback.strideMeters=Stride;
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
        UnityEngine.Object.DestroyImmediate(go);
        CreateScene(prefab,rig.forward);
        AssetDatabase.SaveAssets();
        Validate();
        Selection.activeObject=GameObject.Find("Abyssinian_Walking_Cat");
        Debug.Log("CAT_WALK_BUILD_COMPLETE: "+ClipPath);
    }

    static void ConfigureImporter()
    {
        var importer=(ModelImporter)AssetImporter.GetAtPath(SourcePath);
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation=false;
        importer.optimizeGameObjects=false;
        importer.isReadable=true;
        importer.SaveAndReimport();
        string normal=AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Cat/Source"}).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.ToLowerInvariant().Contains("_normal."));
        if (normal!=null)
        {
            var ti=(TextureImporter)AssetImporter.GetAtPath(normal);
            ti.textureType=TextureImporterType.NormalMap;
            ti.maxTextureSize=2048;
            ti.SaveAndReimport();
        }
    }

    static AnimationClip Bake(Rig rig)
    {
        var clip=new AnimationClip { name="Abyssinian_Walk",frameRate=30,wrapMode=WrapMode.Loop,legacy=false };
        const int intervals=144;
        var channels=new Dictionary<string,float[][]>();
        foreach(var b in rig.bones) channels[b.name]=Enumerable.Range(0,7).Select(_=>new float[intervals+1]).ToArray();
        foreach(var frame in Enumerable.Range(0,intervals+1))
        {
            rig.Pose(Period*frame/intervals);
            foreach(var b in rig.bones)
            {
                var c=channels[b.name]; var p=b.localPosition; var q=b.localRotation;
                if (frame>0 && Quaternion.Dot(new Quaternion(c[3][frame-1],c[4][frame-1],c[5][frame-1],c[6][frame-1]),q)<0) q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                c[0][frame]=p.x;c[1][frame]=p.y;c[2][frame]=p.z;c[3][frame]=q.x;c[4][frame]=q.y;c[5][frame]=q.z;c[6][frame]=q.w;
            }
        }
        string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
        float dt=Period/intervals;
        foreach(var b in rig.bones)
        {
            string path=AnimationUtility.CalculateTransformPath(b,rig.go.transform);
            for(int c=0;c<7;c++)
            {
                var values=channels[b.name][c];
                var keys=new Keyframe[intervals+1];
                for(int i=0;i<=intervals;i++)
                {
                    int before=i==0?intervals-1:i-1, after=i==intervals?1:i+1;
                    float slope=(values[after]-values[before])/(2*dt);
                    keys[i]=new Keyframe(i*dt,values[i],slope,slope);
                }
                var curve=new AnimationCurve(keys) { preWrapMode=WrapMode.Loop,postWrapMode=WrapMode.Loop };
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),properties[c]),curve);
            }
        }
        clip.EnsureQuaternionContinuity();
        var settings=AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime=true; settings.loopBlend=false;
        settings.startTime=0; settings.stopTime=Period;
        settings.keepOriginalOrientation=true; settings.keepOriginalPositionY=true; settings.keepOriginalPositionXZ=true;
        settings.loopBlendOrientation=true; settings.loopBlendPositionY=true; settings.loopBlendPositionXZ=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        rig.Reset();
        return clip;
    }

    static Material CreateCatMaterial()
    {
        string path="Assets/Cat/Materials/Abyssinian_Coat.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader=GraphicsSettings.defaultRenderPipeline?Shader.Find("Universal Render Pipeline/Lit"):Shader.Find("Standard");
        if (!mat) { mat=new Material(shader); AssetDatabase.CreateAsset(mat,path); }
        mat.shader=shader;
        var texPaths=AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Cat/Source"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        string basePath=texPaths.First(p=>p.ToLowerInvariant().Contains("_basecolor."));
        var baseMap=AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
        mat.mainTexture=baseMap;
        if(mat.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",baseMap);
        if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",0);
        if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",.22f);
        if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.22f);
        string normalPath=texPaths.FirstOrDefault(p=>p.ToLowerInvariant().Contains("_normal."));
        if(normalPath!=null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            mat.SetFloat("_BumpScale",.35f);mat.EnableKeyword("_NORMALMAP");
        }
        return mat;
    }

    static void CreateScene(GameObject prefab,Vector3 forward)
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        QualitySettings.shadowDistance=8;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.72f,.75f,.8f);
        RenderSettings.ambientEquatorColor=new Color(.42f,.44f,.48f);
        RenderSettings.ambientGroundColor=new Color(.25f,.27f,.3f);
        var cat=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        cat.name="Abyssinian_Walking_Cat";
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Ground";floor.transform.localScale=new Vector3(10,1,10);
        var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Cat/Materials/Ground.mat");
        if(!mat) { mat=new Material(Shader.Find(GraphicsSettings.defaultRenderPipeline ? "Universal Render Pipeline/Lit" : "Standard")); AssetDatabase.CreateAsset(mat,"Assets/Cat/Materials/Ground.mat"); }
        mat.color=new Color(.46f,.49f,.53f);mat.SetFloat("_Glossiness",.08f);
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Cat/Materials/GroundGrid.asset");
        if(!texture) { texture=new Texture2D(64,64,TextureFormat.RGB24,false); AssetDatabase.CreateAsset(texture,"Assets/Cat/Materials/GroundGrid.asset"); }
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            texture.SetPixel(x,y,(x<2||y<2)?new Color(.75f,.78f,.8f):Color.white);
        texture.Apply();texture.wrapMode=TextureWrapMode.Repeat;mat.mainTexture=texture;mat.mainTextureScale=new Vector2(400,400);
        EditorUtility.SetDirty(texture);EditorUtility.SetDirty(mat);floor.GetComponent<Renderer>().sharedMaterial=mat;
        foreach(var v in new[]{new Vector3(40,-35,0),new Vector3(65,140,0)})
        {
            var lightGO=new GameObject(v.y<0?"Key Light":"Fill Light");
            var light=lightGO.AddComponent<Light>();light.type=LightType.Directional;light.intensity=v.y<0?1.15f:.45f;light.color=new Color(1,.94f,.85f);light.shadows=v.y<0?LightShadows.Soft:LightShadows.None;lightGO.transform.eulerAngles=v;
            light.shadowBias=.005f;light.shadowNormalBias=.05f;
        }
        var cameraGO=new GameObject("Main Camera");cameraGO.tag="MainCamera";
        var camera=cameraGO.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.56f;camera.nearClipPlane=.02f;camera.farClipPlane=150;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.19f);
        Vector3 side=Vector3.Cross(Vector3.up,forward).normalized;
        cameraGO.transform.position=cat.transform.position+forward*1.25f+side*1.65f+Vector3.up*.95f;
        cameraGO.transform.LookAt(cat.transform.position+Vector3.up*.29f);
        cameraGO.AddComponent<AudioListener>();
        cat.GetComponent<CatWalkPlayback>().previewCamera=cameraGO.transform;
        cat.GetComponent<CatWalkPlayback>().previewLoopDistance=7;
        PrefabUtility.RecordPrefabInstancePropertyModifications(cat.GetComponent<CatWalkPlayback>());
        EditorUtility.SetDirty(cat.GetComponent<CatWalkPlayback>());
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        PlayerSettings.runInBackground=true;
        if(SceneView.lastActiveSceneView)
        {
            SceneView.lastActiveSceneView.pivot=Vector3.up*.28f;
            SceneView.lastActiveSceneView.rotation=cameraGO.transform.rotation;
            SceneView.lastActiveSceneView.size=1.15f;
            SceneView.lastActiveSceneView.Repaint();
        }
    }

    [Serializable] public class BoneRecord { public string name,parent,path;public Vector3 position; }
    [Serializable] public class RigAudit { public int bones,vertices; public Vector3 forward;public bool avatarValid;public BoneRecord[] mapping; }
    static RigAudit Audit(Rig rig) => new RigAudit { bones=rig.bones.Length,vertices=rig.go.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.vertexCount,forward=rig.forward,avatarValid=rig.go.GetComponent<Animator>().avatar && rig.go.GetComponent<Animator>().avatar.isValid,mapping=rig.bones.Select(b=>new BoneRecord { name=b.name,parent=b.parent?b.parent.name:"",path=AnimationUtility.CalculateTransformPath(b,rig.go.transform),position=b.position }).ToArray() };

    [Serializable] public class CheckReport
    {
        public string rigType="Generic",clip,notes;
        public int boneCount,curveCount,sampledPoses,unweightedVertexCount;
        public float cycleSeconds,strideMeters,speedMetersPerSecond,maxIKGoalErrorMeters,maxPlantedFootDriftMetersPerSample,maxPlantedFootSpeedMetersPerSecond,lowestMeshVertexMeters,maxBoneLengthDifferenceMeters,seamPositionMeters,seamRotationDegrees;
        public bool avatarValid,loopEnabled;
        public string[] contactOrder={"LH","LF","RH","RF"};
        public float[] contactSeconds={0,.3f,.6f,.9f};
        public float[] pawMeshMotionMeters=new float[4];
    }

    [MenuItem("Cat/Validate Walking Animation")]
    public static void Validate()
    {
        var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        instance.name="Validation_Instance";
        instance.GetComponent<Animator>().enabled=false;
        instance.GetComponent<CatWalkPlayback>().enabled=false;
        var rig=new Rig(instance);
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        var report=new CheckReport { clip=ClipPath,boneCount=rig.bones.Length,curveCount=AnimationUtility.GetCurveBindings(clip).Length,cycleSeconds=clip.length,strideMeters=Stride,speedMetersPerSecond=Stride/Period,sampledPoses=241,lowestMeshVertexMeters=float.PositiveInfinity,avatarValid=instance.GetComponent<Animator>().avatar && instance.GetComponent<Animator>().avatar.isValid,loopEnabled=AnimationUtility.GetAnimationClipSettings(clip).loopTime,notes="Baked on a repaired copy of the source 38-bone rig. Original FBX weights omitted hind limbs/head/tail; reweighted with Blender bone heat (four influences per vertex). Front shoulder translation approximates scapular follow-through; no separate anatomical scapula bone exists. World travel is provided by CatWalkPlayback at the matched stride speed. Muscle simulation is not included." };
        var previous=new Dictionary<string,Vector3>();
        var initialLengths=rig.legs.SelectMany(l=>new[]{l.a,l.b}).ToArray();
        var startP=new Vector3[38];var startQ=new Quaternion[38];
        var mesh=new Mesh();
        var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>();
        var originalVertices=new Dictionary<int,Vector3[]>();
        var pawVertices=new Dictionary<int,List<(int vertex,int leg)>>();
        int[] pawBones={30,20,35,24};
        for(int si=0;si<skins.Length;si++)
        {
            var s=skins[si];var tracked=new List<(int,int)>();var weights=s.sharedMesh.boneWeights;
            for(int vi=0;vi<weights.Length;vi++)
            {
                var bw=weights[vi];
                if(bw.weight0+bw.weight1+bw.weight2+bw.weight3<.999f)report.unweightedVertexCount++;
                int[] indices={bw.boneIndex0,bw.boneIndex1,bw.boneIndex2,bw.boneIndex3};float[] amounts={bw.weight0,bw.weight1,bw.weight2,bw.weight3};
                for(int leg=0;leg<4;leg++)
                    if(Enumerable.Range(0,4).Any(w=>amounts[w]>.2f && s.bones[indices[w]].name=="bone_"+pawBones[leg]))tracked.Add((vi,leg));
            }
            pawVertices[si]=tracked;
        }
        for(int i=0;i<=240;i++)
        {
            float t=Period*i/240;
            rig.Reset();clip.SampleAnimation(instance,t);
            int k=0;
            foreach(var leg in rig.legs)
            {
                float p=Mathf.Repeat(t/Period-leg.phase,1);
                Vector3 planted=leg.paw.position+rig.forward*(Stride*t/Period);
                if(p>.025f && p<Duty-.025f && previous.ContainsKey(leg.label))
                {
                    float drift=Vector3.Distance(planted,previous[leg.label]);
                    report.maxPlantedFootDriftMetersPerSample=Mathf.Max(report.maxPlantedFootDriftMetersPerSample,drift);
                    report.maxPlantedFootSpeedMetersPerSecond=Mathf.Max(report.maxPlantedFootSpeedMetersPerSecond,drift/(Period/240));
                }
                previous[leg.label]=planted;
                report.maxBoneLengthDifferenceMeters=Mathf.Max(report.maxBoneLengthDifferenceMeters,Mathf.Abs(Vector3.Distance(leg.upper.position,leg.lower.position)-initialLengths[k++]));
                report.maxBoneLengthDifferenceMeters=Mathf.Max(report.maxBoneLengthDifferenceMeters,Mathf.Abs(Vector3.Distance(leg.lower.position,leg.end.position)-initialLengths[k++]));
            }
            if(i%2==0)
            {
                for(int si=0;si<skins.Length;si++)
                {
                    var smr=skins[si];
                    smr.BakeMesh(mesh,true);
                    var worldVertices=mesh.vertices.Select(smr.transform.TransformPoint).ToArray();
                    foreach(var v in worldVertices) report.lowestMeshVertexMeters=Mathf.Min(report.lowestMeshVertexMeters,v.y);
                    if(i==0)originalVertices[si]=worldVertices;
                    else foreach(var tracked in pawVertices[si]) report.pawMeshMotionMeters[tracked.leg]=Mathf.Max(report.pawMeshMotionMeters[tracked.leg],Vector3.Distance(worldVertices[tracked.vertex],originalVertices[si][tracked.vertex]));
                }
            }
            if(i==0)for(int b=0;b<38;b++) { startP[b]=rig.bones[b].localPosition;startQ[b]=rig.bones[b].localRotation; }
            if(i==240)for(int b=0;b<38;b++)
            {
                report.seamPositionMeters=Mathf.Max(report.seamPositionMeters,Vector3.Distance(startP[b],rig.bones[b].localPosition));
                report.seamRotationDegrees=Mathf.Max(report.seamRotationDegrees,Quaternion.Angle(startQ[b],rig.bones[b].localRotation));
            }
        }
        for(int i=0;i<=240;i++)rig.Pose(Period*i/240);
        report.maxIKGoalErrorMeters=rig.goalError;
        UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(instance);
        File.WriteAllText(Path.Combine(ProjectRoot,"Assets/Cat/Reports/Walk_Checks.json"),JsonUtility.ToJson(report,true));
        AssetDatabase.Refresh();
        if(report.maxIKGoalErrorMeters>.005f || report.maxPlantedFootSpeedMetersPerSecond>.015f || report.seamPositionMeters>.001f || report.seamRotationDegrees>.2f || report.lowestMeshVertexMeters<-.003f || report.unweightedVertexCount>0 || report.pawMeshMotionMeters.Any(v=>v<.015f))
            throw new Exception("Walk validation needs repair: "+JsonUtility.ToJson(report));
        Debug.Log("CAT_WALK_CHECKS "+JsonUtility.ToJson(report));
    }

    [MenuItem("Cat/Render Preview Frames")]
    public static void RenderPreviewFrames()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play mode before rendering offline frames.");
        string output=Path.Combine(ProjectRoot,"Preview/Frames");Directory.CreateDirectory(output);
        var cat=GameObject.Find("Abyssinian_Walking_Cat");
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        var playback=cat.GetComponent<CatWalkPlayback>();
        var rig=new Rig(cat);var camera=Camera.main;
        var animator=cat.GetComponent<Animator>();bool animatorEnabled=animator.enabled;animator.enabled=false;
        // CPU-baked meshes ensure each offline frame refreshes its skinning inside a single Editor call.
        var skins=cat.GetComponentsInChildren<SkinnedMeshRenderer>();
        var baked=skins.Select(_=>new Mesh()).ToArray();
        var renderers=skins.Select(s=>
        {
            var g=new GameObject("Offline_Skin_Frame");g.transform.SetParent(s.transform,false);
            g.AddComponent<MeshFilter>();var r=g.AddComponent<MeshRenderer>();r.sharedMaterials=s.sharedMaterials;
            s.enabled=false;return r;
        }).ToArray();
        Vector3 initial=cat.transform.position,offset=camera.transform.position-initial;
        var rt=new RenderTexture(960,720,24);var image=new Texture2D(960,720,TextureFormat.RGB24,false);
        var oldActive=RenderTexture.active;var oldTarget=camera.targetTexture;
        try
        {
            for(int frame=0;frame<144;frame++)
            {
                float t=frame/30f;
                rig.Reset();clip.SampleAnimation(cat,Mathf.Repeat(t,Period));
                cat.transform.position=initial+rig.forward*(Stride/Period)*t;
                for(int i=0;i<skins.Length;i++) { skins[i].BakeMesh(baked[i],true);renderers[i].GetComponent<MeshFilter>().sharedMesh=baked[i]; }
                camera.transform.position=cat.transform.position+offset;camera.transform.LookAt(cat.transform.position+Vector3.up*.29f);
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,"frame_"+frame.ToString("D4")+".png"),image.EncodeToPNG());
            }
        }
        finally
        {
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
            cat.transform.position=initial;camera.transform.position=initial+offset;rig.Reset();
            for(int i=0;i<skins.Length;i++) { skins[i].enabled=true;UnityEngine.Object.DestroyImmediate(renderers[i].gameObject);UnityEngine.Object.DestroyImmediate(baked[i]); }
            animator.enabled=animatorEnabled;
        }
        Debug.Log("CAT_WALK_PREVIEW_RENDERED "+output);
    }
}
