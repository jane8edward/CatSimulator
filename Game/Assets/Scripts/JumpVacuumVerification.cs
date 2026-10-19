using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CatSimulator
{
    public sealed class JumpVacuumVerification : MonoBehaviour
    {
        [Serializable] class Result {public string name,detail;public bool passed;}
        [Serializable] class Report {public bool passed;public Result[] checks;public string[] errors;}
        readonly List<Result> checks=new List<Result>();readonly List<string> errors=new List<string>();
        string output,previousCat;CatController cat;RobotVacuum robot;CatCamera cameraRig;float began;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if(!Environment.GetCommandLineArgs().Contains("--check-jump-vacuum"))return;
            var go=new GameObject("JumpVacuumVerification");DontDestroyOnLoad(go);go.AddComponent<JumpVacuumVerification>();
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void Check(string name,bool passed,string detail=""){checks.Add(new Result{name=name,passed=passed,detail=detail});Debug.Log("JUMP_VACUUM "+name+"="+passed+" "+detail);}
        void Update(){if(output!=null&&Time.realtimeSinceStartup-began>480){errors.Add("Timeout");Finish();}}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--verification-output");
            output=Path.GetFullPath(index>=0?args[index+1]:"JumpVacuumVerification");Directory.CreateDirectory(output);
            began=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;previousCat=CatCatalog.SelectedId;
            yield return new WaitForSecondsRealtime(.4f);
            foreach(var choice in CatCatalog.Current.cats)
            {
                CatCatalog.Select(choice.id,false);ContentPreviewMenu.Instance.LoadLevel(2);
                yield return new WaitForSecondsRealtime(.5f);
                while(!FindFirstObjectByType<NewHouseExploration>().IsReady)yield return null;
                cat=FindFirstObjectByType<CatController>();cat.Automated=true;
                cameraRig=FindFirstObjectByType<CatCamera>();cameraRig.Automated=true;
                robot=FindFirstObjectByType<RobotVacuum>();while(!robot.Ready)yield return null;
                robot.enabled=false; // Exercise mounting in its surveyed, open starting space.
                VerificationRenderTarget.Initialize(cameraRig.GetComponent<Camera>());
                var start=cat.session.definition.spawnPosition;cat.Teleport(start);yield return new WaitForSeconds(1.5f);
                yield return Jump(false,choice.id);cat.Teleport(start);yield return new WaitForSeconds(.2f);
                yield return Jump(true,choice.id);cat.Teleport(start);yield return new WaitForSeconds(.2f);
                yield return KitchenJump(choice.id);cat.Teleport(start);yield return new WaitForSeconds(.2f);
                cat.SetJumpHeld(true);yield return new WaitForSeconds(.3f);cat.Paused=true;yield return null;cat.Paused=false;
                cat.SetJumpHeld(false);yield return new WaitForSeconds(.25f);
                Check("pause-cancels-charge-"+choice.id,Mathf.Abs(cat.transform.position.y-start.y)<.07f);
                // F now only changes posture after landing on the scene furniture.
                bool approached=false;
                for(int attempt=0;attempt<6&&!approached;attempt++)
                {
                    robot.enabled=false;
                    cat.Teleport(robot.Seat+Vector3.up*.15f);yield return new WaitForSeconds(.45f);
                    approached=robot.TryMount();
                    robot.enabled=true;
                    if(!approached){cat.Teleport(start);yield return new WaitForSeconds(1.5f);}
                }
                robot.enabled=true;Check("mount-"+choice.id,approached,"robot="+robot.transform.position+" cat="+cat.transform.position+" status="+robot.Status);
                if(approached)
                {
                    var before=cat.transform.position;var relative=robot.transform.InverseTransformPoint(before);yield return new WaitForSeconds(8);yield return new WaitForEndOfFrame();
                    Check("ride-follows-"+choice.id,cat.IsRiding&&!cat.GetComponent<CharacterController>().enabled&&Vector3.Distance(relative,robot.transform.InverseTransformPoint(cat.transform.position))<.002f,
                        "travel="+Vector3.Distance(before,cat.transform.position).ToString("F3"));
                    yield return Capture("ride-"+choice.id);
                    var frozen=robot.transform.position;cat.Paused=true;yield return new WaitForSecondsRealtime(.25f);
                    Check("pause-freezes-robot-"+choice.id,Vector3.Distance(frozen,robot.transform.position)<.0001f);cat.Paused=false;
                    bool off=robot.TryDismount();Check("safe-dismount-"+choice.id,off&&!cat.IsRiding&&cat.GetComponent<CharacterController>().enabled);
                }
            }
            if(args.Contains("--jump-vacuum-quick")){Finish();yield break;}
            // Observe autonomous traversal, including in-place turns and forward-only translation.
            cat.Teleport(cat.session.definition.spawnPosition);
            float until=Time.time+300;var last=robot.transform.position;var lastForward=robot.transform.forward;
            bool forwardOnly=true,turnsInPlace=true;float travel=0;int turns=0;
            var route=new List<string>();float nextTrace=0;
            float lastMotion=Time.time;
            while(Time.time<until)
            {
                yield return new WaitForFixedUpdate();var delta=robot.transform.position-last;
                var horizontal=Vector3.ProjectOnPlane(delta,Vector3.up);
                if(horizontal.magnitude>.0001f)forwardOnly&=Vector3.Dot(horizontal.normalized,lastForward)>.98f;
                float angle=Vector3.Angle(lastForward,robot.transform.forward);
                if(angle>.05f){turns++;turnsInPlace&=delta.magnitude<.001f;}
                travel+=delta.magnitude;last=robot.transform.position;lastForward=robot.transform.forward;
                if(delta.magnitude>.0001f)lastMotion=Time.time;
                if(Time.time>=nextTrace)
                {
                    nextTrace=Time.time+1;
                    route.Add(robot.transform.position+" "+robot.Status+" target="+robot.CurrentWaypoint+" blocker="+robot.LastMotionBlocker+" reachable="+robot.ReachableRoomNames);
                    File.WriteAllLines(Path.Combine(output,"robot-route.txt"),route);
                }
                if(Time.time-lastMotion>20){Check("no-persistent-stall",false,route.LastOrDefault());break;}
                if(robot.RoomsVisited>=8)break;
            }
            Check("forward-only",forwardOnly,"travel="+travel.ToString("F2"));Check("turns-in-place",turnsInPlace&&turns>0,"turn-frames="+turns);
            File.WriteAllLines(Path.Combine(output,"robot-route.txt"),route);
            Check("room-roaming",robot.RoomsVisited>=8,"rooms="+robot.VisitedRoomNames+" reachable="+robot.ReachableRoomNames);
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Verification temporary obstacle";
            obstacle.transform.position=robot.transform.position+robot.transform.forward*.58f+Vector3.up*.20f;
            obstacle.transform.localScale=new Vector3(.24f,.4f,.24f);Physics.SyncTransforms();
            var blocker=obstacle.GetComponent<Collider>();var shape=robot.GetComponentInChildren<MeshCollider>();
            bool penetrated=false;float aroundTravel=0;var previous=robot.transform.position;float avoidUntil=Time.time+10;
            while(Time.time<avoidUntil)
            {
                yield return new WaitForFixedUpdate();
                if(Physics.ComputePenetration(shape,shape.transform.position,shape.transform.rotation,blocker,blocker.transform.position,blocker.transform.rotation,out _,out float penetration)&&penetration>.002f)penetrated=true;
                aroundTravel+=Vector3.Distance(previous,robot.transform.position);previous=robot.transform.position;
            }
            Check("new-obstacle-no-penetration",!penetrated);Check("new-obstacle-keeps-roaming",aroundTravel>.25f,"travel="+aroundTravel.ToString("F2"));Destroy(obstacle);
            int previousRobot=robot.GetInstanceID();cat.session.Replay();yield return new WaitForSecondsRealtime(.5f);
            while(!FindFirstObjectByType<NewHouseExploration>().IsReady)yield return null;
            robot=FindFirstObjectByType<RobotVacuum>();while(!robot.Ready)yield return null;
            Check("replay-resets-robot",robot.GetInstanceID()!=previousRobot&&!robot.Rider&&!FindFirstObjectByType<CatController>().IsRiding);
            Finish();
        }
        IEnumerator Jump(bool charged,string id)
        {
            float floor=cat.transform.position.y;cat.SetJumpHeld(true);yield return new WaitForSeconds(charged?.9f:.06f);
            cat.SetJumpHeld(false);float peak=floor,until=Time.time+1.8f;
            while(Time.time<until){peak=Mathf.Max(peak,cat.transform.position.y);yield return null;}
            Check((charged?"charged-jump-":"short-jump-")+id,charged?peak-floor>1.25f:Mathf.Abs((peak-floor)-cat.jumpHeight)<.12f,
                "height="+(peak-floor).ToString("F3"));
            Check((charged?"charged-land-":"short-land-")+id,cat.GetComponent<CharacterController>().isGrounded&&Mathf.Abs(cat.transform.position.y-floor)<.07f);
        }
        IEnumerator KitchenJump(string id)
        {
            var sink=FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None).First(i=>i.placement.Contains("KIT_NH020"));
            var bounds=sink.Bounds;bool landed=false;string detail="sink="+bounds;int attempts=0;
            foreach(var outward in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            {
                for(int offset=-1;offset<=1&&!landed;offset++)
                {
                    float extent=Mathf.Abs(outward.x)>.5f?bounds.extents.x:bounds.extents.z;
                    var tangent=Vector3.Cross(Vector3.up,outward);
                    var from=bounds.center+outward*(extent+.30f)+tangent*(offset*.15f);from.y=.02f;
                    if(!Physics.Raycast(from+Vector3.up*.2f,Vector3.down,out var floor,.4f,~(1<<8),QueryTriggerInteraction.Ignore)||floor.point.y>.1f)continue;
                    from.y=floor.point.y+.02f;
                    if(Physics.CheckCapsule(from+Vector3.up*.14f,from+Vector3.up*.27f,.09f,~(1<<8),QueryTriggerInteraction.Ignore))continue;
                    attempts++;cat.Teleport(from);cameraRig.yaw=Mathf.Atan2(-outward.x,-outward.z)*Mathf.Rad2Deg;
                    yield return new WaitForSeconds(.2f);cat.SetJumpHeld(true);yield return new WaitForSeconds(.9f);cat.SetJumpHeld(false);
                    cat.VerificationRun=true;cat.VerificationMovement=Vector2.up;
                    var trace=new List<string>();float until=Time.time+1.07f;
                    while(Time.time<until){trace.Add(cat.transform.position.ToString("F3")+" "+cat.MovementBlocker);yield return null;}
                    File.WriteAllLines(Path.Combine(output,"kitchen-"+id+"-"+attempts+".txt"),trace);
                    cat.VerificationMovement=Vector2.zero;cat.VerificationRun=false;
                    yield return new WaitForSeconds(.8f);
                    var p=cat.transform.position;
                    landed=cat.GetComponent<CharacterController>().isGrounded&&p.y>.65f&&p.x>bounds.min.x-.1f&&p.x<bounds.max.x+.1f&&p.z>bounds.min.z-.1f&&p.z<bounds.max.z+.1f;
                    detail="attempts="+attempts+" position="+p+" sink="+bounds+" blocker="+cat.MovementBlocker;
                    if(landed){yield return Capture("kitchen-jump-"+id);break;}
                }
                if(landed)break;
            }
            Check("kitchen-sink-jump-"+id,landed,detail);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();var image=VerificationRenderTarget.Capture();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);
        }
        void Finish()
        {
            if(output==null)return;
            var report=new Report{passed=checks.All(c=>c.passed)&&errors.Count==0,checks=checks.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(Path.Combine(output,"jump-vacuum.json"),JsonUtility.ToJson(report,true));output=null;
            Application.logMessageReceived-=Log;CatCatalog.Select(previousCat,false);Application.Quit(report.passed?0:1);
        }
    }
}
