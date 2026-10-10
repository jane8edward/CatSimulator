using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CatSimulator
{
    public sealed class BedroomPhysicsVerification : MonoBehaviour
    {
        [Serializable] public class CushionCheck
        {
            public string name;
            public bool deforms, collisionFollows, recovers;
            public float indentation, surfaceDrop, recoveryError;
        }
        [Serializable] public class Report
        {
            public string version, gpu, resolution;
            public bool passed, propsStable, catPushesMouse, lampTips, plantFallsAndSettles, phoneTetherWorks, replayResets;
            public Vector3 deskSize;
            public int dynamicProps;
            public float mouseTravel, lampAngle, plantFloorHeight, cableDistance, maximumCableDistance, phoneTravel;
            public float averageFps, p95FrameMs, p99FrameMs;
            public string[] missingDynamicCollisions, errors;
            public CushionCheck[] pillows;
        }
        static Report replayReport;
        static string replayOutput;
        readonly List<string> errors = new List<string>();
        CatController cat;
        CatCamera rig;
        GameObject room;
        string output;
        Report report;
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string text, string trace, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception) errors.Add(text);
        }

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--verify-bedroom-physics")) yield break;
            cat = FindFirstObjectByType<CatController>();
            rig = FindFirstObjectByType<CatCamera>();
            room = GameObject.Find("LivingRoom");
            cat.Automated = rig.Automated = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            var index = Array.IndexOf(args, "--verification-output");
            output = index >= 0 ? args[index + 1] : Path.Combine(Application.persistentDataPath, "BedroomPhysics");
            Directory.CreateDirectory(output);
            yield return new WaitForSeconds(3);
            VerificationRenderTarget.Initialize(rig.GetComponent<Camera>());
            var bodies = room.GetComponentsInChildren<PushableProp>().Select(p => p.GetComponent<Rigidbody>()).ToArray();
            var positions = bodies.Select(b => b.position).ToArray();
            if (replayReport != null)
            {
                report = replayReport;
                output = replayOutput;
                report.replayResets = bodies.All(b => b.GetComponentsInChildren<Collider>().All(c => c.attachedRigidbody == b))
                    && Mathf.Abs(Find("Bedroom_Prop_Plant").position.y - .905f) < .04f
                    && Mathf.Abs(Find("Bedroom_Prop_Phone").position.y - .703f) < .04f
                    && room.GetComponentsInChildren<SoftPillow>().All(p => p.CurrentIndentation < .002f);
                report.errors = report.errors.Concat(errors).ToArray();
                report.passed &= report.replayResets && report.errors.Length == 0;
                File.WriteAllText(Path.Combine(output, "bedroom-physics.json"), JsonUtility.ToJson(report, true));
                Application.Quit(report.passed ? 0 : 2);
                yield break;
            }
            report = new Report { version = Application.version, gpu = SystemInfo.graphicsDeviceName,
                resolution = "1920x1080", dynamicProps = bodies.Length,
                deskSize = Find("Bedroom_DeskTop").GetComponent<Renderer>().bounds.size };
            report.missingDynamicCollisions = bodies.Where(b => b.isKinematic || !b.useGravity
                || b.constraints != RigidbodyConstraints.None || b.GetComponentsInChildren<Collider>().Length == 0
                || b.GetComponentsInChildren<Collider>().Any(c => c.isTrigger || c.attachedRigidbody != b))
                .Select(b => b.name).ToArray();
            yield return new WaitForSeconds(1);
            report.propsStable = bodies.Select((b, i) => Vector3.Distance(b.position, positions[i])).All(d => d < .025f);
            var rotations = bodies.Select(b => b.rotation).ToArray();
            cat.Teleport(Find("BedroomPreviewCat").position);
            yield return View("desk-details", new Vector3(8.17f, 1.7f, .95f), new Vector3(8.48f, 1.03f, 2.35f), 60);
            yield return View("phone-charging", new Vector3(9.12f, 1.37f, -1.47f), new Vector3(8.40f, .76f, -2.35f), 49);

            var mouse = Find("Bedroom_Prop_Mouse").GetComponent<Rigidbody>();
            var mouseStart = mouse.position;
            // Exercise the actual controller, including its separate nose/rump collision cast.
            cat.Teleport(new Vector3(mouse.position.x - .43f, .85f, mouse.position.z + .01f));
            cat.transform.rotation = Quaternion.Euler(0, 90, 0);
            rig.enabled = true;
            rig.yaw = 90;
            cat.VerificationMovement = Vector2.up;
            yield return new WaitForSeconds(.9f);
            cat.VerificationMovement = Vector2.zero;
            report.mouseTravel = Vector3.Distance(mouse.position, mouseStart);
            report.catPushesMouse = report.mouseTravel > .03f;
            cat.Teleport(Find("BedroomPreviewCat").position);

            var lamp = Find("Bedroom_Prop_Lamp_Left").GetComponent<Rigidbody>();
            var lampStart = lamp.rotation;
            lamp.AddForceAtPosition(Vector3.forward * 1.1f, lamp.worldCenterOfMass + Vector3.up * .35f, ForceMode.Impulse);
            yield return new WaitForSeconds(1.5f);
            report.lampAngle = Quaternion.Angle(lamp.rotation, lampStart);
            report.lampTips = report.lampAngle > 15;
            yield return View("lamp-tipped", new Vector3(9.12f, 1.37f, -1.47f), new Vector3(8.40f, .76f, -2.20f), 52);

            var plant = Find("Bedroom_Prop_Plant").GetComponent<Rigidbody>();
            plant.position = new Vector3(6.0f, 1.40f, 1.30f);
            plant.linearVelocity = Vector3.zero;
            plant.angularVelocity = Vector3.zero;
            yield return new WaitForSeconds(2.5f);
            report.plantFloorHeight = plant.position.y;
            report.plantFallsAndSettles = plant.position.y < .15f && plant.position.y > -.06f
                && plant.linearVelocity.magnitude < .15f;

            var checks = new List<CushionCheck>();
            foreach (var pillow in room.GetComponentsInChildren<SoftPillow>())
            {
                var surface = pillow.GetComponent<MeshCollider>();
                var bounds = pillow.GetComponent<Renderer>().bounds;
                var ray = new Ray(new Vector3(bounds.center.x, bounds.max.y + .5f, bounds.center.z), Vector3.down);
                surface.Raycast(ray, out var restHit, 1);
                cat.Teleport(new Vector3(bounds.center.x, restHit.point.y + .022f, bounds.center.z));
                cat.transform.rotation = Quaternion.Euler(0, 180, 0);
                yield return new WaitForSeconds(1.3f);
                surface.Raycast(ray, out var loadedHit, 1);
                var check = new CushionCheck { name = pillow.name, indentation = pillow.CurrentIndentation,
                    surfaceDrop = restHit.point.y - loadedHit.point.y,
                    deforms = pillow.CurrentIndentation > .012f,
                    collisionFollows = pillow.CollisionIndentation > .01f && restHit.point.y - loadedHit.point.y > .008f };
                if (checks.Count == 0)
                {
                    yield return View("pillow-loaded", new Vector3(bounds.center.x + 1.0f, 1.7f, bounds.center.z + .84f),
                        new Vector3(bounds.center.x, .83f, bounds.center.z), 52);
                }
                cat.Teleport(Find("BedroomPreviewCat").position);
                yield return new WaitForSeconds(2.1f);
                surface.Raycast(ray, out var recoveredHit, 1);
                check.recoveryError = Mathf.Abs(restHit.point.y - recoveredHit.point.y);
                check.recovers = pillow.CurrentIndentation < .002f && check.recoveryError < .003f;
                checks.Add(check);
            }
            report.pillows = checks.ToArray();
            var phone = Find("Bedroom_Prop_Phone").GetComponent<Rigidbody>();
            var cable = room.GetComponentInChildren<ChargingCable>();
            var phoneStart = phone.position;
            phone.AddForce(Vector3.forward * .65f, ForceMode.Impulse);
            var cableTestEnd = Time.time + 2;
            while (Time.time < cableTestEnd)
            {
                yield return new WaitForFixedUpdate();
                report.maximumCableDistance = Mathf.Max(report.maximumCableDistance,
                    Vector3.Distance(cable.phoneAnchor.position, cable.wallAnchor.position));
            }
            report.cableDistance = Vector3.Distance(cable.phoneAnchor.position, cable.wallAnchor.position);
            report.phoneTravel = Vector3.Distance(phone.position, phoneStart);
            report.phoneTetherWorks = phone.GetComponent<ConfigurableJoint>() != null
                && report.phoneTravel > .10f && report.maximumCableDistance > cable.cableLength - .04f
                && report.maximumCableDistance <= cable.cableLength + .05f
                && Vector3.Distance(cable.GetComponent<LineRenderer>().GetPosition(27), cable.phoneAnchor.position) < .02f;

            for (var i = 0; i < bodies.Length; i++)
            {
                bodies[i].position = positions[i];
                bodies[i].rotation = rotations[i];
                bodies[i].linearVelocity = bodies[i].angularVelocity = Vector3.zero;
            }
            yield return new WaitForSeconds(1);
            cat.Teleport(Find("BedroomPreviewCat").position);
            yield return View("bedroom-physics-wide", new Vector3(8.94f, 2.35f, 2.25f), new Vector3(6.91f, .77f, -.65f), 65);
            // Include a loaded/deforming cushion and animated cat in the performance sample.
            var loaded = room.GetComponentsInChildren<SoftPillow>()[0].GetComponent<Renderer>().bounds;
            cat.Teleport(new Vector3(loaded.center.x, loaded.max.y + .02f, loaded.center.z));
            rig.enabled = true; rig.yaw = 180; rig.pitch = 24;
            yield return new WaitForSeconds(.5f);
            var durations = new List<float>();
            var begin = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - begin < 8)
            {
                rig.yaw += Time.unscaledDeltaTime * 25;
                durations.Add(Time.unscaledDeltaTime);
                yield return null;
            }
            durations.Sort();
            report.averageFps = durations.Count / durations.Sum();
            report.p95FrameMs = durations[(int)(durations.Count * .95f)] * 1000;
            report.p99FrameMs = durations[(int)(durations.Count * .99f)] * 1000;
            report.errors = errors.ToArray();
            report.passed = report.deskSize.x > 2.1f && report.deskSize.z > .8f && report.dynamicProps >= 14
                && report.propsStable && report.missingDynamicCollisions.Length == 0 && report.catPushesMouse
                && report.lampTips && report.plantFallsAndSettles && report.phoneTetherWorks && checks.Count == 2
                && checks.All(c => c.deforms && c.collisionFollows && c.recovers) && errors.Count == 0;
            replayReport = report;
            replayOutput = output;
            FindFirstObjectByType<LevelSession>().Replay();
        }

        Transform Find(string name) => room.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
        IEnumerator View(string filename, Vector3 eye, Vector3 target, float fov)
        {
            rig.enabled = false;
            var hud = FindFirstObjectByType<GameHud>().GetComponent<Canvas>();
            hud.enabled = false;
            var camera = rig.GetComponent<Camera>();
            camera.fieldOfView = fov;
            camera.transform.position = eye;
            camera.transform.LookAt(target);
            yield return new WaitForSeconds(.15f);
            yield return new WaitForEndOfFrame();
            var image = VerificationRenderTarget.Capture();
            if (image == null) { errors.Add("Capture failed: " + filename); yield break; }
            File.WriteAllBytes(Path.Combine(output, filename + ".png"), image.EncodeToPNG());
            Destroy(image);
            camera.fieldOfView = 53;
            hud.enabled = true;
        }
    }
}
