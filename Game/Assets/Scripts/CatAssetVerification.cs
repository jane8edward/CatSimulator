using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CatSimulator
{
    // A rendered animation showcase and skin/scale check in the built game.
    public sealed class CatAssetVerification : MonoBehaviour
    {
        [Serializable] class Report
        {
            public string gpu,version,resolution;
            public bool allAnimationsMove,materialsSupported,headFacesForward;
            public Vector3 idleBounds;
            public string[] clips,errors;
            public float walkPawMotion,runPawMotion;
            public bool backSkinHasNoControllers;
            public int sampledWalkPhases;
        }
        readonly List<string> errors=new List<string>();
        string output;
        CatController cat;
        CatCamera rig;
        Report report;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if(Environment.GetCommandLineArgs().Contains("--verify-cat"))
                new GameObject("CatAssetVerification").AddComponent<CatAssetVerification>();
        }

        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error || type==LogType.Exception) errors.Add(message);}

        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"--verification-output");
            output=index>=0?args[index+1]:Application.persistentDataPath;
            Directory.CreateDirectory(output);
            cat=FindFirstObjectByType<CatController>();rig=FindFirstObjectByType<CatCamera>();
            cat.Automated=true;rig.Automated=true;Application.targetFrameRate=30;
            Screen.SetResolution(1920,1080,FullScreenMode.FullScreenWindow);
            yield return new WaitForSeconds(3);
            cat.Teleport(new Vector3(-.7f,.024f,0));cat.transform.rotation=Quaternion.identity;
            cat.enabled=false;rig.enabled=false;
            Debug.Log("CAT_VERIFY_STAGE: close camera");
            var camera=Camera.main;
            camera.transform.position=cat.transform.position+new Vector3(.55f,.37f,.72f);
            camera.transform.LookAt(cat.transform.position+new Vector3(0,.245f,.03f));
            cat.animator.Play("Locomotion",0,0);cat.animator.SetFloat("Speed",0);
            yield return new WaitForSeconds(.4f);
            Debug.Log("CAT_VERIFY_STAGE: renderer bounds");
            var renderers=cat.visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var head=cat.visual.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
            Debug.Log("CAT_VERIFY_STAGE: report; controller="+(cat.animator.runtimeAnimatorController!=null)+"; missing bones="+renderers.Sum(r=>r.bones.Count(b=>b==null)));
            report=new Report{gpu=SystemInfo.graphicsDeviceName,version=Application.version,
                resolution=Screen.width+"x"+Screen.height,idleBounds=bounds.size,
                headFacesForward=Vector3.Dot(head.position-cat.transform.position,cat.transform.forward)>.10f,
                clips=cat.animator.runtimeAnimatorController.animationClips.Select(c=>c.name).Distinct().ToArray(),
                materialsSupported=renderers.SelectMany(r=>r.sharedMaterials).All(m=>m!=null && m.shader.isSupported)};
            report.backSkinHasNoControllers=renderers.All(r=>r.bones.All(b=>b==null || !new[]{
                "Spine_controller_1","Spine_controller_2","Head_controller","Tail_controller",
                "Knee_front_IK_L","Knee_front_IK_R","Foot_front_IK_L","Foot_front_IK_R"}.Contains(b.name)));
            if(!report.backSkinHasNoControllers) errors.Add("Non-deforming controls present in the runtime skin");
            Debug.Log("CAT_VERIFY_STAGE: capture closeup");
            yield return Capture("cat-closeup");
            var recorder=new GameObject("CatShowcaseRecorder").AddComponent<GameFrameRecorder>();
            recorder.folder=Path.Combine(output,"cat-showcase-frames-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            File.WriteAllText(Path.Combine(output,"cat-showcase-frames.path.txt"),recorder.folder);
            yield return new WaitForSeconds(2);
            camera.transform.position=cat.transform.position+new Vector3(.7f,.30f,-.5f);
            camera.transform.LookAt(cat.transform.position+new Vector3(0,.245f,.02f));
            report.walkPawMotion=0;
            yield return Locomotion(1,v=>report.walkPawMotion=v);
            yield return Capture("cat-walk");
            camera.transform.position=cat.transform.position+new Vector3(.015f,.59f,-.66f);
            camera.transform.LookAt(cat.transform.position+new Vector3(0,.235f,.03f));
            var walk=cat.animator.runtimeAnimatorController.animationClips.First(c=>c.name.EndsWith("Walk",StringComparison.Ordinal));
            cat.animator.SetFloat("Speed",0);cat.animator.ResetTrigger("Land");cat.animator.ResetTrigger("Jump");
            cat.animator.Play("WalkAction",0,0);cat.animator.Update(0);cat.animator.speed=0;
            for(var phase=0;phase<8;phase++)
            {
                cat.animator.Play("WalkAction",0,phase/8f);cat.animator.Update(0);
                yield return Capture("cat-back-walk-"+phase);
                report.sampledWalkPhases++;
            }
            cat.animator.speed=1;
            yield return Locomotion(1,v=>{});
            camera.transform.position=cat.transform.position+new Vector3(.7f,.30f,-.5f);
            camera.transform.LookAt(cat.transform.position+new Vector3(0,.245f,.02f));
            report.runPawMotion=0;
            yield return Locomotion(1.8f/.86f,v=>report.runPawMotion=v);
            yield return Capture("cat-run");
            cat.animator.SetFloat("Speed",0);cat.animator.Play("Locomotion",0,0);
            camera.transform.position=cat.transform.position+new Vector3(.55f,.37f,.72f);
            camera.transform.LookAt(cat.transform.position+new Vector3(0,.245f,.03f));
            yield return new WaitForSeconds(1);
            cat.animator.SetTrigger("Paw");yield return new WaitForSeconds(.42f);yield return Capture("cat-paw");
            yield return new WaitForSeconds(1);
            cat.animator.SetTrigger("Scratch");yield return new WaitForSeconds(.55f);yield return Capture("cat-scratch");
            yield return new WaitForSeconds(2.1f);
            recorder.Recording=false;
            report.allAnimationsMove=report.walkPawMotion>.025f && report.runPawMotion>.025f;
            report.errors=errors.ToArray();
            File.WriteAllText(Path.Combine(output,"cat-asset.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.allAnimationsMove && report.headFacesForward && report.materialsSupported && report.backSkinHasNoControllers && errors.Count==0?0:2);
        }

        IEnumerator Locomotion(float speed,Action<float> save)
        {
            cat.animator.SetFloat("Speed",speed);cat.animator.Play("Locomotion",0,0);
            yield return new WaitForSeconds(.2f);
            var paw=cat.visual.GetComponentsInChildren<Transform>().First(t=>t.name=="Foot_front_L");
            var origin=paw.position;float max=0,time=0;
            while(time<2.1f){max=Mathf.Max(max,Vector3.Distance(origin,paw.position));time+=Time.deltaTime;yield return null;}
            save(max);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));
            yield return null;
        }
    }
}
