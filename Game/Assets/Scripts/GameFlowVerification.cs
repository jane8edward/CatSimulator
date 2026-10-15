using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatSimulator
{
    // Runs only with --verify-game-flow. Exercises UI routes, separate rigs and actual traversal.
    public sealed class GameFlowVerification : MonoBehaviour
    {
        [Serializable] public sealed class CatResult
        {
            public string id; public bool nativeRig, firstLevelTasks, exploration, jumps, stableSteering, replayKeepsCat;
            public bool sofaJump, sofaSeams, sofaBack, sofaExit, furnitureTops;
            public float sofaLandingHeight, sofaExpectedHeight;
            public string[] badFurnitureTops;
            public bool scaleCorrect, boundSkin;
            public float firstLevelScale,homeScale,controllerHeight,visualScale;
            public ControlsVerification.Result controls;
            public HomeUpgradeVerification.Result homePhysics;
            public int bones, physicalMeshes, missingCollisions; public float averageFps;
            public string[] visitedRooms, unreachableRooms;
        }
        [Serializable] public sealed class Report
        {
            public bool passed, twoStartButtons, levelChoice, galleryChoices, selectionSaved, separateControllers;
            public string stage, gpu; public CatResult[] cats; public string[] errors;
        }
        readonly List<string> errors = new List<string>();
        readonly List<CatResult> results = new List<CatResult>();
        readonly Report report = new Report();
        string output, previousSelection; bool hadSelection;
        float began;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if (!Environment.GetCommandLineArgs().Contains("--verify-game-flow")) return;
            var root = new GameObject("GameFlowVerification"); DontDestroyOnLoad(root); root.AddComponent<GameFlowVerification>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message,string stack,LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message); }
        void Update() { if (output != null && Time.realtimeSinceStartup - began > 600) {errors.Add("Traversal test timed out");Finish();} }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); output = args[Array.IndexOf(args,"--verification-output")+1];
            Directory.CreateDirectory(output); began = Time.realtimeSinceStartup;
            hadSelection = PlayerPrefs.HasKey("selected-cat"); previousSelection = PlayerPrefs.GetString("selected-cat","original");
            report.gpu = SystemInfo.graphicsDeviceName;
            yield return new WaitForSecondsRealtime(.5f);
            var menu = ContentPreviewMenu.Instance;
            if (menu == null) { errors.Add("Start menu missing"); Finish(); yield break; }
            report.twoStartButtons = SceneManager.GetActiveScene().name == ContentPreviewMenu.StartScene &&
                menu.GetComponentsInChildren<Button>().Count(b=>b.gameObject.activeInHierarchy) == 2;
            Stage("start-menu"); yield return Capture("start-menu");
            Click("StartGame"); yield return null;
            report.levelChoice = HasButton("Level1") && HasButton("Level2"); yield return Capture("level-selection");
            Click("Back"); yield return null; Click("CatModels"); yield return null;
            var catalog = CatCatalog.Current;
            report.galleryChoices = catalog.cats.Length==3 && catalog.cats.All(c=>HasButton("Preview-"+c.id));
            report.separateControllers = catalog.cats[0].visualPrefab.GetComponent<Animator>().runtimeAnimatorController != catalog.cats[1].visualPrefab.GetComponent<Animator>().runtimeAnimatorController;
            foreach (var choice in catalog.cats)
            {
                Stage("gallery-"+choice.id); Click("Preview-"+choice.id); yield return null;
                Click("Walking"); yield return new WaitForSecondsRealtime(.35f); yield return Capture("gallery-"+choice.id);
                Click("UseCat"); yield return null;
                report.selectionSaved |= PlayerPrefs.GetString("selected-cat") == choice.id;
                Click("Back"); yield return null; Click("StartGame"); yield return null; Click("Level1");
                yield return new WaitForSecondsRealtime(.6f);
                var cat = FindFirstObjectByType<CatController>(); var camera = FindFirstObjectByType<CatCamera>();
                cat.Automated = true; camera.Automated = true;
                var result = new CatResult {id=choice.id}; results.Add(result);
                result.firstLevelScale=cat.BodyScale;
                result.bones = cat.animator.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("bone_"));
                var skins=cat.visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                result.boundSkin=skins.Length>0 && skins.All(s=>s.bones.Length>0 && s.bones.All(b=>b!=null) && s.sharedMesh.bindposes.Length==s.bones.Length);
                bool balcony=choice.id=="balcony";
                result.nativeRig = cat.animationDriver != null && cat.animationDriver.CatId == choice.id && cat.animator.avatar.isValid && result.boundSkin &&
                    (balcony ? skins.Any(s=>s.sharedMesh.vertexCount>=25515 && s.sharedMesh.vertexCount<55000 && s.bones.Length==50) && cat.animator.runtimeAnimatorController.name=="BalconyCat" :
                     choice.animationKind == CatAnimationKind.Original ? cat.animator.runtimeAnimatorController.name == "AnimatedCat" : result.bones == 38 && cat.animator.runtimeAnimatorController.name == "Abyssinian_Walk");
                Stage("level1-tasks-"+choice.id);
                cat.BeginInteraction(FindFirstObjectByType<CupInteractable>()); while (cat.IsBusy) yield return null;
                cat.BeginInteraction(FindFirstObjectByType<CurtainInteractable>()); while (cat.IsBusy) yield return null;
                result.firstLevelTasks = cat.session.IsComplete && cat.session.HasObjective("spill-cup") && cat.session.HasObjective("damage-curtain");
                yield return Capture("level1-"+choice.id);
                menu.ReturnToStart(); yield return new WaitForSecondsRealtime(.4f); Click("StartGame"); yield return null; Click("Level2");
                yield return new WaitForSecondsRealtime(1);
                cat = FindFirstObjectByType<CatController>(); camera = FindFirstObjectByType<CatCamera>(); cat.Automated = true; camera.Automated = true;
                result.exploration = cat.session.definition.IsExploration && !cat.session.IsComplete && cat.animationDriver.CatId == choice.id;
                result.homeScale=cat.BodyScale;result.controllerHeight=cat.GetComponent<CharacterController>().height;result.visualScale=cat.visual.localScale.x;
                result.scaleCorrect=Mathf.Abs(result.firstLevelScale-1)<.001f && Mathf.Abs(result.homeScale-.75f)<.001f &&
                    Mathf.Abs(result.controllerHeight-.27f)<.001f && Mathf.Abs(result.visualScale-choice.visualScale*.75f)<.001f;
                var house = GameObject.Find("Home90Residence");
                var physical = house.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
                result.physicalMeshes = physical.Length; result.missingCollisions = physical.Count(r=>r.GetComponent<Collider>()==null && r.GetComponentInParent<Rigidbody>()==null);
                Stage("home90-"+choice.id); yield return Capture("home90-"+choice.id);
                if(house.GetComponent<HomeLayout>())
                {
                    Stage("home-physics-"+choice.id);result.homePhysics=new HomeUpgradeVerification.Result();
                    yield return HomeUpgradeVerification.Run(cat,camera,house,output,result.homePhysics);
                }
                Stage("controls-"+choice.id);result.controls=new ControlsVerification.Result();
                yield return ControlsVerification.Run(cat,camera,house,output,result.controls);
                Stage("furniture-"+choice.id);
                var topNames=new[] {"_SeatCushion_","_Mattress_","_DiningTable_01_P01_","_CoffeeTable_01_P01_","_Desk_01_P01_","_TVConsole_01_P01_","_GuestStorage_01_P01_"};
                result.badFurnitureTops=physical.Where(r=>topNames.Any(n=>r.name.Contains(n))).Where(r=> {
                    var b=r.bounds;
                    return !r.GetComponent<Collider>().Raycast(new Ray(new Vector3(b.center.x,b.max.y+.1f,b.center.z),Vector3.down),out var h,b.size.y+.2f) || h.point.y<b.max.y-.06f;
                }).Select(r=>r.name).ToArray();
                result.furnitureTops=result.badFurnitureTops.Length==0;
                yield return CheckSofa(cat,camera,house,result);
                cat.Teleport(cat.session.definition.spawnPosition);yield return new WaitForSecondsRealtime(.2f);
                var visited = new List<string>(); var unreachable = new List<string>();
                result.stableSteering = true;
                var route = new[] {
                    new RoutePoint(-7.6f,-3.35f,"客餐厅"),new RoutePoint(-7.85f,-4.1f),new RoutePoint(-5.8f,-4.1f),
                    new RoutePoint(-5.8f,-5.08f),new RoutePoint(-3.6f,-5.08f,"走廊"),
                    new RoutePoint(-3.6f,-3.1f),new RoutePoint(-3.04f,-3.1f),new RoutePoint(-3.04f,-1.1f,"主卧"),
                    new RoutePoint(-3.04f,-3.1f),new RoutePoint(-3.6f,-3.1f),new RoutePoint(-3.6f,-5.575f),
                    new RoutePoint(-2.02f,-5.575f,"次卧与书房"),new RoutePoint(-2.02f,-4.95f),new RoutePoint(-2.02f,-5.575f),
                    new RoutePoint(-3.6f,-5.575f),new RoutePoint(-3.6f,-7.5f,"玄关"),new RoutePoint(-3.6f,-7.825f),
                    new RoutePoint(-4.75f,-7.825f,"卫生间"),new RoutePoint(-3.6f,-7.825f),new RoutePoint(-3.6f,-5.33f),
                    new RoutePoint(-5.8f,-5.33f),new RoutePoint(-5.8f,-4.1f),new RoutePoint(-8.3f,-4.1f),
                    new RoutePoint(-8.3f,-6.8f,"厨房"),new RoutePoint(-8.3f,-4.1f),new RoutePoint(-8.05f,-2.2f),
                    new RoutePoint(-7.05f,-2.2f),new RoutePoint(-7.05f,-1.11f,"阳台")};
                foreach (var waypoint in route)
                {
                    string location=waypoint.room ?? waypoint.position.ToString(); Stage("walk-"+choice.id+"-"+location);
                    bool reached = true;
                    var layout=house.GetComponent<HomeLayout>();
                    var target=layout?layout.FromOriginal(waypoint.position):waypoint.position;
                    {
                        float until = Time.realtimeSinceStartup + Vector3.ProjectOnPlane(target-cat.transform.position,Vector3.up).magnitude/cat.walkSpeed*1.7f+3;
                        Vector3 previous = cat.transform.position;
                        while (Vector3.ProjectOnPlane(target-cat.transform.position,Vector3.up).magnitude > .008f)
                        {
                            var offset = Vector3.ProjectOnPlane(target-cat.transform.position,Vector3.up); var direction = offset.normalized;
                            camera.yaw = Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
                            cat.VerificationMovement = Vector2.up*Mathf.Clamp01(offset.magnitude/(cat.walkSpeed*Mathf.Max(.001f,Time.deltaTime)));
                            yield return null;
                            if (Vector3.Dot(cat.transform.position-previous,direction) < -.005f) result.stableSteering = false;
                            previous = cat.transform.position;
                            if (Time.realtimeSinceStartup > until) { reached = false; break; }
                        }
                        if (!reached) {unreachable.Add(location+": blocked by "+cat.MovementBlocker+" at "+cat.transform.position);}
                    }
                    cat.VerificationMovement = Vector2.zero;
                    if (!reached) {yield return Capture("blocked-"+choice.id);break;}
                    if (waypoint.room != null && cat.session.definition.LocationAt(cat.transform.position)==waypoint.room) visited.Add(waypoint.room);
                    else if (waypoint.room != null) unreachable.Add(waypoint.room+": target outside area");
                }
                result.visitedRooms = visited.ToArray(); result.unreachableRooms = unreachable.ToArray();
                cat.VerificationMovement = Vector2.zero;
                cat.Teleport(cat.session.definition.spawnPosition); yield return new WaitForSecondsRealtime(.2f);
                float ground = cat.transform.position.y, peak = ground; cat.RequestJump();
                for (float t=0;t<1.1f;t+=Time.deltaTime) {yield return null;peak=Mathf.Max(peak,cat.transform.position.y);}
                result.jumps = peak > ground+.25f && Mathf.Abs(cat.transform.position.y-ground)<.06f;
                Application.targetFrameRate = -1; yield return new WaitForSecondsRealtime(.3f);
                int frames = 0; float started = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup-started<2) {frames++;yield return null;}
                result.averageFps = frames/(Time.realtimeSinceStartup-started);
                cat.session.Replay(); yield return new WaitForSecondsRealtime(.4f);
                result.replayKeepsCat = FindFirstObjectByType<CatController>().animationDriver.CatId == choice.id;
                menu.ReturnToStart(); yield return new WaitForSecondsRealtime(.4f); Click("CatModels"); yield return null;
            }
            report.passed = report.twoStartButtons && report.levelChoice && report.galleryChoices && report.selectionSaved && report.separateControllers &&
                results.Count == catalog.cats.Length && results.All(r=>r.nativeRig && r.firstLevelTasks && r.exploration && r.missingCollisions==0 && r.visitedRooms.Length==8 && r.unreachableRooms.Length==0 && r.jumps && r.stableSteering && r.replayKeepsCat && r.averageFps>=60 && r.sofaJump && r.sofaSeams && r.sofaBack && r.sofaExit && r.furnitureTops && r.scaleCorrect && r.controls.passed && (r.homePhysics==null || r.homePhysics.passed)) && errors.Count==0;
            Finish();
        }
        bool HasButton(string name) => ContentPreviewMenu.Instance.GetComponentsInChildren<Button>().Any(b=>b.name==name && b.gameObject.activeInHierarchy);
        IEnumerator CheckSofa(CatController cat,CatCamera camera,GameObject house,CatResult result)
        {
            var seats=house.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.Contains("_Sofa_01_") && r.name.Contains("_SeatCushion_")).ToArray();
            var b=seats[0].bounds;foreach(var seat in seats.Skip(1))b.Encapsulate(seat.bounds);
            float z=b.center.z,targetX=b.min.x+.18f*cat.BodyScale;
            cat.transform.rotation=Quaternion.Euler(0,90,0);camera.yaw=90;
            cat.Teleport(new Vector3(b.min.x-.38f*cat.BodyScale,.055f,z));
            yield return new WaitForSecondsRealtime(.3f);
            float ground=cat.transform.position.y,peak=ground;
            cat.VerificationRun=true;cat.RequestJump();
            float end=Time.realtimeSinceStartup+1.6f;
            while(Time.realtimeSinceStartup<end)
            {
                cat.VerificationMovement=cat.transform.position.x<targetX?Vector2.up:Vector2.zero;
                yield return null;peak=Mathf.Max(peak,cat.transform.position.y);
            }
            cat.VerificationMovement=Vector2.zero;cat.VerificationRun=false;
            yield return new WaitForSecondsRealtime(.3f);
            result.sofaExpectedHeight=b.max.y;result.sofaLandingHeight=cat.transform.position.y;
            result.sofaJump=peak>ground+.4f && Mathf.Abs(cat.transform.position.y-b.max.y)<.045f && cat.GetComponent<CharacterController>().isGrounded;
            yield return Capture("sofa-land-"+result.id);
            bool across=result.sofaJump;
            foreach(float targetZ in new[] {b.center.z+.52f,b.center.z-.52f})
            {
                camera.yaw=targetZ>cat.transform.position.z?0:180;
                end=Time.realtimeSinceStartup+3.5f;
                while(Mathf.Abs(targetZ-cat.transform.position.z)>.035f && Time.realtimeSinceStartup<end)
                {
                    cat.VerificationMovement=Vector2.up*Mathf.Clamp01(Mathf.Abs(targetZ-cat.transform.position.z)/(cat.walkSpeed*Mathf.Max(.001f,Time.deltaTime)));
                    yield return null;
                    across &= Mathf.Abs(cat.transform.position.y-b.max.y)<.055f;
                }
                across &= Mathf.Abs(targetZ-cat.transform.position.z)<.04f;
            }
            result.sofaSeams=across;
            camera.yaw=90;cat.VerificationMovement=Vector2.up;
            yield return new WaitForSecondsRealtime(.8f);cat.VerificationMovement=Vector2.zero;
            var back=house.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.Contains("_Sofa_01_") && r.name.Contains("_BackCushion_"));
            result.sofaBack=result.sofaJump && cat.transform.position.x+cat.BodyFrontReach<back.bounds.min.x+.035f && Mathf.Abs(cat.transform.position.y-b.max.y)<.055f;
            camera.yaw=-90;cat.VerificationMovement=Vector2.up;cat.VerificationRun=true;
            end=Time.realtimeSinceStartup+2;
            while(cat.transform.position.x>b.min.x-.5f && Time.realtimeSinceStartup<end)yield return null;
            cat.VerificationMovement=Vector2.zero;cat.VerificationRun=false;
            yield return new WaitForSecondsRealtime(.7f);
            result.sofaExit=cat.transform.position.x<b.min.x-.45f && Mathf.Abs(cat.transform.position.y-ground)<.04f && cat.GetComponent<CharacterController>().isGrounded;
            yield return Capture("sofa-exit-"+result.id);
        }
        void Click(string name) => ContentPreviewMenu.Instance.GetComponentsInChildren<Button>().Single(b=>b.name==name && b.gameObject.activeInHierarchy).onClick.Invoke();
        IEnumerator Capture(string name)
        {
            var camera = FindFirstObjectByType<Camera>();
            if (camera.GetComponent<VerificationRenderTarget>()==null) VerificationRenderTarget.Initialize(camera);
            yield return new WaitForEndOfFrame();
            var image = VerificationRenderTarget.Capture();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG()); Destroy(image);
        }
        void Stage(string value) {report.stage=value;WriteReport();}
        void WriteReport() {report.cats=results.ToArray();report.errors=errors.ToArray();File.WriteAllText(Path.Combine(output,"game-flow.json"),JsonUtility.ToJson(report,true));}
        void Finish()
        {
            if (hadSelection) PlayerPrefs.SetString("selected-cat",previousSelection); else PlayerPrefs.DeleteKey("selected-cat"); PlayerPrefs.Save();
            report.stage="complete";WriteReport(); Application.Quit(report.passed?0:2);
        }

        sealed class RoutePoint
        {
            public readonly Vector3 position;
            public readonly string room;
            public RoutePoint(float x,float z,string location=null) { position=new Vector3(x,.04f,z);room=location; }
        }
    }
}
