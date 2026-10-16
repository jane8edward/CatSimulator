using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatSimulator
{
    // Opt-in physical invariants and actual player contacts; no effect in normal play.
    public sealed class CatPolishVerification : MonoBehaviour
    {
        [Serializable] public class MassCheck {public float mass,expected,actual;public bool passed;}
        [Serializable] public class RateCheck {public int renderRate;public float peakSpeed;public bool passed;}
        [Serializable] public class ContactCheck {public string name,mode;public float catSpeed,peakHorizontal,peakUp,travel;public bool passed;}
        [Serializable] public class Report
        {
            public string version;public bool passed,separatingContact,offCentreEnergy,walkTailRaised,runTailTrails;
            public float incidentEnergy,remainingEnergy,idleTailRise,walkTailRise,runTailRise;
            public MassCheck[] masses;public RateCheck[] frameRates;public ContactCheck[] realContacts;public string[] errors;
        }
        readonly List<string> errors=new List<string>();Report report=new Report();string output;
        static int freeIndex;float began;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if(!Environment.GetCommandLineArgs().Contains("--verify-cat-polish"))return;
            var root=new GameObject("CatPolishVerification");DontDestroyOnLoad(root);root.AddComponent<CatPolishVerification>();
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string value,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors.Add(value);}
        void Update()
        {
            if(output==null || errors.Count==0 && Time.realtimeSinceStartup-began<100)return;
            report.errors=errors.ToArray();File.WriteAllText(Path.Combine(output,"cat-polish.json"),JsonUtility.ToJson(report,true));Application.Quit(2);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"--verification-output")+1];Directory.CreateDirectory(output);
            began=Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(.3f);CatCatalog.Select("balcony",false);SceneManager.LoadScene(ContentPreviewMenu.HomeScene);
            yield return new WaitForSecondsRealtime(.8f);
            while(!FindFirstObjectByType<NewHouseExploration>().IsReady)yield return null;
            var cat=FindFirstObjectByType<CatController>();var camera=FindFirstObjectByType<CatCamera>();cat.Automated=true;camera.Automated=true;
            VerificationRenderTarget.Initialize(camera.GetComponent<Camera>());QualitySettings.vSyncCount=0;
            report.version=Application.version;float mass=3.5f*Mathf.Pow(cat.BodyScale,3);const float speed=.6f;
            var checks=new List<MassCheck>();
            foreach(float objectMass in new[]{.045f,.22f,.65f,1.8f,8f})
            {
                var body=FreeBody(PrimitiveType.Sphere,objectMass);var prop=body.GetComponent<PushableProp>();
                prop.RequestContact(Vector3.right*speed,mass,body.position-Vector3.right*.04f,Vector3.left);
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                float expected=speed*mass/(mass+objectMass);float actual=body.linearVelocity.x;
                checks.Add(new MassCheck{mass=objectMass,expected=expected,actual=actual,passed=Mathf.Abs(expected-actual)<.003f});Destroy(body.gameObject);
            }
            report.masses=checks.ToArray();
            var rates=new List<RateCheck>();
            foreach(int fps in new[]{30,60,144})
            {
                Application.targetFrameRate=fps;var body=FreeBody(PrimitiveType.Sphere,.045f);var prop=body.GetComponent<PushableProp>();float peak=0;
                for(float until=Time.realtimeSinceStartup+.6f;Time.realtimeSinceStartup<until;)
                {
                    // Keep contact, including more render requests than physics steps.
                    prop.RequestContact(Vector3.right*speed,mass,body.position-Vector3.right*.04f,Vector3.left);
                    yield return null;peak=Mathf.Max(peak,body.linearVelocity.magnitude);
                }
                rates.Add(new RateCheck{renderRate=fps,peakSpeed=peak,passed=peak<=speed+.005f && peak>speed*.95f});Destroy(body.gameObject);
            }
            report.frameRates=rates.ToArray();Application.targetFrameRate=60;
            var separating=FreeBody(PrimitiveType.Sphere,.045f);separating.linearVelocity=Vector3.right*1.1f;
            separating.GetComponent<PushableProp>().RequestContact(Vector3.right*speed,mass,separating.position-Vector3.right*.04f,Vector3.left);
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();report.separatingContact=Mathf.Abs(separating.linearVelocity.x-1.1f)<.002f;Destroy(separating.gameObject);
            var offset=FreeBody(PrimitiveType.Cube,.2f);offset.transform.localScale=new Vector3(.12f,.08f,.08f);offset.ResetInertiaTensor();
            offset.GetComponent<PushableProp>().RequestContact(Vector3.right*speed,mass,offset.position+new Vector3(-.06f,.035f,.03f),Vector3.left);
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            float impulse=offset.mass*offset.linearVelocity.x;var actorAfter=Vector3.right*(speed-impulse/mass);
            var spin=Quaternion.Inverse(offset.rotation*offset.inertiaTensorRotation)*offset.angularVelocity;
            report.incidentEnergy=.5f*mass*speed*speed;
            report.remainingEnergy=.5f*mass*actorAfter.sqrMagnitude+.5f*offset.mass*offset.linearVelocity.sqrMagnitude+.5f*Vector3.Dot(spin,Vector3.Scale(offset.inertiaTensor,spin));
            report.offCentreEnergy=report.remainingEnergy<=report.incidentEnergy+.0005f;Destroy(offset.gameObject);

            var bodies=FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None).Where(i=>i.GetComponent<Rigidbody>()).ToArray();
            var chosen=new[]{"LIV_NH015","LIV_NH012","LIV_NH008","LIV_NH029_PlantTrailing_02"}
                .Select(id=>bodies.Single(i=>i.placement.Contains(id)).GetComponent<Rigidbody>()).ToArray();
            var originalBounds=cat.session.definition.activityBounds;var originalPosition=cat.transform.position;
            cat.session.definition.activityBounds=new Bounds(new Vector3(40,2,40),new Vector3(12,8,12));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="VerificationFloor";floor.transform.position=new Vector3(40,-.05f,40);floor.transform.localScale=new Vector3(12,.1f,12);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.55f,.52f,.45f);floor.GetComponent<Renderer>().sharedMaterial=material;
            var results=new List<ContactCheck>();
            foreach(var body in chosen)foreach(bool run in new[]{false,true})
            {
                // The test moves the real prop to a separate contact floor outside the
                // residence; suspend only its out-of-bounds recovery during that fixture.
                var recovery=body.GetComponent<NewHouseBall>();if(recovery)recovery.enabled=false;
                var position=body.position;var rotation=body.rotation;cat.Paused=true;
                body.position=new Vector3(40,.5f,40);Physics.SyncTransforms();var bounds=BodyBounds(body);
                body.position+=new Vector3(40-bounds.center.x,.001f-bounds.min.y,40-bounds.center.z);body.linearVelocity=body.angularVelocity=Vector3.zero;Physics.SyncTransforms();
                cat.Teleport(new Vector3(40,.035f,39.45f));cat.transform.rotation=Quaternion.identity;camera.yaw=0;camera.pitch=22;camera.distance=1.25f;
                yield return new WaitForSeconds(.3f);var start=body.position;
                cat.Paused=false;cat.VerificationRun=run;cat.VerificationMovement=Vector2.up;
                var check=new ContactCheck{name=body.name,mode=run?"run":"walk",catSpeed=run?cat.runSpeed:cat.walkSpeed};
                for(float until=Time.time+1.5f;Time.time<until;)
                {
                    yield return new WaitForFixedUpdate();check.peakHorizontal=Mathf.Max(check.peakHorizontal,Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude);check.peakUp=Mathf.Max(check.peakUp,body.linearVelocity.y);
                }
                cat.VerificationMovement=Vector2.zero;cat.VerificationRun=false;cat.Paused=true;
                check.travel=Vector3.ProjectOnPlane(body.position-start,Vector3.up).magnitude;
                // A cat can step over a flat book. A brief, friction-limited
                // nudge need not slide it centimetres across the floor.
                bool touched=body.name.Contains("_Book_")?check.travel>.0005f && check.peakHorizontal>.05f:check.travel>.015f;
                check.passed=touched && check.peakHorizontal<check.catSpeed*1.5f+.1f && check.peakUp<.8f;
                results.Add(check);body.position=position;body.rotation=rotation;body.linearVelocity=body.angularVelocity=Vector3.zero;body.Sleep();Physics.SyncTransforms();
                if(recovery)recovery.enabled=true;
            }
            report.realContacts=results.ToArray();
            cat.Teleport(new Vector3(40,.015f,40));cat.transform.rotation=Quaternion.identity;camera.yaw=-55;camera.pitch=13;camera.distance=1.15f;
            VerificationRenderTarget.Initialize(camera.GetComponent<Camera>());
            var tailBase=cat.visual.GetComponentsInChildren<Transform>().First(t=>t.name=="bone_45");
            var tailTip=cat.visual.GetComponentsInChildren<Transform>().First(t=>t.name=="bone_49");
            foreach(var state in new[]{"idle","walk","run"})
            {
                cat.animator.SetFloat("Speed",state=="idle"?0:state=="walk"?1:cat.runSpeed/cat.walkSpeed);cat.animator.Play("Locomotion",0,0);cat.animator.speed=1;
                yield return new WaitForSeconds(.35f);yield return new WaitForEndOfFrame();
                float rise=tailTip.position.y-tailBase.position.y;
                if(state=="idle")report.idleTailRise=rise;else if(state=="walk")report.walkTailRise=rise;else report.runTailRise=rise;
                var frame=VerificationRenderTarget.Capture();if(frame){File.WriteAllBytes(Path.Combine(output,"cat-"+state+".png"),frame.EncodeToPNG());Destroy(frame);}
            }
            float minimumTailRise=.1f*cat.BodyScale;
            report.walkTailRaised=report.idleTailRise>minimumTailRise && report.walkTailRise>minimumTailRise;report.runTailTrails=report.runTailRise<report.walkTailRise*.8f;
            cat.session.definition.activityBounds=originalBounds;cat.Teleport(originalPosition);Destroy(floor);Destroy(material);
            report.errors=errors.ToArray();report.passed=report.masses.All(c=>c.passed) && report.frameRates.All(c=>c.passed) && report.separatingContact && report.offCentreEnergy && report.realContacts.All(c=>c.passed) && report.walkTailRaised && report.runTailTrails && errors.Count==0;
            File.WriteAllText(Path.Combine(output,"cat-polish.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:2);
        }
        static Rigidbody FreeBody(PrimitiveType type,float mass)
        {
            var obj=GameObject.CreatePrimitive(type);obj.transform.position=new Vector3(70,3,70+2*freeIndex++);obj.transform.localScale=Vector3.one*.08f;obj.GetComponent<Renderer>().enabled=false;
            var body=obj.AddComponent<Rigidbody>();body.mass=mass;body.useGravity=false;body.linearDamping=0;body.angularDamping=0;obj.AddComponent<PushableProp>();return body;
        }
        static Bounds BodyBounds(Rigidbody body)
        {
            var colliders=body.GetComponentsInChildren<Collider>();var bounds=colliders[0].bounds;foreach(var c in colliders.Skip(1))bounds.Encapsulate(c.bounds);return bounds;
        }
    }
}
