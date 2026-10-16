using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CatSimulator
{
    // Opt-in standalone acceptance of the second level's furniture, contacts and soft surfaces.
    public sealed class NewHouseUpgradeVerification : MonoBehaviour
    {
        [Serializable] class Check { public string name, detail; public bool passed; }
        [Serializable] class Report { public bool passed; public string stage,gpu; public float medianFps,p5Fps; public Check[] checks; public string[] errors; }
        readonly List<Check> checks=new List<Check>();readonly List<string> errors=new List<string>();
        readonly Report report=new Report();
        string output;float began;CatController cat;CatCamera rig;NewHouseExploration game;NewHouseLayout layout;NewHouseItem[] items;
        string previousCat;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if(!Environment.GetCommandLineArgs().Any(a=>a=="--check-new-house-upgrade"||a=="--check-new-house-exit"||a=="--check-new-house-perf"))return;
            var go=new GameObject("NewHouseUpgradeVerification");DontDestroyOnLoad(go);go.AddComponent<NewHouseUpgradeVerification>();
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        void Update(){if(output!=null&&Time.realtimeSinceStartup-began>240){errors.Add("Timeout: "+report.stage);Finish();}}
        void Stage(string name){report.stage=name;Debug.Log("NEW_HOUSE_UPGRADE_CHECK "+name);}
        void CheckIt(string name,bool passed,string detail=""){checks.Add(new Check{name=name,passed=passed,detail=detail});Debug.Log("NH_UPGRADE "+name+"="+passed+" "+detail);}
        NewHouseItem Item(string n)=>items.Single(i=>i.placement.Contains(n));
        Vector3 P(float x,float z,float y=.035f)=>layout.Remap(new Vector3(-x,y,-z));
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--verification-output");
            output=Path.GetFullPath(index>=0?args[index+1]:Path.Combine(Application.persistentDataPath,"NewHouseUpgrade"));Directory.CreateDirectory(output);
            began=Time.realtimeSinceStartup;report.gpu=SystemInfo.graphicsDeviceName;previousCat=CatCatalog.Selected.id;
            yield return new WaitForSecondsRealtime(.4f);
            var menu=ContentPreviewMenu.Instance;menu.ShowLevels();yield return null;
            var choices=menu.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith("Level")).ToArray();
            CheckIt("two-level-menu",choices.Length==2&&choices.Any(b=>b.name=="Level1")&&choices.Any(b=>b.name=="Level2"));
            CheckIt("legacy-house-not-shipped",!UnityEngine.Application.CanStreamedLevelBeLoaded("Home90")&&!UnityEngine.Application.CanStreamedLevelBeLoaded("Home90_Preview"));
            VerificationRenderTarget.Initialize(FindFirstObjectByType<CatModelGallery>().galleryCamera);yield return Capture("level-selection");
            choices.Single(b=>b.name=="Level2").onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
            while(!FindFirstObjectByType<NewHouseExploration>().IsReady)yield return null;
            Connect();
            CheckIt("second-level-is-new-house",UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==ContentPreviewMenu.HomeScene&&cat.session.definition.levelId=="home-02"&&cat.session.definition.IsExploration);
            if(args.Contains("--check-new-house-perf")){yield return ProfileWindow();Finish();yield break;}
            if(args.Contains("--check-new-house-exit"))
            {
                for(int n=0;n<3;n++){yield return Overview("exit-view-"+n);ContentPreviewMenu.Instance.LoadLevel(2);yield return new WaitForSecondsRealtime(.4f);Connect();yield return new WaitForSeconds(.2f);}
                CheckIt("scene-mode-and-exit-smoke",true);Finish();yield break;
            }
            Stage("layout-and-stability");yield return new WaitForSeconds(1.5f);
            CheckIt("expanded-house",Mathf.Abs(layout.horizontalExpansion-1.2f)<.001f,cat.session.definition.activityBounds.size.ToString("F3"));
            CheckIt("smaller-cat",Mathf.Abs(cat.BodyScale-.675f)<.001f,"scale="+cat.BodyScale);
            var bodies=items.Where(i=>i.GetComponent<Rigidbody>()).ToArray();
            CheckIt("dynamic-props",bodies.Length>=34,"count="+bodies.Length);
            CheckIt("soft-surface-coverage",layout.GetComponentsInChildren<SoftPillow>().Length>=19,"count="+layout.GetComponentsInChildren<SoftPillow>().Length);
            var cornerSofa=Item("LIV_NH007").Bounds;
            float backGap=-5.112f-cornerSofa.max.x,sideGap=-1.992f-cornerSofa.max.z;
            CheckIt("sofa-wall-corner",backGap>=.08f&&backGap<=.12f&&sideGap>=.08f&&sideGap<=.12f,"back="+backGap.ToString("F3")+" side="+sideGap.ToString("F3"));
            CheckIt("continuous-contacts",bodies.All(i=>i.GetComponent<PushableProp>()&&i.GetComponent<Rigidbody>().collisionDetectionMode==CollisionDetectionMode.ContinuousDynamic));
            CheckIt("initial-stability",bodies.All(i=>i.GetComponent<Rigidbody>().linearVelocity.magnitude<.06f&&(!i.placement.Contains("Plant")&&!i.placement.Contains("Lamp")||Vector3.Angle(i.transform.up,Vector3.up)<10)),string.Join(";",bodies.Where(i=>i.GetComponent<Rigidbody>().linearVelocity.magnitude>=.06f||Vector3.Angle(i.transform.up,Vector3.up)>10).Select(i=>i.placement+":"+i.GetComponent<Rigidbody>().linearVelocity.magnitude.ToString("F3")+" angle="+Vector3.Angle(i.transform.up,Vector3.up).ToString("F1"))));
            var table=Item("LIV_NH062");var chairs=new[]{Item("LIV_NH059_DiningChair_01"),Item("LIV_NH059_DiningChair_02"),Item("LIV_NH052")};
            CheckIt("dining-clearance",chairs.All(i=>!i.Bounds.Intersects(table.Bounds)&&i.Bounds.min.z> -7.13f)&&!chairs[0].Bounds.Intersects(chairs[1].Bounds));
            var wardrobe=Item("MBR_NH076");var fridge=Item("KIT_NH034");
            CheckIt("wardrobe-front",Vector3.Dot(wardrobe.transform.rotation*Vector3.left,Vector3.right)>.99f);
            CheckIt("fridge-front",Vector3.Dot(fridge.transform.rotation*Vector3.forward,Vector3.back)>.99f);
            CheckIt("cat-bowls-size",Item("LIV_NH068").Bounds.size.x<.36f&&Item("LIV_NH068").Bounds.size.y<.125f);
            CheckIt("two-master-pillows",items.Count(i=>i.placement.StartsWith("NH_MBR")&&i.placement.Contains("NH048"))==2&&!items.Any(i=>i.placement.StartsWith("NH_MBR")&&(i.placement.Contains("NH016")||i.placement.Contains("NH033"))));
            var floating=new List<string>();
            foreach(var item in bodies)
            {
                var b=item.Bounds;float baseY=item.hasContactBase?item.transform.TransformPoint(item.contactBaseLocal).y:b.min.y;
                var hits=Physics.RaycastAll(new Vector3(b.center.x,baseY+.035f,b.center.z),Vector3.down,2).Where(h=>h.collider.attachedRigidbody!=item.GetComponent<Rigidbody>()&&h.normal.y>.5f).OrderBy(h=>h.distance).ToArray();
                if(hits.Length==0||baseY-hits[0].point.y>.025f||baseY-hits[0].point.y<-.04f)floating.Add(item.placement+" gap="+(hits.Length==0?"none":(baseY-hits[0].point.y).ToString("F4")));
            }
            CheckIt("props-supported",floating.Count==0,string.Join(";",floating));
            yield return Capture("gameplay");yield return Overview("overview");
            yield return Detail("sofa-corner",cornerSofa.center,new Vector3(-3,1.8f,-.6f),2.0f);
            yield return Detail("living",new Vector3(-8.1f,.7f,-4.8f),new Vector3(3,4,4),3.1f);
            yield return Detail("bedroom",new Vector3(-2.6f,.8f,-2.9f),new Vector3(3,4,4),2.6f);
            yield return Detail("laundry",new Vector3(-5.8f,1.1f,-1.05f),new Vector3(-3,2,-2),1.4f);
            Stage("gentle-prop-contact");
            foreach(var id in new[]{"LIV_NH012","LIV_NH008","LIV_NH029_PlantTrailing_02","LIV_NH015","KIT_NH061_ChoppingBoard"})
            {
                var item=Item(id);var body=item.GetComponent<Rigidbody>();var position=body.position;var rotation=body.rotation;
                body.linearVelocity=body.angularVelocity=Vector3.zero;
                item.GetComponent<PushableProp>().RequestContact(Vector3.forward*.7f,3.5f*Mathf.Pow(cat.BodyScale,3),body.worldCenterOfMass,Vector3.back);
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                CheckIt("finite-contact-"+id,body.linearVelocity.magnitude<.73f&&float.IsFinite(body.angularVelocity.magnitude),"m/s="+body.linearVelocity.magnitude.ToString("F4"));
                body.position=position;body.rotation=rotation;body.linearVelocity=body.angularVelocity=Vector3.zero;body.Sleep();
            }
            Stage("soft-surfaces");
            foreach(var id in new[]{"LIV_NH007","MBR_NH050","MBR_NH048_SleepingPillow_01","OFF_NH051","LIV_NH023","MBR_NH073"})
            {
                var item=Item(id);var surface=item.GetComponentsInChildren<SoftPillow>().First(s=>s.GetComponentInParent<NewHouseItem>()==item&&!s.name.Contains("Cover"));
                var bounds=surface.GetComponent<Renderer>().bounds;var point=bounds.center;
                if(id.Contains("NH007"))point.x-=.28f;if(id.Contains("NH050"))point.z+=.25f;
                var shape=surface.GetComponent<MeshCollider>();
                if(!shape.Raycast(new Ray(new Vector3(point.x,3,point.z),Vector3.down),out var hit,4)){CheckIt("soft-hit-"+id,false);continue;}
                var mesh=surface.GetComponent<MeshFilter>().sharedMesh;var original=mesh.vertices;
                cat.Teleport(hit.point+Vector3.up*.025f);yield return new WaitForSeconds(.3f);
                var softFrames=new List<float>();float softStop=Time.realtimeSinceStartup+1;while(Time.realtimeSinceStartup<softStop){yield return null;softFrames.Add(Time.unscaledDeltaTime);}softFrames.Sort();
                float softFps=1/softFrames[softFrames.Count/2];CheckIt("soft-rendered-60fps-"+id,softFps>=59,"median="+softFps.ToString("F1"));
                float indent=surface.CurrentIndentation;float collision=surface.CollisionIndentation;
                var changed=mesh.vertices;float actual=0;for(int v=0;v<original.Length;v++)actual=Mathf.Max(actual,surface.transform.TransformVector(original[v]-changed[v]).magnitude);
                float footSink=hit.point.y-cat.transform.position.y;
                CheckIt("soft-load-"+id,indent>.012f&&collision>.01f&&actual>.012f&&footSink>.01f&&footSink<.065f,"visual="+indent.ToString("F4")+" collider="+collision.ToString("F4")+" actual="+actual.ToString("F4")+" footSink="+footSink.ToString("F4"));
                if(id.Contains("NH007")||id.Contains("NH048"))yield return Detail("loaded-"+id,hit.point+Vector3.up*.18f,new Vector3(-1.7f,1.8f,1.3f),.85f);
                cat.Teleport(cat.session.definition.spawnPosition);yield return new WaitForSeconds(1.8f);
                CheckIt("soft-recovery-"+id,surface.CurrentIndentation<.002f,"indent="+surface.CurrentIndentation.ToString("F5"));
            }
            Stage("jump-on-and-off-sofa");
            var sofa=Item("LIV_NH007").Bounds;cat.Teleport(new Vector3(sofa.min.x-.18f,.035f,sofa.center.z+.43f));rig.yaw=90;yield return new WaitForSeconds(.2f);
            cat.VerificationRun=true;cat.VerificationMovement=Vector2.up;cat.RequestJump();yield return new WaitForSeconds(.60f);cat.VerificationMovement=Vector2.zero;yield return new WaitForSeconds(.8f);
            CheckIt("jump-onto-sofa",cat.transform.position.y>.38f&&cat.GetComponent<CharacterController>().isGrounded&&cat.transform.position.x>sofa.min.x,"position="+cat.transform.position+" blocker="+cat.MovementBlocker);
            rig.yaw=-90;cat.VerificationMovement=Vector2.up;yield return new WaitForSeconds(1.4f);cat.VerificationMovement=Vector2.zero;cat.VerificationRun=false;yield return new WaitForSeconds(.6f);
            CheckIt("leave-sofa",cat.transform.position.y<.035f&&cat.GetComponent<CharacterController>().isGrounded,"position="+cat.transform.position);
            Stage("doors-after-expansion");cat.Teleport(cat.session.definition.spawnPosition);yield return new WaitForSeconds(.2f);
            foreach(var door in FindObjectsByType<NewHouseDoor>(FindObjectsSortMode.None))
            {
                bool initial=door.IsOpen;bool requested=door.RequestToggle();float until=Time.time+3;
                while(door.IsMoving&&Time.time<until)yield return null;
                bool toggled=!door.IsMoving&&door.IsOpen!=initial;
                bool again=door.RequestToggle();until=Time.time+3;while(door.IsMoving&&Time.time<until)yield return null;
                CheckIt("door-"+door.displayName,requested&&again&&toggled&&!door.IsMoving&&door.IsOpen==initial&&door.panels.All(p=>p.body.transform.lossyScale==Vector3.one),"obstructions="+door.ObstructionCount);
            }
            Stage("continuous-eight-room-route");yield return Route();
            Stage("three-cat-scale-and-movement");
            foreach(var choice in CatCatalog.Current.cats)
            {
                CatCatalog.Select(choice.id,false);ContentPreviewMenu.Instance.LoadLevel(2);yield return new WaitForSecondsRealtime(.5f);
                while(!FindFirstObjectByType<NewHouseExploration>().IsReady)yield return null;
                Connect();yield return new WaitForSeconds(.2f);
                cat.Teleport(P(8.1f,3.4f));rig.yaw=180;yield return new WaitForSeconds(.2f);var start=cat.transform.position;
                cat.VerificationMovement=Vector2.up;yield return new WaitForSeconds(.5f);cat.VerificationMovement=Vector2.zero;
                float moved=Vector3.ProjectOnPlane(cat.transform.position-start,Vector3.up).magnitude;
                cat.RequestJump();float floor=cat.transform.position.y,peak=floor,end=Time.time+1.2f;while(Time.time<end){peak=Mathf.Max(peak,cat.transform.position.y);yield return null;}
                CheckIt("cat-"+choice.id,Mathf.Abs(cat.BodyScale-.675f)<.001f&&moved>cat.walkSpeed*.35f&&peak-floor>.35f&&Mathf.Abs(cat.transform.position.y-floor)<.05f&&cat.animator.avatar.isValid,"walk="+moved.ToString("F3")+" jump="+(peak-floor).ToString("F3"));
                yield return Capture("cat-"+choice.id);
            }
            Stage("rendered-performance");
            var samples=new List<float>();float stop=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<stop){yield return null;samples.Add(Time.unscaledDeltaTime);}
            samples.Sort();report.medianFps=1/samples[samples.Count/2];report.p5Fps=1/samples[(int)(samples.Count*.95f)];
            CheckIt("rendered-60fps",report.medianFps>=59,"median="+report.medianFps.ToString("F1")+" p5="+report.p5Fps.ToString("F1"));
            Finish();
        }
        void Connect()
        {
            cat=FindFirstObjectByType<CatController>();rig=FindFirstObjectByType<CatCamera>();game=FindFirstObjectByType<NewHouseExploration>();layout=FindFirstObjectByType<NewHouseLayout>();
            items=FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None);cat.Automated=true;rig.Automated=true;
            RenderCamera(rig.GetComponent<Camera>());QualitySettings.vSyncCount=0;Application.targetFrameRate=90;
        }
        IEnumerator Route()
        {
            var route=new[]{P(7.55f,4.25f),P(7.55f,5.62f),P(8.02f,5.62f),P(8.02f,6.35f),P(8.02f,5.62f),P(7.45f,5.62f),P(7.45f,5.33f),P(5.75f,5.33f),P(4.55f,5.33f),P(3.6f,5.33f),P(3.6f,4.5f),P(3.6f,3.55f),P(3.6f,4.5f),P(3.6f,5.575f),P(2.75f,5.575f),P(3.6f,5.575f),P(3.6f,7.25f),P(3.6f,7.62f),P(4.7f,7.62f),P(3.6f,7.62f),P(3.6f,5.33f),P(4.55f,5.33f),P(5.75f,5.33f),P(7.45f,5.33f),P(7.55f,4.25f),P(7.55f,2.35f),P(7.05f,2.35f),P(7.05f,1.1f)};
            cat.Teleport(cat.session.definition.spawnPosition);yield return new WaitForSeconds(.2f);cat.VerificationRun=true;var visited=new HashSet<string>();bool pass=true;
            var trace=new List<string>();
            foreach(var point in route)
            {
                float remaining=Vector3.ProjectOnPlane(point-cat.transform.position,Vector3.up).magnitude;float until=Time.time+remaining/cat.runSpeed*1.8f+2;
                while((remaining=Vector3.ProjectOnPlane(point-cat.transform.position,Vector3.up).magnitude)>.035f&&Time.time<until)
                {
                    var delta=point-cat.transform.position;rig.yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                    cat.VerificationMovement=Vector2.up*Mathf.Clamp01(remaining/(cat.runSpeed*Mathf.Max(.001f,Time.deltaTime)));yield return null;
                }
                cat.VerificationMovement=Vector2.zero;string room=cat.session.definition.LocationAt(cat.transform.position);visited.Add(room);
                trace.Add(point+" reached="+cat.transform.position+" room="+room+" blocker="+cat.MovementBlocker);
                if(remaining>.06f){pass=false;yield return Capture("route-blocked");break;}
            }
            cat.VerificationRun=false;File.WriteAllLines(Path.Combine(output,"route.txt"),trace);CheckIt("eight-room-route",pass&&visited.Count==8,string.Join(",",visited)+" "+trace.LastOrDefault());
        }
        IEnumerator Capture(string name)
        {
            yield return null;yield return new WaitForEndOfFrame();var image=VerificationRenderTarget.Capture();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);
        }
        IEnumerator Overview(string name)
        {
            game.SetPreview(true);RenderCamera(game.preview.viewCamera);yield return Capture(name);game.SetPreview(false);
        }
        IEnumerator Detail(string name,Vector3 center,Vector3 offset,float size)
        {
            game.SetPreview(true);RenderCamera(game.preview.viewCamera);var camera=game.preview.viewCamera;
            var walls=game.preview.residence.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.name.StartsWith("NH_Wall_")).ToArray();foreach(var wall in walls)wall.enabled=false;
            camera.orthographic=true;camera.orthographicSize=size;camera.transform.SetPositionAndRotation(center+offset,Quaternion.LookRotation(-offset));
            yield return Capture(name);foreach(var wall in walls)wall.enabled=true;game.SetPreview(false);
        }
        static void RenderCamera(Camera camera)
        {
            VerificationRenderTarget.Initialize(camera);
        }
        IEnumerator ProfileWindow()
        {
            var item=Item("MBR_NH073");var b=item.Bounds;
            var soft=item.GetComponentInChildren<SoftPillow>();soft.GetComponent<MeshCollider>().Raycast(new Ray(new Vector3(b.center.x,3,b.center.z),Vector3.down),out var hit,4);
            cat.Teleport(hit.point+Vector3.up*.025f);yield return new WaitForSeconds(1);
            yield return Profile("window-normal");
            var softs=FindObjectsByType<SoftPillow>(FindObjectsSortMode.None);foreach(var s in softs)s.enabled=false;
            yield return Profile("soft-disabled");
            cat.Paused=true;yield return Profile("controller-paused");cat.Paused=false;
            foreach(var s in softs)s.enabled=true;
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            float oldDistance=pipeline.shadowDistance;pipeline.shadowDistance=8;yield return Profile("shadow-distance-8");pipeline.shadowDistance=oldDistance;
            int oldCascades=pipeline.shadowCascadeCount;pipeline.shadowCascadeCount=1;pipeline.shadowDistance=10;yield return Profile("one-cascade-distance-10");pipeline.shadowCascadeCount=oldCascades;pipeline.shadowDistance=oldDistance;
            int oldResolution=pipeline.mainLightShadowmapResolution;pipeline.mainLightShadowmapResolution=1024;yield return Profile("shadow-map-1024");pipeline.mainLightShadowmapResolution=oldResolution;
            var lights=FindObjectsByType<Light>(FindObjectsSortMode.None);var sun=lights.Single(l=>l.type==LightType.Directional);var oldShadows=sun.shadows;
            sun.shadows=LightShadows.None;yield return Profile("sun-shadow-disabled");sun.shadows=oldShadows;
            foreach(var light in lights.Where(l=>l.type!=LightType.Directional))light.enabled=false;
            yield return Profile("fill-lights-disabled");foreach(var light in lights)light.enabled=true;
            foreach(var light in lights)light.enabled=false;
            yield return Profile("lights-disabled");foreach(var light in lights)light.enabled=true;
            var walls=layout.GetComponentsInChildren<MeshRenderer>().Where(r=>!r.GetComponentInParent<NewHouseItem>()&&r.enabled).ToArray();foreach(var wall in walls)wall.enabled=false;
            yield return Profile("architecture-hidden");foreach(var wall in walls)wall.enabled=true;
            rig.yaw=0;rig.SnapToTarget();yield return Profile("look-outside");
        }
        IEnumerator Profile(string label)
        {
            Stage(label);yield return new WaitForSeconds(.25f);var frames=new List<float>();float end=Time.realtimeSinceStartup+2;
            while(Time.realtimeSinceStartup<end){yield return null;frames.Add(Time.unscaledDeltaTime);}frames.Sort();
            CheckIt(label,true,"fps="+(1/frames[frames.Count/2]).ToString("F2"));
        }
        void Finish()
        {
            if(output==null)return;report.passed=checks.All(c=>c.passed)&&errors.Count==0;report.checks=checks.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(Path.Combine(output,"upgrade-check.json"),JsonUtility.ToJson(report,true));if(previousCat!=null)CatCatalog.Select(previousCat,false);output=null;
            Debug.Log("NEW_HOUSE_UPGRADE_RESULT "+report.passed);Application.Quit(report.passed?0:1);
        }
    }
}
