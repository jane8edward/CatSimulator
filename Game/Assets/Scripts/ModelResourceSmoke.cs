using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatSimulator
{
    // Explicit command-line checks for the added resources in a standalone player.
    public sealed class ModelResourceSmoke : MonoBehaviour
    {
        readonly List<string> errors = new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--verify-model-resources")) return;
            var go = new GameObject("ModelResourceSmoke"); DontDestroyOnLoad(go); go.AddComponent<ModelResourceSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string m, string s, LogType t) { if(t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m); }
        [Serializable] class Report
        {
            public bool passed, avatarValid, animationMoved, pauseStable, resetWorks, homeLoaded;
            public float travelledMeters, expectedMeters, maxBoneMotionMeters;
            public int homeRenderers;
            public string gpu; public string[] errors;
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            var idx = Array.IndexOf(args, "--verification-output");
            var output = idx >= 0 ? args[idx+1] : Path.Combine(Application.persistentDataPath, "ModelResources");
            Directory.CreateDirectory(output);
            SceneManager.LoadScene("Walk_Preview"); yield return null; yield return new WaitForSeconds(.3f);
            var playback = FindFirstObjectByType<CatWalkPlayback>();
            var report = new Report { gpu = SystemInfo.graphicsDeviceName };
            if (!playback) { errors.Add("Walk playback missing"); Finish(report, output); yield break; }
            playback.showControls = false;
            report.avatarValid = playback.animator.avatar && playback.animator.avatar.isValid;
            var bones = playback.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("bone_")).ToArray();
            var before = bones.Select(t => t.localPosition).ToArray(); var rotations = bones.Select(t => t.localRotation).ToArray();
            var start = playback.subject.position; var elapsed = playback.elapsed;
            yield return new WaitForSeconds(.43f);
            report.travelledMeters = Vector3.Distance(start, playback.subject.position);
            report.expectedMeters = (playback.elapsed - elapsed) * playback.strideMeters / playback.cycleSeconds;
            for (var i = 0; i < bones.Length; i++) {
                report.maxBoneMotionMeters = Mathf.Max(report.maxBoneMotionMeters, Vector3.Distance(before[i], bones[i].localPosition));
                report.animationMoved |= Vector3.Distance(before[i], bones[i].localPosition) > .001f || Quaternion.Angle(rotations[i], bones[i].localRotation) > 1;
            }
            playback.paused = true; yield return null; start = playback.subject.position;
            yield return new WaitForSeconds(.15f); report.pauseStable = Vector3.Distance(start, playback.subject.position) < .0001f;
            playback.ResetPreview(); report.resetWorks = playback.elapsed == 0;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"walk-runtime.png"));
            yield return new WaitForSeconds(.2f);
            SceneManager.LoadScene("Home90_Preview"); yield return null; yield return new WaitForSeconds(.5f);
            var home = GameObject.Find("H90_Residence_Preview"); report.homeLoaded = home;
            report.homeRenderers = home ? home.GetComponentsInChildren<MeshRenderer>(true).Length : 0;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output,"home-runtime.png"));
            yield return new WaitForSeconds(.3f);
            report.passed = report.avatarValid && report.animationMoved && report.pauseStable && report.resetWorks && report.homeLoaded &&
                report.homeRenderers >= 800 && report.travelledMeters > .07f && Mathf.Abs(report.travelledMeters-report.expectedMeters) < .003f && errors.Count == 0;
            Finish(report, output);
        }
        void Finish(Report report, string output)
        {
            report.errors = errors.ToArray(); File.WriteAllText(Path.Combine(output, "runtime-smoke.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 2);
        }
    }
}
