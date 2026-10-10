using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CatSimulator
{
    // Runs only when explicitly requested by the verification command line.
    public sealed class BedroomVerification : MonoBehaviour
    {
        [Serializable] public class WalkCheck
        {
            public string route;
            public bool reached, noReversal;
            public float travel, minimumCameraDistance;
        }
        [Serializable] public class Report
        {
            public string version, resolution, gpu;
            public bool passed, doorwayClear, floorSupported, catVisualPreserved;
            public int physicalMeshes;
            public string[] missingCollisions, errors;
            public WalkCheck[] walking;
            public Vector3 bedroomFloorSize;
            public float averageFps, p95FrameMs, p99FrameMs;
        }
        readonly List<string> errors=new List<string>();
        readonly List<WalkCheck> walking=new List<WalkCheck>();
        CatController cat;
        CatCamera rig;
        GameObject room;
        string output;

        void OnEnable() => Application.logMessageReceived+=OnLog;
        void OnDisable() => Application.logMessageReceived-=OnLog;
        void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception) errors.Add(message);
        }

        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(!args.Contains("--verify-bedroom")) yield break;
            var index=Array.IndexOf(args,"--verification-output");
            output=index>=0?args[index+1]:Path.Combine(Application.persistentDataPath,"BedroomVerification");
            Directory.CreateDirectory(output);
            cat=FindFirstObjectByType<CatController>();rig=FindFirstObjectByType<CatCamera>();room=GameObject.Find("LivingRoom");
            cat.Automated=true;rig.Automated=true;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
            Screen.SetResolution(1920,1080,FullScreenMode.FullScreenWindow);
            yield return new WaitForSeconds(3);
            VerificationRenderTarget.Initialize(rig.GetComponent<Camera>());
            var report=new Report {version=Application.version,resolution="1920x1080",gpu=SystemInfo.graphicsDeviceName};
            var physical=room.GetComponentsInChildren<MeshFilter>(true).Where(f=>!f.name.StartsWith("COL_")
                && !f.name.StartsWith("FloorPlank_") && !f.name.Contains("Outline") && f.name!="RugFibres"
                && f.name!="WaterInside" && f.GetComponent<Renderer>()!=null && f.GetComponent<Renderer>().enabled).ToArray();
            report.physicalMeshes=physical.Length;
            report.missingCollisions=physical.Where(f=>f.GetComponent<Collider>()==null).Select(f=>f.name).ToArray();
            report.bedroomFloorSize=Find("Bedroom_FloorBase").GetComponent<Renderer>().bounds.size;
            var skins=cat.visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            var clips=cat.animator.runtimeAnimatorController.animationClips;
            report.catVisualPreserved=skins.Length>=4 && skins.All(s=>s.bones.All(b=>b!=null))
                && clips.Any(c=>c.name.EndsWith("Walk",StringComparison.Ordinal))
                && clips.Any(c=>c.name.EndsWith("Run",StringComparison.Ordinal));
            var doorway=Vector3.Lerp(Find("BedroomDoorLiving").position,Find("BedroomDoorInside").position,.5f);
            report.doorwayClear=!Physics.CheckCapsule(doorway+Vector3.up*.13f,doorway+Vector3.up*.27f,.126f,~(1<<8));
            report.floorSupported=new[]{"BedroomDoorLiving","BedroomDoorInside","BedroomWalkStart","BedroomWalkEnd"}
                .All(n=>Physics.Raycast(Find(n).position+Vector3.up*.2f,Vector3.down,.5f,~(1<<8)));
            yield return Walk("living-to-bedroom",Find("BedroomDoorLiving").position,Find("BedroomDoorInside").position);
            yield return Walk("bedroom-to-living",Find("BedroomDoorInside").position,Find("BedroomDoorLiving").position);
            yield return Walk("bedroom-center-aisle",Find("BedroomWalkStart").position,Find("BedroomWalkEnd").position);
            report.walking=walking.ToArray();

            cat.Teleport(Find("BedroomPreviewCat").position);cat.transform.rotation=Quaternion.Euler(0,180,0);rig.yaw=180;
            yield return new WaitForSeconds(.5f);
            yield return Capture("bedroom-cat-view");
            var durations=new List<float>();var begin=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-begin<8)
            {
                rig.yaw+=Time.unscaledDeltaTime*25;durations.Add(Time.unscaledDeltaTime);yield return null;
            }
            durations.Sort();report.averageFps=durations.Count/durations.Sum();
            report.p95FrameMs=durations[(int)(durations.Count*.95f)]*1000;
            report.p99FrameMs=durations[(int)(durations.Count*.99f)]*1000;
            rig.enabled=false;cat.enabled=false;FindFirstObjectByType<GameHud>().GetComponent<Canvas>().enabled=false;
            var camera=rig.GetComponent<Camera>();camera.fieldOfView=65;
            camera.transform.position=new Vector3(8.94f,2.35f,2.25f);
            camera.transform.LookAt(new Vector3(6.91f,.77f,-.65f));
            yield return Capture("bedroom-wide");
            camera.fieldOfView=58;camera.transform.position=new Vector3(2.85f,1.25f,1.10f);
            camera.transform.LookAt(new Vector3(6.95f,.76f,-.30f));
            yield return Capture("bedroom-doorway");
            Find("Bedroom_Ceiling").GetComponent<Renderer>().enabled=false;
            camera.orthographic=true;camera.orthographicSize=4.65f;
            camera.transform.position=new Vector3(2.8f,13,0);camera.transform.rotation=Quaternion.Euler(90,0,0);
            yield return Capture("home-layout");
            report.errors=errors.ToArray();
            report.passed=report.doorwayClear && report.floorSupported && report.catVisualPreserved
                && report.missingCollisions.Length==0 && walking.All(w=>w.reached && w.noReversal) && errors.Count==0;
            File.WriteAllText(Path.Combine(output,"bedroom-verification.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:2);
        }

        Transform Find(string name) => room.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);

        IEnumerator Walk(string name,Vector3 from,Vector3 to)
        {
            var forward=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized;
            var yaw=Quaternion.LookRotation(forward).eulerAngles.y;
            cat.Teleport(from);cat.transform.rotation=Quaternion.Euler(0,yaw,0);rig.yaw=yaw;
            yield return new WaitForSeconds(.3f);
            var start=cat.transform.position;var previous=start;var duration=Vector3.Distance(from,to)/cat.walkSpeed+.08f;
            var check=new WalkCheck {route=name,noReversal=true,minimumCameraDistance=float.MaxValue};
            cat.VerificationMovement=Vector2.up;var end=Time.time+duration;
            while(Time.time<end)
            {
                yield return null;
                check.noReversal &= Vector3.Dot(cat.transform.position-previous,forward)>=-.003f;
                check.minimumCameraDistance=Mathf.Min(check.minimumCameraDistance,
                    Vector3.Distance(rig.transform.position,cat.transform.position+Vector3.up*.26f));
                previous=cat.transform.position;
            }
            cat.VerificationMovement=Vector2.zero;
            check.travel=Vector3.Dot(cat.transform.position-start,forward);
            check.reached=check.travel>=Vector3.Distance(from,to)-.12f;
            walking.Add(check);
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();
            var image=VerificationRenderTarget.Capture();
            if(image==null) {errors.Add("Screenshot failed: "+name);yield break;}
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);
        }
    }
}
