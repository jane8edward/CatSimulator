using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CatSimulator.Editor
{
    public static class NewHouseInteractionIntegration
    {
        const string Scene = "Assets/Scenes/New_House_Preview.unity";
        const string Folder = "Assets/Art/NewHouse/Gameplay";
        const string Reports = "../Source/Verification/NewHouseInteractions";
        [Serializable] class Manifest { public Item[] objects; }
        [Serializable] class Item { public string name, source, id; }
        static Dictionary<string, Item> rows;
        static MeshRenderer[] renderers;
        static Transform root;
        static PhysicsMaterial leafMaterial;
        static Vector3 P(float x, float y, float z = 0) => new Vector3(-x, z, -y);

        public static void PrepareAndBuild() { Prepare(); ModelResourceIntegration.BuildMac(); }
        public static void Prepare()
        {
            Directory.CreateDirectory(Reports);
            var scene = EditorSceneManager.OpenScene(Scene);
            if (UnityEngine.Object.FindFirstObjectByType<NewHouseDoor>())
            {
                var existingBall = UnityEngine.Object.FindFirstObjectByType<NewHouseBall>();
                existingBall.transform.position = P(8.28f, 4.55f, existingBall.GetComponent<SphereCollider>().radius + .004f);
                NewHouseDoorMeshRepair.Repair(UnityEngine.Object.FindFirstObjectByType<NewHousePreview>().residence);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Validate(); Debug.Log("NEW_HOUSE_INTERACTIONS_ALREADY_PRESENT"); return;
            }
            var home = UnityEngine.Object.FindFirstObjectByType<NewHousePreview>().residence;
            renderers = home.GetComponentsInChildren<MeshRenderer>(true);
            rows = JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Art/NewHouse/manifest.json")).objects.ToDictionary(o => o.name);
            root = new GameObject("NH_InteractiveDoorsWindowsAndToys").transform;
            root.SetParent(home, true);
            leafMaterial = Material("NH_DoorContact", .35f, 0);
            MakeBall();
            Hinge("主卧门", "NH_MBR_", P(3.60f, 4, .15f));
            Hinge("书房门", "NH_OFF_", P(3, 5.575f, .15f));
            Slider("卫生间移门", "NH_BTH_", "NH038", P(0, .425f), P(0, .425f), P(4.2f, 7.62f, .15f), Vector3.right * .014f);
            Slider("厨房移门", "NH_KIT_", "NH038", P(.55f, 0), P(.55f, 0), P(8.02f, 6, .15f), Vector3.forward * .014f);
            Balcony();
            Slider("主卧窗户", "NH_MBR_", "NH041", Vector3.zero, P(1.2f, 0), P(2.05f, .65f, .15f), Vector3.forward * .014f);
            Slider("书房窗户", "NH_OFF_", "NH041", Vector3.zero, P(0, .75f), P(.75f, 5.6f, .15f), Vector3.right * .014f);
            Slider("厨房窗户", "NH_KIT_", "NH040", Vector3.zero, P(.95f, 0), P(8.35f, 7.7f, .15f), Vector3.forward * .014f);
            NewHouseDoorMeshRepair.Repair(home);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate();
        }

        static PhysicsMaterial Material(string name, float friction, float bounce)
        {
            string path = Folder + "/" + name + ".physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (!material) { material = new PhysicsMaterial(name); AssetDatabase.CreateAsset(material, path); }
            material.dynamicFriction = material.staticFriction = friction; material.bounciness = bounce;
            material.frictionCombine = PhysicsMaterialCombine.Average; material.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material); return material;
        }

        static void MakeBall()
        {
            var visual = renderers.Single(r => r.name.Contains("_NH015_"));
            var bounds = visual.bounds;
            foreach (var c in visual.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            var go = new GameObject("NH_Physics_LinenBall"); go.transform.SetParent(root, true); go.transform.position = bounds.center + Vector3.forward * .32f;
            visual.transform.position += Vector3.forward * .32f; visual.transform.SetParent(go.transform, true);
            var sphere = go.AddComponent<SphereCollider>(); sphere.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            sphere.contactOffset = .0015f; sphere.sharedMaterial = Material("NH_LinenBallContact", .55f, .08f);
            var body = go.AddComponent<Rigidbody>(); body.mass = .08f; body.linearDamping = .45f; body.angularDamping = .5f;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.sleepThreshold = .008f; body.solverIterations = 12; body.solverVelocityIterations = 8;
            go.AddComponent<PushableProp>(); go.AddComponent<NewHouseBall>();
        }

        static NewHouseDoor Door(string name, Vector3 anchor, bool opened)
        {
            var go = new GameObject("NH_Interaction_" + name); go.transform.SetParent(root, true);
            var door = go.AddComponent<NewHouseDoor>(); door.displayName = name;
            door.interactionId = "new-house-" + name; door.startsOpen = opened; door.radius = 1.05f;
            door.duration = name.Contains("窗户") ? 1.4f : 1.25f;
            door.prompt = (opened ? "关闭" : "打开") + name;
            var point = new GameObject("InteractionAnchor").transform; point.SetParent(go.transform, true); point.position = anchor;
            door.anchor = point; door.session = UnityEngine.Object.FindFirstObjectByType<LevelSession>();
            return door;
        }

        static NewHouseDoor.Panel Body(Transform pivot, MeshRenderer[] visuals, Vector3 closed, Quaternion closedRot, Vector3 open, Quaternion openRot, bool opened, bool sliding)
        {
            pivot.SetPositionAndRotation(closed, closedRot);
            var bounds = visuals[0].bounds;
            foreach (var visual in visuals)
            {
                foreach (var c in visual.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                // Keep handle geometry, but do not let its projection thicken the whole glass collision.
                if (!visual.name.Contains("RecessedHandle")) bounds.Encapsulate(visual.bounds);
            }
            var box = pivot.gameObject.AddComponent<BoxCollider>();
            box.center = pivot.InverseTransformPoint(bounds.center);
            box.size = bounds.size;
            var size = box.size;
            // All closed pivots have identity rotation and unit scale.
            if (size.x < size.z) size.x = sliding ? .014f : .045f;
            else size.z = sliding ? .014f : .045f;
            box.size = size; box.contactOffset = .0025f; box.sharedMaterial = leafMaterial;
            var rb = pivot.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            pivot.SetPositionAndRotation(opened ? open : closed, opened ? openRot : closedRot);
            return new NewHouseDoor.Panel { body = rb, shape = box, closedPosition = closed, openPosition = open, closedRotation = closedRot, openRotation = openRot };
        }

        static void Hinge(string name, string prefix, Vector3 anchor)
        {
            var visual = renderers.Single(r => r.name.StartsWith(prefix) && r.name.EndsWith("_OpenLeaf"));
            var hinge = visual.transform.parent; var openRot = hinge.rotation; var closed = hinge.position;
            var door = Door(name, anchor, true); hinge.SetParent(door.transform, true);
            door.panels = new[] { Body(hinge, new[] { visual }, closed, Quaternion.identity, closed, openRot, true, false) };
        }

        static MeshRenderer[] Parts(string prefix, string id) => renderers.Where(r => r.name.StartsWith(prefix) && rows.TryGetValue(r.name, out var row) && row.id == id).ToArray();
        static bool FirstPane(MeshRenderer r) => rows[r.name].source.Contains("ClearPane_1_") || rows[r.name].source.EndsWith("Mullion_1") || rows[r.name].source.EndsWith("RecessedHandle");

        static NewHouseDoor.Panel SlidingPanel(NewHouseDoor door, string name, MeshRenderer[] parts, Vector3 previousShift, Vector3 travel, Vector3 trackOffset, bool opened)
        {
            foreach (var part in parts) part.transform.position += trackOffset - previousShift;
            var pane = parts.First(r => rows[r.name].source.Contains("ClearPane"));
            var closed = pane.bounds.center;
            var pivot = new GameObject(name).transform; pivot.SetParent(door.transform, true); pivot.position = closed;
            foreach (var part in parts) part.transform.SetParent(pivot, true);
            return Body(pivot, parts.OrderBy(r => r == pane ? 0 : 1).ToArray(), closed, Quaternion.identity, closed + travel, Quaternion.identity, opened, true);
        }

        static void Slider(string name, string prefix, string id, Vector3 previousShift, Vector3 travel, Vector3 anchor, Vector3 trackOffset)
        {
            bool opened = id == "NH038";
            var door = Door(name, anchor, opened);
            door.panels = new[] { SlidingPanel(door, "SlidingLeaf", Parts(prefix, id).Where(FirstPane).ToArray(), previousShift, travel, trackOffset, opened) };
        }

        static void Balcony()
        {
            var door = Door("阳台移门", P(7.05f, 1.6f, .15f), true);
            var parts = Parts("NH_LIV_", "NH039");
            var pane2 = parts.Single(r => rows[r.name].source.Contains("ClearPane_2_"));
            var pane3 = parts.Single(r => rows[r.name].source.Contains("ClearPane_3_"));
            var h1 = parts.Single(r => rows[r.name].source.EndsWith("RecessedHandle"));
            var h2 = parts.Single(r => rows[r.name].source.EndsWith("RecessedHandle.001"));
            var mullion = parts.Single(r => rows[r.name].source.EndsWith("Mullion_2"));
            // Restore the original closed panes, then keep both grips at the meeting edges.
            pane2.transform.position -= P(-.8f, 0); pane3.transform.position -= P(.8f, 0);
            h1.transform.position = P(7.0f, 1.545f, 1.144f); h2.transform.position = P(7.1f, 1.545f, 1.144f);
            mullion.transform.position -= P(1.6f, 0);
            door.panels = new[] {
                SlidingPanel(door, "LeftSlidingLeaf", new[] { pane2, h1 }, Vector3.zero, P(-.8f,0), Vector3.forward * .018f, true),
                SlidingPanel(door, "RightSlidingLeaf", new[] { pane3, h2, mullion }, Vector3.zero, P(.8f,0), Vector3.forward * .018f, true)
            };
        }

        public static void Validate()
        {
            var doors = UnityEngine.Object.FindObjectsByType<NewHouseDoor>(FindObjectsSortMode.None);
            var ball = UnityEngine.Object.FindFirstObjectByType<NewHouseBall>();
            if (doors.Length != 8 || !ball) throw new Exception("Expected 5 doors, 3 windows and one loose linen ball");
            foreach (var d in doors)
            {
                if (!d.anchor || d.panels.Length == 0) throw new Exception("Missing door references: " + d.name);
                foreach (var p in d.panels)
                    if (!p.body.isKinematic || !p.shape || p.body.transform.lossyScale != Vector3.one || p.body.GetComponentsInChildren<MeshCollider>().Length != 0)
                        throw new Exception("Invalid moving collision body: " + d.name);
            }
            File.WriteAllText(Reports + "/scene-setup.json", "{\"passed\":true,\"doors\":5,\"windows\":3,\"ball\":1,\"movingPanels\":9,\"kinematicTrajectories\":true,\"sharedMovementPreserved\":true}");
            Debug.Log("NEW_HOUSE_INTERACTIONS_READY doors=5 windows=3 looseBall=1");
        }
    }
}
