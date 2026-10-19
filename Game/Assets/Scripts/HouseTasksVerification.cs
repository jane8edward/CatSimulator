using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CatSimulator
{
    public sealed class HouseTasksVerification : MonoBehaviour
    {
        [Serializable] class Check {public string name,detail;public bool passed;}
        [Serializable] class Report {public bool passed;public Check[] checks;public string[] errors;}
        readonly List<Check> checks=new List<Check>();readonly List<string> errors=new List<string>();
        string output,previousCat;float began;CatController cat;CatCamera cameraRig;RobotVacuum robot;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin(){if(!Environment.GetCommandLineArgs().Contains("--check-house-tasks"))return;var go=new GameObject("HouseTasksVerification");DontDestroyOnLoad(go);go.AddComponent<HouseTasksVerification>();}
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void Update(){if(output!=null && Time.realtimeSinceStartup-began>660){errors.Add("Timed out");Finish();}}
        void Result(string name,bool passed,string detail=""){checks.Add(new Check{name=name,passed=passed,detail=detail});Debug.Log("HOUSE_CHECK "+name+" "+passed+" "+detail);}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"--verification-output")+1];Directory.CreateDirectory(output);began=Time.realtimeSinceStartup;
            previousCat=CatCatalog.Selected.id;yield return new WaitForSecondsRealtime(.3f);
            foreach(var choice in CatCatalog.Current.cats)
            {
                CatCatalog.Select(choice.id,false);ContentPreviewMenu.Instance.LoadLevel(2);yield return null;
                while(!FindFirstObjectByType<NewHouseExploration>()?.IsReady??true)yield return null;
                cat=FindFirstObjectByType<CatController>();cameraRig=FindFirstObjectByType<CatCamera>();robot=FindFirstObjectByType<RobotVacuum>();
                cat.Automated=true;cameraRig.Automated=true;VerificationRenderTarget.Initialize(cameraRig.GetComponent<Camera>());
                Application.targetFrameRate=90;QualitySettings.vSyncCount=0;
                Result("scene-furniture-"+choice.id,robot && robot.definition==cat.session.definition && robot.ParkedPosition.x<-11 && robot.ParkedPosition.z<-2.5f,"parked="+robot.ParkedPosition);
                robot.enabled=false;yield return new WaitForSeconds(.4f);
                Result("F-from-floor-does-not-board-"+choice.id,!robot.TryMount()&&!cat.IsRiding);
                Result("five-objectives-"+choice.id,cat.session.definition.requiredObjectives.Length==5&&!cat.session.IsComplete);
                yield return Capture("living-room-"+choice.id,new Vector3(-10.7f,.25f,-2.6f),robot.Seat);
                // A real short jump lands on the actual cylinder collider, with F untouched.
                var parked=robot.transform.position;robot.GetComponent<Rigidbody>().position=new Vector3(-9.25f,.0105f,-3.25f);robot.transform.position=new Vector3(-9.25f,.0105f,-3.25f);Physics.SyncTransforms();
                cat.Teleport(robot.Seat+Vector3.right*.43f);cat.Teleport(new Vector3(cat.transform.position.x,.014f,cat.transform.position.z));
                cameraRig.yaw=-90;yield return new WaitForSeconds(.25f);cat.SetJumpHeld(true);yield return new WaitForSeconds(.06f);cat.SetJumpHeld(false);
                cat.VerificationMovement=Vector2.up;cat.VerificationRun=true;
                float startX=cat.transform.position.x,until=Time.time+1.4f;
                while(Time.time<until)
                {
                    if(startX-cat.transform.position.x>.42f)cat.VerificationMovement=Vector2.zero;
                    yield return null;
                }
                cat.VerificationMovement=Vector2.zero;cat.VerificationRun=false;
                Result("jump-onto-without-F-"+choice.id,cat.SupportVehicle==robot&&!cat.IsRiding&&cat.GetComponent<CharacterController>().enabled,"cat="+cat.transform.position+" robot="+robot.transform.position);
                // Clear the old navigation plan after the test relocation, then drive real physics.
                robot.enabled=true;InvokePlan();
                var relative=robot.transform.InverseTransformPoint(cat.transform.position);var before=robot.transform.position;
                yield return new WaitForSeconds(5);
                Result("standing-carried-"+choice.id,cat.SupportVehicle==robot&&!cat.IsRiding&&Vector3.Distance(relative,robot.transform.InverseTransformPoint(cat.transform.position))<.06f,"travel="+Vector3.Distance(before,robot.transform.position)+" drift="+Vector3.Distance(relative,robot.transform.InverseTransformPoint(cat.transform.position)));
                bool seated=robot.TryMount();yield return new WaitForSeconds(.3f);var sittingPosition=robot.transform.InverseTransformPoint(cat.transform.position);
                yield return new WaitForSeconds(2);
                Result("F-only-sits-"+choice.id,seated&&cat.IsRiding&&Vector3.Distance(sittingPosition,robot.transform.InverseTransformPoint(cat.transform.position))<.015f);
                yield return Capture("seated-"+choice.id,robot.Seat+new Vector3(.55f,.3f,.7f),robot.Seat+Vector3.up*.15f);
                bool stood=robot.TryDismount();yield return new WaitForSeconds(.1f);
                Result("F-stands-on-platform-"+choice.id,stood&&!cat.IsRiding&&cat.SupportVehicle==robot);
                cat.RequestJump();cat.VerificationMovement=Vector2.up;yield return new WaitForSeconds(.7f);cat.VerificationMovement=Vector2.zero;yield return new WaitForSeconds(.45f);
                Result("jump-off-"+choice.id,!cat.SupportVehicle&&!cat.IsRiding&&cat.GetComponent<CharacterController>().isGrounded);
                cat.Teleport(cat.session.definition.spawnPosition);robot.enabled=false;
                foreach(var task in FindObjectsByType<HouseTaskInteractable>(FindObjectsSortMode.None).OrderBy(t=>(int)t.kind))
                {
                    cat.Teleport(task.actionPosition.position);yield return new WaitForSeconds(.15f);
                    Result("E-reachable-"+task.interactionId+"-"+choice.id,cat.Nearest==task,"nearest="+cat.Nearest?.interactionId);
                    Result("highlight-"+task.interactionId+"-"+choice.id,task.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="InteractionOutline"&&t.gameObject.activeInHierarchy));
                    cat.BeginInteraction(task);
                    if(task.kind==HouseTaskInteractable.Kind.Phone||task.kind==HouseTaskInteractable.Kind.Slippers)
                    {
                        while(!task.EffectApplied && cat.IsBusy)yield return null;
                        yield return Capture(task.interactionId+"-action-"+choice.id,task.focus.position+(task.kind==HouseTaskInteractable.Kind.Phone?new Vector3(-.24f,.28f,.35f):new Vector3(-.3f,.7f,-.38f)),task.focus.position);
                    }
                    while(cat.IsBusy)yield return null;
                    Result("objective-"+task.interactionId+"-"+choice.id,cat.session.HasObjective(task.interactionId));
                    Result("effect-"+task.interactionId+"-"+choice.id,task.kind==HouseTaskInteractable.Kind.Food?task.effect.transform.Cast<Transform>().Any(t=>!t.gameObject.activeSelf):task.EffectApplied);
                    if(task.kind==HouseTaskInteractable.Kind.Phone)Result("cord-sparks-and-unplugged-indicator-"+choice.id,task.SparkBursts==1&&task.damaged.activeSelf&&!task.intact.activeSelf&&!task.effect.activeSelf);
                    Vector3 offset=task.kind==HouseTaskInteractable.Kind.Laptop?new Vector3(.05f,.25f,.6f):task.kind==HouseTaskInteractable.Kind.Phone?new Vector3(-.3f,.4f,.4f):task.kind==HouseTaskInteractable.Kind.Slippers?new Vector3(-.3f,.75f,-.38f):new Vector3(-.3f,.75f,.3f);
                    yield return Capture(task.interactionId+"-"+choice.id,task.focus.position+offset,task.focus.position);
                }
                var climb=FindObjectsByType<ClimbInteractable>(FindObjectsSortMode.None).Single(c=>c.interactionId=="climb-house-tree");
                cat.Teleport(climb.anchor.position);yield return new WaitForSeconds(.1f);Result("E-reachable-climb-"+choice.id,cat.Nearest==climb);
                cat.BeginInteraction(climb);var climbTrace=new List<string>();
                while(cat.IsBusy){climbTrace.Add(cat.transform.position.ToString("F4")+" cc="+cat.GetComponent<CharacterController>().enabled);yield return null;}
                yield return new WaitForSeconds(.4f);File.WriteAllLines(Path.Combine(output,"climb-"+choice.id+".txt"),climbTrace);
                string support="none";if(Physics.Raycast(cat.transform.position+Vector3.up*.04f,Vector3.down,out var below,.6f,~(1<<8)))support=below.collider.name+" y="+below.point.y;
                Result("reused-climb-top-"+choice.id,cat.session.HasObjective(climb.interactionId)&&Vector3.Distance(cat.transform.position,climb.route.Last().position)<.08f,"cat="+cat.transform.position+" top="+climb.route.Last().position+" support="+support);
                Result("all-five-complete-"+choice.id,cat.session.IsComplete);
                yield return Capture("climb-top-"+choice.id,cat.transform.position+new Vector3(.9f,.35f,.55f),cat.transform.position);
                cat.Teleport(cat.session.definition.spawnPosition);
                if(choice==CatCatalog.Current.cats[0])yield return UnderBed();
                // Replay restores furniture, cable, laptop and task completion together.
                cat.session.Replay();yield return null;while(!FindFirstObjectByType<NewHouseExploration>()?.IsReady??true)yield return null;
                cat=FindFirstObjectByType<CatController>();cat.Automated=true;
                Result("replay-restores-"+choice.id,!cat.session.IsComplete&&FindObjectsByType<HouseTaskInteractable>(FindObjectsSortMode.None).Where(t=>t.kind!=HouseTaskInteractable.Kind.Food).All(t=>!t.EffectApplied)&&cat.animationDriver.CatId==choice.id);
            }
            Finish();
        }
        void InvokePlan(){var method=typeof(RobotVacuum).GetMethod("Plan",BindingFlags.Instance|BindingFlags.NonPublic);robot.StartCoroutine((IEnumerator)method.Invoke(robot,new object[]{true}));}
        IEnumerator UnderBed()
        {
            var bed=FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None).Single(i=>i.placement.Contains("MBR_NH050"));var b=bed.Bounds;
            var clear=typeof(RobotVacuum).GetMethod("Clear",BindingFlags.Instance|BindingFlags.NonPublic);
            var inside=new Vector3(b.center.x,.0105f,b.center.z);
            bool fits=(bool)clear.Invoke(robot,new object[]{inside,false,false,null});
            Result("thin-body-fits-under-bed",fits,"height="+RobotVacuum.Height+" center="+inside);
            var body=robot.GetComponent<Rigidbody>();body.position=new Vector3(b.min.x-.36f,.0105f,b.center.z);robot.transform.position=body.position;Physics.SyncTransforms();
            var path=(List<Vector3>)typeof(RobotVacuum).GetField("path",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(robot);path.Clear();path.Add(inside);
            robot.enabled=true;yield return new WaitForSeconds(5);
            Result("actually-drives-under-bed",Vector3.Distance(robot.transform.position,inside)<.08f,"position="+robot.transform.position+" status="+robot.Status+" blocker="+robot.LastMotionBlocker);
            yield return Capture("under-bed",inside+new Vector3(-1.35f,.11f,0),inside+Vector3.up*.025f);
            robot.enabled=false;
        }
        IEnumerator Capture(string name,Vector3 position,Vector3 target)
        {
            var camera=cameraRig.GetComponent<Camera>();cameraRig.enabled=false;camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            yield return new WaitForEndOfFrame();var frame=VerificationRenderTarget.Capture();File.WriteAllBytes(Path.Combine(output,name+".png"),frame.EncodeToPNG());Destroy(frame);cameraRig.enabled=true;
        }
        void Finish()
        {
            if(output==null)return;var report=new Report{passed=checks.All(c=>c.passed)&&errors.Count==0,checks=checks.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(Path.Combine(output,"house-tasks.json"),JsonUtility.ToJson(report,true));output=null;CatCatalog.Select(previousCat,false);Application.Quit(report.passed?0:1);
        }
    }
}
