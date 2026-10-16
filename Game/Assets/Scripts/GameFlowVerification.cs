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
    // Runs only with --verify-game-flow. Exercises UI routes, separate rigs and both levels.
    public sealed class GameFlowVerification : MonoBehaviour
    {
        [Serializable] public sealed class CatResult
        {
            public string id; public bool nativeRig, firstLevelTasks, exploration, jumps, stableSteering, replayKeepsCat;
            public bool scaleCorrect, boundSkin;
            public float firstLevelScale,homeScale,controllerHeight,visualScale;
            public float jumpRise, jumpLandingError, jumpWorstFrame;
            public int bones; public float averageFps;
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
            QualitySettings.vSyncCount=0; Application.targetFrameRate=90;
            hadSelection = PlayerPrefs.HasKey("selected-cat"); previousSelection = PlayerPrefs.GetString("selected-cat","original");
            report.gpu = SystemInfo.graphicsDeviceName;
            yield return new WaitForSecondsRealtime(.5f);
            var menu = ContentPreviewMenu.Instance;
            if (menu == null) { errors.Add("Start menu missing"); Finish(); yield break; }
            report.twoStartButtons = SceneManager.GetActiveScene().name == ContentPreviewMenu.StartScene &&
                menu.GetComponentsInChildren<Button>().Count(b=>b.gameObject.activeInHierarchy) == 2;
            Stage("start-menu"); yield return Capture("start-menu");
            Click("StartGame"); yield return null;
            report.levelChoice = HasButton("Level1") && HasButton("Level2") && !HasButton("Level3"); yield return Capture("level-selection");
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
                while(!FindFirstObjectByType<NewHouseExploration>().IsReady)yield return null;
                cat = FindFirstObjectByType<CatController>(); camera = FindFirstObjectByType<CatCamera>(); cat.Automated = true; camera.Automated = true;
                result.exploration = cat.session.definition.IsExploration && !cat.session.IsComplete && cat.animationDriver.CatId == choice.id;
                result.homeScale=cat.BodyScale;result.controllerHeight=cat.GetComponent<CharacterController>().height;result.visualScale=cat.visual.localScale.x;
                result.scaleCorrect=Mathf.Abs(result.firstLevelScale-1)<.001f && Mathf.Abs(result.homeScale-.675f)<.001f &&
                    Mathf.Abs(result.controllerHeight-.36f*.675f)<.001f && Mathf.Abs(result.visualScale-choice.visualScale*.675f)<.001f;
                var house=FindFirstObjectByType<NewHouseExploration>();
                result.exploration &= house && SceneManager.GetActiveScene().name==ContentPreviewMenu.HomeScene && cat.session.definition.levelId=="home-02";
                VerificationRenderTarget.Initialize(camera.GetComponent<Camera>());
                Stage("second-level-"+choice.id);yield return Capture("second-level-"+choice.id);
                var start=cat.transform.position;camera.yaw=180;cat.VerificationMovement=Vector2.up;
                yield return new WaitForSeconds(.5f);cat.VerificationMovement=Vector2.zero;
                result.stableSteering=Vector3.Dot(cat.transform.position-start,Vector3.back)>cat.walkSpeed*.3f;
                var layout=FindFirstObjectByType<NewHouseLayout>();
                cat.Teleport(layout.Remap(new Vector3(-8.1f,.035f,-3.4f)));
                var controller=cat.GetComponent<CharacterController>();float settleUntil=Time.time+2;
                while(!controller.isGrounded && Time.time<settleUntil)yield return null;
                // Let the newly visible materials and scene-unload work finish before
                // sampling a jump; scene entry and steady play are separate measurements.
                yield return new WaitForSeconds(1);
                float ground=cat.transform.position.y,peak=ground;cat.RequestJump();
                for(float t=0;t<1.2f;t+=Time.deltaTime){yield return null;peak=Mathf.Max(peak,cat.transform.position.y);result.jumpWorstFrame=Mathf.Max(result.jumpWorstFrame,Time.deltaTime);}
                result.jumpRise=peak-ground;result.jumpLandingError=Mathf.Abs(cat.transform.position.y-ground);
                result.jumps=result.jumpRise>.25f && result.jumpLandingError<.06f && controller.isGrounded;
                Application.targetFrameRate=90;yield return new WaitForSecondsRealtime(.3f);
                int frames=0;float started=Time.realtimeSinceStartup;
                while(Time.realtimeSinceStartup-started<2){frames++;yield return null;}
                result.averageFps=frames/(Time.realtimeSinceStartup-started);
                cat.session.Replay(); yield return new WaitForSecondsRealtime(.4f);
                result.replayKeepsCat = FindFirstObjectByType<CatController>().animationDriver.CatId == choice.id;
                menu.ReturnToStart(); yield return new WaitForSecondsRealtime(.4f); Click("CatModels"); yield return null;
            }
            // Detailed furniture, eight-room traversal and contacts are covered by
            // --check-new-house-upgrade; this check covers the complete menu/task flow.
            report.passed = report.twoStartButtons && report.levelChoice && report.galleryChoices && report.selectionSaved && report.separateControllers &&
                results.Count == catalog.cats.Length && results.All(r=>r.nativeRig && r.firstLevelTasks && r.exploration && r.jumps && r.stableSteering && r.replayKeepsCat && r.averageFps>=59 && r.scaleCorrect) && errors.Count==0;
            Finish();
        }
        bool HasButton(string name) => ContentPreviewMenu.Instance.GetComponentsInChildren<Button>().Any(b=>b.name==name && b.gameObject.activeInHierarchy);
        void Click(string name) => ContentPreviewMenu.Instance.GetComponentsInChildren<Button>().Single(b=>b.name==name && b.gameObject.activeInHierarchy).onClick.Invoke();
        IEnumerator Capture(string name)
        {
            var gallery=FindFirstObjectByType<CatModelGallery>();
            var camera=gallery?gallery.galleryCamera:FindFirstObjectByType<CatCamera>().GetComponent<Camera>();
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

    }
}
