using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatSimulator
{
    // Explicit smoke check for both levels and the three independent cat rigs.
    public sealed class NewHouseSelectionVerification : MonoBehaviour
    {
        [Serializable] class ChoiceResult
        {
            public string id;
            public bool gallery, selectionRetained, newHouse, firstLevel, secondLevel;
        }
        [Serializable] class Report
        {
            public bool passed, originalStartMenu, allSceneChoices, renderersValid, sharedMeshes;
            public int meshRenderers, uniqueMeshes, placements;
            public string gpu, stage;
            public ChoiceResult[] cats;
            public string[] errors;
        }
        readonly Report report = new Report();
        readonly List<string> errors = new List<string>();
        readonly List<ChoiceResult> choices = new List<ChoiceResult>();
        string output, previousId, previousPreference;
        bool hadPreference;
        float began;
        ContentPreviewMenu Menu => ContentPreviewMenu.Instance;
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        void Update()
        {
            if (output != null && Time.realtimeSinceStartup - began > 120)
            { errors.Add("Scene selection check timed out at " + report.stage); Finish(); }
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--verification-output");
            output = Path.GetFullPath(i >= 0 ? args[i + 1] : Path.Combine(Application.persistentDataPath, "NewHouseSelection"));
            Directory.CreateDirectory(output); began = Time.realtimeSinceStartup;
            previousId = CatCatalog.SelectedId;
            hadPreference = PlayerPrefs.HasKey("selected-cat"); previousPreference = PlayerPrefs.GetString("selected-cat");
            yield return new WaitForSecondsRealtime(.5f);
            report.gpu = SystemInfo.graphicsDeviceName;
            if (Menu == null) { errors.Add("Main menu missing"); Finish(); yield break; }
            report.originalStartMenu = SceneManager.GetActiveScene().name == ContentPreviewMenu.StartScene && Buttons().Count() == 2;
            Click("StartGame"); yield return null;
            report.allSceneChoices = new[] { "Level1", "Level2" }.All(Has) && !Has("Level3") &&
                !Application.CanStreamedLevelBeLoaded("Home90") && !Application.CanStreamedLevelBeLoaded("Home90_Preview");
            yield return Capture("scene-selection");
            Click("Back"); yield return null;
            foreach (var choice in CatCatalog.Current.cats)
            {
                report.stage = "cat-" + choice.id;
                var result = new ChoiceResult { id = choice.id }; choices.Add(result);
                Menu.ShowCats(); yield return null;
                Click("Preview-" + choice.id); yield return null;
                Click("Walking"); yield return new WaitForSecondsRealtime(.12f);
                var gallery = FindFirstObjectByType<CatModelGallery>();
                result.gallery = gallery != null && gallery.DisplayedId == choice.id && gallery.GetComponentInChildren<Animator>().avatar.isValid;
                Click("UseCat"); yield return null;
                Click("Back"); yield return null; Click("StartGame"); yield return null; Click("Level2");
                yield return new WaitForSecondsRealtime(.45f);
                while(!FindFirstObjectByType<NewHouseExploration>().IsReady)yield return null;
                var viewer = FindFirstObjectByType<NewHousePreview>();
                result.newHouse = SceneManager.GetActiveScene().name == ContentPreviewMenu.NewHouseScene && viewer && !Menu.IsOpen;
                result.selectionRetained = CatCatalog.SelectedId == choice.id;
                if (viewer && choices.Count == 1)
                {
                    var exploration = FindFirstObjectByType<NewHouseExploration>();
                    if (exploration) exploration.SetPreview(true);
                    var renderers = viewer.residence.GetComponentsInChildren<MeshRenderer>(true);
                    report.meshRenderers = renderers.Length;
                    report.uniqueMeshes = renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh).Distinct().Count();
                    report.sharedMeshes = report.uniqueMeshes < renderers.Length;
                    report.placements = viewer.placementCount;
                    report.renderersValid = renderers.All(r => r.GetComponent<MeshFilter>().sharedMesh &&
                        r.sharedMaterials.All(m => m && m.shader && !m.shader.name.Contains("InternalError")));
                    for (int v = 0; v < 4; v++)
                    {
                        viewer.SetView(v); yield return new WaitForSecondsRealtime(.15f);
                        yield return Capture("New_House_" + viewer.CurrentView);
                    }
                    viewer.SetView(0);
                    if (exploration) exploration.SetPreview(false);
                }
                Menu.Open(); Menu.ShowCats(); yield return new WaitForSecondsRealtime(.35f);
                result.selectionRetained &= CatCatalog.SelectedId == choice.id && Menu.CurrentPage == "Cats";
                Click("Back"); yield return null; Click("StartGame"); yield return null; Click("Level1");
                yield return new WaitForSecondsRealtime(.4f);
                var cat = FindFirstObjectByType<CatController>();
                result.firstLevel = SceneManager.GetActiveScene().name == ContentPreviewMenu.PlayableScene &&
                    cat && cat.animationDriver && cat.animationDriver.CatId == choice.id;
                Menu.ReturnToStart(); yield return new WaitForSecondsRealtime(.35f);
                Click("StartGame"); yield return null; Click("Level2");
                yield return new WaitForSecondsRealtime(.5f);
                cat = FindFirstObjectByType<CatController>();
                result.secondLevel = SceneManager.GetActiveScene().name == ContentPreviewMenu.HomeScene &&
                    cat && cat.animationDriver && cat.animationDriver.CatId == choice.id && cat.session.definition.IsExploration;
                Menu.ReturnToStart(); yield return new WaitForSecondsRealtime(.35f);
            }
            report.passed = report.originalStartMenu && report.allSceneChoices && report.renderersValid && report.sharedMeshes &&
                report.placements == 124 && report.meshRenderers >= 278 && choices.Count == 3 &&
                choices.All(c => c.gallery && c.selectionRetained && c.newHouse && c.firstLevel && c.secondLevel) && errors.Count == 0;
            report.stage = "complete"; Finish();
        }
        IEnumerable<Button> Buttons() => Menu.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy);
        bool Has(string name) => Buttons().Any(b => b.name == name);
        void Click(string name) => Buttons().Single(b => b.name == name).onClick.Invoke();
        IEnumerator Capture(string name)
        {
            var gallery=FindFirstObjectByType<CatModelGallery>();
            var game=FindFirstObjectByType<NewHouseExploration>();
            var camera=gallery?gallery.galleryCamera:game.IsPreview?game.preview.viewCamera:game.followCamera.GetComponent<Camera>();
            VerificationRenderTarget.Initialize(camera);
            yield return new WaitForEndOfFrame();
            var image=VerificationRenderTarget.Capture();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);
            yield return new WaitForSecondsRealtime(.15f);
        }
        void Finish()
        {
            if (hadPreference) PlayerPrefs.SetString("selected-cat", previousPreference); else PlayerPrefs.DeleteKey("selected-cat");
            PlayerPrefs.Save(); CatCatalog.Select(previousId, false);
            report.cats = choices.ToArray(); report.errors = errors.ToArray();
            File.WriteAllText(Path.Combine(output, "selection-check.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 2);
        }
    }
}
