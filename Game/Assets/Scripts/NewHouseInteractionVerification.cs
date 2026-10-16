using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatSimulator
{
    // Opt-in tests run the rendered standalone player and the actual controller/physics loop.
    public sealed class NewHouseInteractionVerification : MonoBehaviour
    {
        [Serializable] class DoorResult
        {
            public string name;
            public int toggles, obstructions, propObstructions;
            public float propFinalSpeed, propMaxSpeed, propMaxHeight;
            public float pausedDoorDelta, pausedBallMeters, resumedDoorDelta, resumedBallMeters;
            public bool repeated, spamGuard, smooth, idleStable, paused, previewPaused, catSafe, propSafe, blocksClosed, passesOpen, interactionPrompt;
            public bool obstructionTested, passageTested, windowInteraction;
            public float endpointError, maxStepMeters, maxStepDegrees;
        }
        [Serializable] class BallResult
        {
            public string cat;
            public bool pushed, rolled, settled, bounded;
            public float displacement, rotationDegrees, maxSpeed, minimumY = 100, finalSpeed;
        }
        [Serializable] class Report
        {
            public bool passed, replay, returnToMenu;
            public string stage, gpu;
            public DoorResult[] doors;
            public BallResult[] balls;
            public string[] errors;
        }
        readonly Report report = new Report();
        readonly List<DoorResult> doors = new List<DoorResult>();
        readonly List<BallResult> balls = new List<BallResult>();
        readonly List<string> errors = new List<string>();
        readonly List<string> trace = new List<string> { "door,time,progress,x,y,z,yaw" };
        CatController cat;
        CatCamera cameraRig;
        NewHouseExploration exploration;
        NewHouseBall ball;
        Rigidbody ballBody;
        string output, savedCat;
        float began;
        bool finished;
        static Vector3 P(float x, float y, float z = .035f) => new Vector3(-x, z, -y);
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        void Update()
        { if (!finished && output != null && Time.realtimeSinceStartup - began > 240) { errors.Add("Timeout: " + report.stage); Finish(); } }

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--verification-output");
            output = Path.GetFullPath(at >= 0 ? args[at + 1] : Path.Combine(Application.persistentDataPath, "NewHouseInteractions"));
            Directory.CreateDirectory(output); began = Time.realtimeSinceStartup; savedCat = CatCatalog.Selected.id;
            report.gpu = SystemInfo.graphicsDeviceName;
            yield return new WaitForSecondsRealtime(.4f);
            foreach (var choice in CatCatalog.Current.cats)
            {
                CatCatalog.Select(choice.id, false); ContentPreviewMenu.Instance.LoadLevel(2);
                yield return new WaitForSeconds(.6f); Connect();
                Stage("ball:" + choice.id);
                var r = new BallResult { cat = choice.id }; balls.Add(r);
                cat.Teleport(P(8.28f, 3.96f)); cameraRig.yaw = 180; ball.ResetPose();
                yield return new WaitForSeconds(.2f);
                var start = ballBody.position; var rotation = ballBody.rotation;
                cat.VerificationMovement = Vector2.up;
                float until = Time.time + .55f / cat.walkSpeed;
                while (Time.time < until) { Sample(r); yield return null; }
                cat.VerificationMovement = Vector2.zero;
                r.displacement = Vector3.ProjectOnPlane(ballBody.position - start, Vector3.up).magnitude;
                r.rotationDegrees = Quaternion.Angle(rotation, ballBody.rotation);
                r.pushed = r.displacement > .05f; r.rolled = r.rotationDegrees > 5;
                yield return Capture("linen-ball-" + choice.id);
                cat.Teleport(P(7.55f, 2.35f));
                until = Time.time + 6;
                while (Time.time < until) { Sample(r); yield return null; }
                r.finalSpeed = ballBody.linearVelocity.magnitude;
                r.settled = r.finalSpeed < .035f;
                r.bounded = r.minimumY > .025f && r.maxSpeed < 4 && float.IsFinite(r.displacement);
                Write();
            }
            // Fresh scene for deterministic door start states after all three real cat contacts.
            CatCatalog.Select(CatCatalog.Current.cats[0].id, false); ContentPreviewMenu.Instance.LoadLevel(2);
            yield return new WaitForSeconds(.5f); Connect();
            var all = FindObjectsByType<NewHouseDoor>(FindObjectsSortMode.None).OrderBy(d => d.displayName).ToArray();
            foreach (var door in all)
            {
                var r = new DoorResult { name = door.displayName, repeated = true, spamGuard = true, smooth = true, idleStable = true,
                    paused = true, previewPaused = true, catSafe = true, propSafe = true, blocksClosed = true, passesOpen = true, interactionPrompt = true };
                doors.Add(r); Stage("cycles:" + door.displayName); cat.Teleport(P(8.1f, 3.4f));
                for (int cycle = 0; cycle < 4; cycle++) yield return ToggleAndMeasure(door, r, cycle == 0);
                if (r.name == "主卧门")
                {
                    Stage("pause-and-preview");
                    door.RequestToggle();
                    // UI input runs after this frame's fixed-step catch-up. Do not pause from a
                    // WaitForFixedUpdate continuation while further physics steps are queued.
                    while (door.IsMoving && door.Progress > .8f) yield return null;
                    ballBody.linearVelocity = Vector3.right * .5f; ballBody.WakeUp();
                    yield return null;
                    ContentPreviewMenu.Instance.Open(); float progress = door.Progress; var position = ballBody.position;
                    yield return new WaitForSecondsRealtime(.25f);
                    r.pausedDoorDelta = Mathf.Abs(door.Progress - progress);
                    r.pausedBallMeters = Vector3.Distance(position, ballBody.position);
                    r.paused = Time.timeScale == 0 && cat.Paused && r.pausedDoorDelta == 0 && r.pausedBallMeters < 1e-6f;
                    ContentPreviewMenu.Instance.Close();
                    yield return new WaitForSeconds(.15f);
                    r.resumedDoorDelta = Mathf.Abs(door.Progress - progress);
                    r.resumedBallMeters = Vector3.Distance(position, ballBody.position);
                    r.paused &= r.resumedDoorDelta > .0001f && r.resumedBallMeters > .001f;
                    exploration.SetPreview(true); progress = door.Progress; position = ballBody.position;
                    yield return new WaitForSecondsRealtime(.25f);
                    r.previewPaused = door.Progress == progress && Vector3.Distance(position, ballBody.position) < 1e-6f;
                    exploration.SetPreview(false); yield return Wait(door);
                    if (!door.IsOpen) yield return ToggleAndMeasure(door, r, false);
                }
                if (!r.name.Contains("窗户"))
                {
                    r.obstructionTested = r.passageTested = true;
                    Stage("obstruction:" + door.displayName);
                    if (!door.IsOpen) yield return ToggleAndMeasure(door, r, false);
                    var panel = door.panels[0]; var center = panel.closedPosition + panel.closedRotation * panel.shape.center;
                    cat.Teleport(new Vector3(center.x, .035f, center.z)); yield return new WaitForSeconds(.1f);
                    int prior = door.ObstructionCount;
                    door.RequestToggle(); yield return Wait(door);
                    r.catSafe = door.ObstructionCount > prior && door.IsOpen && cat.transform.position.y > -.04f;
                    r.obstructions = door.ObstructionCount;
                    cat.Teleport(P(8.1f, 3.4f)); yield return new WaitForSeconds(.1f);
                    ballBody.position = new Vector3(center.x, .08f, center.z);
                    ballBody.linearVelocity = ballBody.angularVelocity = Vector3.zero; ballBody.Sleep();
                    yield return new WaitForSeconds(.3f);
                    prior = door.ObstructionCount;
                    door.RequestToggle();
                    float propTimeout = Time.time + door.duration * 3 + 1;
                    while (door.IsMoving && Time.time < propTimeout)
                    {
                        r.propMaxSpeed = Mathf.Max(r.propMaxSpeed, ballBody.linearVelocity.magnitude);
                        r.propMaxHeight = Mathf.Max(r.propMaxHeight, ballBody.position.y);
                        yield return null;
                    }
                    yield return new WaitForSeconds(.08f);
                    r.propObstructions = door.ObstructionCount - prior; r.propFinalSpeed = ballBody.linearVelocity.magnitude;
                    r.propSafe = r.propObstructions > 0 && door.IsOpen && r.propFinalSpeed < .5f && r.propMaxSpeed < 1 && r.propMaxHeight < .15f;
                    ball.ResetPose();
                    Stage("passage:" + door.displayName);
                    if (door.IsOpen) yield return ToggleAndMeasure(door, r, false);
                    var normal = r.name == "主卧门" || r.name == "阳台移门" ? Vector3.back :
                        r.name == "书房门" ? Vector3.left : r.name == "卫生间移门" ? Vector3.right : Vector3.forward;
                    var start = new Vector3(door.Anchor.position.x, .035f, door.Anchor.position.z) + normal * .68f;
                    var end = new Vector3(door.Anchor.position.x, .035f, door.Anchor.position.z) - normal * .28f;
                    cat.Teleport(start); cameraRig.yaw = Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg;
                    yield return new WaitForSeconds(.2f);
                    cat.VerificationMovement = Vector2.up; yield return new WaitForSeconds(1.1f); cat.VerificationMovement = Vector2.zero;
                    r.blocksClosed = Vector3.Dot(cat.transform.position - door.Anchor.position, normal) > .08f && !string.IsNullOrEmpty(cat.MovementBlocker);
                    yield return new WaitForSeconds(.1f);
                    r.interactionPrompt = cat.Nearest == door && door.prompt.StartsWith("打开");
                    cat.BeginInteraction(door); yield return Wait(door); yield return null;
                    float until = Time.time + 3;
                    while (Time.time < until && Vector3.ProjectOnPlane(end - cat.transform.position, Vector3.up).magnitude > .05f)
                    {
                        var delta = Vector3.ProjectOnPlane(end - cat.transform.position, Vector3.up);
                        cameraRig.yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                        cat.VerificationMovement = Vector2.up * Mathf.Clamp01(delta.magnitude / (cat.walkSpeed * Mathf.Max(.001f, Time.deltaTime)));
                        yield return null;
                    }
                    cat.VerificationMovement = Vector2.zero;
                    r.passesOpen = Vector3.ProjectOnPlane(end - cat.transform.position, Vector3.up).magnitude < .08f && !cat.IsBusy;
                    yield return Capture("open-" + door.displayName);
                }
                else
                {
                    Stage("window-interaction:" + door.displayName);
                    cat.Teleport(new Vector3(door.Anchor.position.x, .035f, door.Anchor.position.z));
                    var delta = door.panels[0].closedPosition - cat.transform.position;
                    cameraRig.yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                    yield return new WaitForSeconds(.2f);
                    r.interactionPrompt = cat.Nearest == door && door.prompt.StartsWith("打开");
                    cat.BeginInteraction(door); yield return Wait(door);
                    r.windowInteraction = door.IsOpen && !cat.IsBusy;
                    yield return Capture("open-" + door.displayName);
                    cat.BeginInteraction(door); yield return Wait(door);
                    r.windowInteraction &= !door.IsOpen && !cat.IsBusy;
                }
                Write();
            }
            Stage("scene-reload"); cat.session.Replay(); yield return new WaitForSeconds(.5f); Connect();
            report.replay = FindObjectsByType<NewHouseDoor>(FindObjectsSortMode.None).Length == 8 && ball && !cat.IsBusy && !cat.Paused && Time.timeScale == 1;
            ContentPreviewMenu.Instance.ReturnToStart(); yield return new WaitForSecondsRealtime(.3f);
            report.returnToMenu = !FindFirstObjectByType<NewHouseDoor>() && !FindFirstObjectByType<NewHouseBall>() && Time.timeScale == 1;
            report.passed = errors.Count == 0 && report.replay && report.returnToMenu && balls.Count == 3 && balls.All(r => r.pushed && r.rolled && r.settled && r.bounded) &&
                doors.Count == 8 && doors.All(r => r.repeated && r.spamGuard && r.smooth && r.idleStable && r.paused && r.previewPaused && r.catSafe && r.propSafe && r.blocksClosed && r.passesOpen && r.interactionPrompt && r.endpointError < .0001f && (r.passageTested || r.windowInteraction));
            Stage("complete"); Finish();
        }
        void Connect()
        {
            cat = FindFirstObjectByType<CatController>(); cameraRig = FindFirstObjectByType<CatCamera>();
            exploration = FindFirstObjectByType<NewHouseExploration>(); ball = FindFirstObjectByType<NewHouseBall>(); ballBody = ball.GetComponent<Rigidbody>();
            cat.Automated = true; cameraRig.Automated = true;
        }
        void Sample(BallResult r)
        { r.minimumY = Mathf.Min(r.minimumY, ballBody.position.y); r.maxSpeed = Mathf.Max(r.maxSpeed, ballBody.linearVelocity.magnitude); }
        IEnumerator ToggleAndMeasure(NewHouseDoor door, DoorResult r, bool record)
        {
            bool opened = door.IsOpen; float previousProgress = door.Progress;
            r.repeated &= door.RequestToggle(); r.toggles++;
            for (int i = 0; i < 10; i++) r.spamGuard &= !door.RequestToggle();
            var positions = door.panels.Select(p => p.body.position).ToArray();
            var rotations = door.panels.Select(p => p.body.rotation).ToArray();
            float until = Time.time + door.duration + 2;
            while (door.IsMoving && Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                r.smooth &= opened ? door.Progress <= previousProgress + 1e-6f : door.Progress >= previousProgress - 1e-6f;
                r.smooth &= door.Progress >= 0 && door.Progress <= 1;
                previousProgress = door.Progress;
                for (int i = 0; i < door.panels.Length; i++)
                {
                    var p = door.panels[i];
                    float step = Vector3.Distance(positions[i], p.body.position), angle = Quaternion.Angle(rotations[i], p.body.rotation);
                    r.maxStepMeters = Mathf.Max(r.maxStepMeters, step); r.maxStepDegrees = Mathf.Max(r.maxStepDegrees, angle);
                    r.smooth &= step <= Vector3.Distance(p.closedPosition, p.openPosition) * .04f + .001f && angle <= Quaternion.Angle(p.closedRotation, p.openRotation) * .04f + .01f;
                    positions[i] = p.body.position; rotations[i] = p.body.rotation;
                    if (record) trace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1:F4},{2:F6},{3:F6},{4:F6},{5:F6},{6:F6}", r.name, Time.time, door.Progress, p.body.position.x, p.body.position.y, p.body.position.z, p.body.rotation.eulerAngles.y));
                }
            }
            r.repeated &= !door.IsMoving && door.IsOpen != opened;
            yield return new WaitForSeconds(.06f);
            foreach (var p in door.panels)
            {
                r.endpointError = Mathf.Max(r.endpointError, Vector3.Distance(p.body.position, door.IsOpen ? p.openPosition : p.closedPosition));
                r.endpointError = Mathf.Max(r.endpointError, Quaternion.Angle(p.body.rotation, door.IsOpen ? p.openRotation : p.closedRotation) / 180);
            }
            positions = door.panels.Select(p => p.body.position).ToArray(); rotations = door.panels.Select(p => p.body.rotation).ToArray();
            yield return new WaitForSeconds(.12f);
            for (int i = 0; i < door.panels.Length; i++)
                r.idleStable &= Vector3.Distance(positions[i], door.panels[i].body.position) < 1e-6f && Quaternion.Angle(rotations[i], door.panels[i].body.rotation) < .001f;
        }
        IEnumerator Wait(NewHouseDoor door)
        {
            float until = Time.time + door.duration * 3 + 1;
            while (door.IsMoving && Time.time < until) yield return null;
            if (door.IsMoving) errors.Add("Door timed out: " + door.displayName);
            yield return new WaitForSeconds(.08f);
        }
        IEnumerator Capture(string name)
        { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return null; }
        void Stage(string stage) { report.stage = stage; Debug.Log("NEW_HOUSE_INTERACTION_CHECK " + stage); Write(); }
        void Write()
        {
            report.doors = doors.ToArray(); report.balls = balls.ToArray(); report.errors = errors.ToArray();
            File.WriteAllText(Path.Combine(output, "validation-summary.json"), JsonUtility.ToJson(report, true));
            File.WriteAllLines(Path.Combine(output, "door-motion.csv"), trace);
        }
        void Finish()
        {
            if (finished) return; finished = true; Time.timeScale = 1;
            if (!string.IsNullOrEmpty(savedCat)) CatCatalog.Select(savedCat, false);
            Write(); Application.Quit(report.passed ? 0 : 2);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Requested()
        {
            if (!Environment.GetCommandLineArgs().Contains("--check-new-house-interactions")) return;
            var go = new GameObject("NewHouseInteractionVerification"); DontDestroyOnLoad(go); go.AddComponent<NewHouseInteractionVerification>();
        }
    }
}
