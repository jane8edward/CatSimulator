using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatSimulator
{
    // Exercises the same interaction coroutines as the player's E key in a rendered build.
    public sealed class PlayableVerification : MonoBehaviour
    {
        public CatController cat;
        public LevelSession session;
        public CatCamera cameraRig;
        static int phase;
        static Report report;
        static string output, order;
        [Serializable] class Report
        {
            public string order, gpu, resolution;
            public bool optionalDoesNotComplete, firstTaskDoesNotComplete, bothComplete, replayResets, cupMoves, clothChanges;
            public bool movementWorks, jumpLands, climbHasSupport, cupColliderSized;
            public bool cameraClearAtInteractions = true;
            public bool roomRegressionPass;
            public float averageFps,p95FrameMs,p99FrameMs;
            public int frames;
            public string[] errors;
        }
        readonly List<string> errors=new List<string>();
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string message,string stack,LogType type) { if(type==LogType.Error || type==LogType.Exception) errors.Add(message); }

        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(!args.Contains("--verify-playable")) yield break;
            cat.Automated=true;cameraRig.Automated=true;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
            if(phase==1)
            {
                yield return new WaitForSeconds(1);
                var curtain=FindFirstObjectByType<CurtainInteractable>();var cup=FindFirstObjectByType<CupInteractable>();
                report.replayResets=!session.IsComplete && !session.HasObjective("spill-cup") && !session.HasObjective("damage-curtain")
                    && curtain.intact.activeSelf && !curtain.damaged.activeSelf && cup.cupBody.isKinematic;
                report.errors=report.errors.Concat(errors).ToArray();File.WriteAllText(Path.Combine(output,order+".json"),JsonUtility.ToJson(report,true));
                Application.Quit(report.errors.Length==0 && report.optionalDoesNotComplete && report.firstTaskDoesNotComplete
                    && report.bothComplete && report.replayResets && report.cupMoves && report.clothChanges && report.movementWorks
                    && report.jumpLands && report.climbHasSupport && report.cupColliderSized && report.cameraClearAtInteractions && report.roomRegressionPass?0:2);yield break;
            }
            order=args.Contains("--curtain-first")?"curtain-first":"cup-first";
            var index=Array.IndexOf(args,"--verification-output");output=index>=0?args[index+1]:Path.Combine(Application.persistentDataPath,"Verification");Directory.CreateDirectory(output);
            report=new Report{order=order,gpu=SystemInfo.graphicsDeviceName,resolution=Screen.width+"x"+Screen.height};
            Screen.SetResolution(1920,1080,FullScreenMode.FullScreenWindow);
            yield return new WaitForSeconds(3);
            VerificationRenderTarget.Initialize(cameraRig.GetComponent<Camera>());
            var initial=cat.transform.position;
            cat.VerificationMovement=Vector2.up;yield return new WaitForSeconds(.7f);cat.VerificationMovement=Vector2.zero;
            report.movementWorks=Vector3.Distance(initial,cat.transform.position)>.25f;
            var groundedHeight=cat.transform.position.y;cat.RequestJump();yield return new WaitForSeconds(.2f);
            var rose=cat.transform.position.y-groundedHeight>.2f;yield return new WaitForSeconds(1);
            report.jumpLands=rose && Mathf.Abs(cat.transform.position.y-groundedHeight)<.08f;
            var cupCollider=FindFirstObjectByType<CupInteractable>().GetComponentInChildren<Collider>();report.cupColliderSized=cupCollider.bounds.size.magnitude<.3f;
            var roomRegression=gameObject.AddComponent<RoomRegressionVerification>();
            yield return roomRegression.Run(cat,cameraRig,output,order);report.roomRegressionPass=roomRegression.result.passed;
            cat.Teleport(initial);cameraRig.yaw=180;yield return new WaitForSeconds(.5f);
            yield return Capture("initial");
            var durations=new List<float>();var begin=Time.realtimeSinceStartup;
            report.resolution="1920x1080";
            while(Time.realtimeSinceStartup-begin<8)
            {
                cameraRig.yaw+=Time.unscaledDeltaTime*25;
                durations.Add(Time.unscaledDeltaTime);yield return null;
            }
            durations.Sort();report.frames=durations.Count;report.averageFps=durations.Count/durations.Sum();
            report.p95FrameMs=durations[(int)(durations.Count*.95f)]*1000;report.p99FrameMs=durations[(int)(durations.Count*.99f)]*1000;
            var tree=FindFirstObjectByType<ClimbInteractable>();cat.Teleport(tree.Anchor.position-Vector3.forward*.3f);cameraRig.yaw=225;
            GameFrameRecorder recorder=null;
            if(args.Contains("--record-preview"))
            {
                Application.targetFrameRate=30;recorder=new GameObject("PreviewRecorder").AddComponent<GameFrameRecorder>();
                recorder.folder=Path.Combine(output,"preview-frames-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                File.WriteAllText(Path.Combine(output,"preview-frames.path.txt"),recorder.folder);
            }
            yield return Interact(tree);report.optionalDoesNotComplete=!session.IsComplete && session.DiscoveryCount==1;
            report.climbHasSupport=Physics.Raycast(cat.transform.position+Vector3.up*.01f,Vector3.down,.09f,~(1<<8));yield return Capture("tree");
            var box=FindFirstObjectByType<BoxInteractable>();cat.Teleport(box.Anchor.position+Vector3.back*.45f);
            yield return Interact(box);report.optionalDoesNotComplete &= !session.IsComplete && session.DiscoveryCount==2;
            var cupTarget=FindFirstObjectByType<CupInteractable>();var cloth=FindFirstObjectByType<CurtainInteractable>();
            CatInteractable first=order=="cup-first"?(CatInteractable)cupTarget:cloth;
            CatInteractable second=order=="cup-first"?(CatInteractable)cloth:cupTarget;
            var cupStart=cupTarget.cupBody.position;
            cat.Teleport(first.Anchor.position-Vector3.forward*.3f);yield return Interact(first);
            report.firstTaskDoesNotComplete=!session.IsComplete;yield return Capture("first-task");
            cat.Teleport(second.Anchor.position-Vector3.forward*.3f);yield return Interact(second);
            yield return new WaitForSeconds(1);report.bothComplete=session.IsComplete;
            report.cupMoves=Vector3.Distance(cupTarget.cupBody.position,cupStart)>.02f;
            report.clothChanges=!cloth.intact.activeSelf && cloth.damaged.activeSelf;yield return Capture("complete");
            FindFirstObjectByType<GameHud>().ShowResult();yield return Capture("completion-ui");
            if(recorder!=null) recorder.Recording=false;
            report.errors=errors.ToArray();phase=1;session.Replay();
        }

        IEnumerator Interact(CatInteractable target)
        {
            cat.BeginInteraction(target);var timeout=Time.time+12;
            while(cat.IsBusy && Time.time<timeout) yield return null;
            if(cat.IsBusy) errors.Add("Interaction timed out: "+target.interactionId);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();
            report.cameraClearAtInteractions &= Vector3.Distance(cameraRig.transform.position,cat.transform.position+Vector3.up*.26f)>=.9f;
            var texture=VerificationRenderTarget.Capture();
            if(texture==null) { errors.Add("Screenshot capture failed: "+name);yield break; }
            File.WriteAllBytes(Path.Combine(output,order+"-"+name+".png"),texture.EncodeToPNG());Destroy(texture);
        }
    }
}
