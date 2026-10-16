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
    // Opt-in standalone check of the existing controller inside the new house.
    public sealed class NewHouseControlsVerification : MonoBehaviour
    {
        [Serializable] class CatResult
        {
            public string id, routeBlocker;
            public bool selectedCat, movement, running, jumping, animation, camera, wall, furniture, pause, preview, replay;
            public float walkMeters, runMeters, jumpMeters, minimumY = 100;
            public string[] visitedRooms;
        }
        [Serializable] class Report
        {
            public bool passed;
            public string stage, gpu;
            public CatResult[] cats;
            public string[] errors;
        }
        readonly Report report = new Report();
        readonly List<CatResult> results = new List<CatResult>();
        readonly List<string> errors = new List<string>();
        string output, selected;
        float began;
        CatController cat;
        CatCamera rig;
        NewHouseExploration gameplay;
        static Vector3 P(float x, float y, float z = .035f) => new Vector3(-x, z, -y);
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        void Update()
        {
            if (output != null && Time.realtimeSinceStartup - began > 180)
            { errors.Add("Controls check timed out: " + report.stage); Finish(); }
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "--verification-output");
            output = Path.GetFullPath(i >= 0 ? args[i + 1] : Path.Combine(Application.persistentDataPath, "NewHouseControls"));
            Directory.CreateDirectory(output); began = Time.realtimeSinceStartup;
            selected = CatCatalog.Selected.id; report.gpu = SystemInfo.graphicsDeviceName;
            yield return new WaitForSecondsRealtime(.4f);
            foreach (var choice in CatCatalog.Current.cats)
            {
                CatCatalog.Select(choice.id, false);
                ContentPreviewMenu.Instance.LoadLevel(2);
                yield return new WaitForSecondsRealtime(.6f);
                cat = FindFirstObjectByType<CatController>(); rig = FindFirstObjectByType<CatCamera>();
                gameplay = FindFirstObjectByType<NewHouseExploration>();
                var r = new CatResult { id = choice.id }; results.Add(r);
                Stage(choice.id + ":spawn");
                if (!cat || !rig || !gameplay) { errors.Add("Missing gameplay components: " + choice.id); Finish(); yield break; }
                cat.Automated = true; rig.Automated = true;
                r.selectedCat = cat.animationDriver && cat.animationDriver.CatId == choice.id && cat.animator.avatar.isValid && !cat.Paused;
                r.movement = true; r.camera = true;
                var bones = cat.visual.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(s => s.bones).Distinct().ToArray();
                foreach (var input in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, new Vector2(1,1) })
                {
                    cat.VerificationMovement = Vector2.zero; rig.yaw = 180; cat.Teleport(P(8.1f, 3.4f));
                    yield return new WaitForSecondsRealtime(.2f);
                    var start = cat.transform.position; var rotations = bones.Select(b => b.localRotation).ToArray();
                    var direction = (rig.MovementForward * input.y + rig.MovementRight * input.x).normalized;
                    cat.VerificationMovement = input; float time = Time.time;
                    yield return new WaitForSeconds(.38f);
                    cat.VerificationMovement = Vector2.zero;
                    var delta = Vector3.ProjectOnPlane(cat.transform.position - start, Vector3.up);
                    float expected = cat.walkSpeed * (Time.time - time);
                    r.movement &= delta.magnitude > expected * .8f && delta.magnitude < expected * 1.15f && Vector3.Angle(delta, direction) < 3;
                    if (input == Vector2.up) r.walkMeters = delta.magnitude;
                    r.minimumY = Mathf.Min(r.minimumY, cat.transform.position.y);
                    r.animation |= bones.Where((b, n) => Quaternion.Angle(rotations[n], b.localRotation) > .5f).Any();
                    r.camera &= Quaternion.Angle(rig.transform.rotation, rig.ViewRotation) < .1f;
                }
                Stage(choice.id + ":run-jump");
                cat.Teleport(P(8.1f, 3.4f)); yield return new WaitForSecondsRealtime(.2f);
                var origin = cat.transform.position;
                cat.VerificationRun = true; cat.VerificationMovement = Vector2.up;
                yield return new WaitForSeconds(.38f);
                cat.VerificationMovement = Vector2.zero; cat.VerificationRun = false;
                r.runMeters = Vector3.ProjectOnPlane(cat.transform.position - origin, Vector3.up).magnitude;
                r.running = r.runMeters > r.walkMeters * 1.45f;
                cat.Teleport(P(8.1f, 3.4f)); yield return new WaitForSecondsRealtime(.3f);
                float floor = cat.transform.position.y, peak = floor;
                cat.RequestJump(); float end = Time.time + 1.2f;
                while (Time.time < end) { yield return null; peak = Mathf.Max(peak, cat.transform.position.y); }
                r.jumpMeters = peak - floor;
                r.jumping = r.jumpMeters > .35f && Mathf.Abs(cat.transform.position.y - floor) < .045f && cat.GetComponent<CharacterController>().isGrounded;
                yield return Capture("cat-" + choice.id);
                Stage(choice.id + ":collisions");
                rig.yaw = -90; cat.Teleport(P(9.35f, 2.18f)); yield return new WaitForSecondsRealtime(.2f);
                cat.VerificationMovement = Vector2.up;
                yield return new WaitForSeconds(1);
                cat.VerificationMovement = Vector2.zero;
                r.wall = -cat.transform.position.x < 9.72f && !string.IsNullOrEmpty(cat.MovementBlocker);
                rig.yaw = 90; cat.Teleport(P(5.98f, 3.8f)); yield return new WaitForSecondsRealtime(.2f);
                cat.VerificationMovement = Vector2.up;
                yield return new WaitForSeconds(1);
                cat.VerificationMovement = Vector2.zero;
                r.furniture = -cat.transform.position.x > 5.6f && !string.IsNullOrEmpty(cat.MovementBlocker);
                cat.Teleport(P(8.1f, 3.4f)); yield return new WaitForSecondsRealtime(.2f);
                ContentPreviewMenu.Instance.Open(); origin = cat.transform.position; cat.VerificationMovement = Vector2.up;
                yield return new WaitForSecondsRealtime(.2f);
                r.pause = cat.Paused && Vector3.Distance(origin, cat.transform.position) < .001f;
                cat.VerificationMovement = Vector2.zero; ContentPreviewMenu.Instance.Close();
                gameplay.SetPreview(true); yield return new WaitForSecondsRealtime(.15f);
                r.preview = cat.Paused && gameplay.preview.viewCamera.isActiveAndEnabled;
                gameplay.SetPreview(false); yield return new WaitForSecondsRealtime(.15f);
                r.preview &= !cat.Paused && rig.isActiveAndEnabled && !gameplay.preview.viewCamera.isActiveAndEnabled;
                if (results.Count == 1)
                {
                    Stage(choice.id + ":room-route");
                    yield return Route(r);
                }
                Stage(choice.id + ":replay");
                cat.session.Replay(); yield return new WaitForSecondsRealtime(.5f);
                var reloaded = FindFirstObjectByType<CatController>();
                r.replay = reloaded && reloaded.animationDriver && reloaded.animationDriver.CatId == choice.id &&
                    FindFirstObjectByType<NewHouseExploration>() && !reloaded.Paused;
                ContentPreviewMenu.Instance.ReturnToStart(); yield return new WaitForSecondsRealtime(.35f);
                Write();
            }
            report.passed = results.Count == 3 && results.All(r => r.selectedCat && r.movement && r.running && r.jumping && r.animation &&
                r.camera && r.wall && r.furniture && r.pause && r.preview && r.replay && r.minimumY > -.04f) &&
                results[0].visitedRooms.Length == 8 && string.IsNullOrEmpty(results[0].routeBlocker) && errors.Count == 0;
            Stage("complete"); Finish();
        }

        IEnumerator Route(CatResult r)
        {
            var route = new[] {
                P(7.55f,4.25f), P(7.55f,5.62f), P(8.02f,5.62f), P(8.02f,6.35f), P(8.02f,5.62f),
                P(7.45f,5.62f),P(7.45f,5.33f),P(5.75f,5.33f),P(4.55f,5.33f),P(3.6f,5.33f),P(3.6f,4.5f),P(3.6f,3.55f),
                P(3.6f,4.5f),P(3.6f,5.575f),P(2.75f,5.575f),P(3.6f,5.575f),P(3.6f,7.25f),P(3.6f,7.62f),
                P(4.7f,7.62f),P(3.6f,7.62f),P(3.6f,5.33f),P(4.55f,5.33f),P(5.75f,5.33f),P(7.45f,5.33f),
                P(7.55f,4.25f),P(7.55f,2.35f),P(7.05f,2.35f),P(7.05f,1.1f)
            };
            var visited = new HashSet<string>(); var trace = new List<string> { "targetX,targetZ,reachedX,reachedZ,room,blocker" };
            cat.Teleport(cat.session.definition.spawnPosition); yield return new WaitForSecondsRealtime(.2f);
            cat.VerificationRun = true;
            foreach (var point in route)
            {
                float remaining = Vector3.ProjectOnPlane(point - cat.transform.position, Vector3.up).magnitude;
                float until = Time.time + remaining / cat.runSpeed * 1.8f + 2;
                while ((remaining = Vector3.ProjectOnPlane(point - cat.transform.position, Vector3.up).magnitude) > .035f && Time.time < until)
                {
                    var delta = Vector3.ProjectOnPlane(point - cat.transform.position, Vector3.up);
                    rig.yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                    cat.VerificationMovement = Vector2.up * Mathf.Clamp01(remaining / (cat.runSpeed * Mathf.Max(.001f, Time.deltaTime)));
                    yield return null;
                    r.minimumY = Mathf.Min(r.minimumY, cat.transform.position.y);
                }
                cat.VerificationMovement = Vector2.zero;
                string room = cat.session.definition.LocationAt(cat.transform.position);
                trace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F3},{1:F3},{2:F3},{3:F3},{4},{5}", point.x, point.z, cat.transform.position.x, cat.transform.position.z, room, cat.MovementBlocker));
                if (remaining > .06f)
                {
                    r.routeBlocker = point + " blocked by " + cat.MovementBlocker + " at " + cat.transform.position;
                    yield return Capture("route-blocked"); break;
                }
                visited.Add(room);
            }
            cat.VerificationRun = false; r.visitedRooms = visited.ToArray();
            File.WriteAllLines(Path.Combine(output, "room-route.csv"), trace);
            yield return Capture("room-route-end");
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return new WaitForSecondsRealtime(.15f);
        }
        void Stage(string value) { report.stage = value; Write(); }
        void Write()
        {
            report.cats = results.ToArray(); report.errors = errors.ToArray();
            File.WriteAllText(Path.Combine(output, "controls-check.json"), JsonUtility.ToJson(report, true));
        }
        void Finish() { CatCatalog.Select(selected, false); Write(); Application.Quit(report.passed ? 0 : 2); }
    }
}
