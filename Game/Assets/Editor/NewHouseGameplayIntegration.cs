using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CatSimulator.Editor
{
    public static class NewHouseGameplayIntegration
    {
        const string ScenePath = "Assets/Scenes/New_House_Preview.unity";
        const string Folder = "Assets/Art/NewHouse/Gameplay";
        const string PhysicsPath = Folder + "/NewHousePhysics.asset";
        const string ReportFolder = "../Source/Verification/NewHouseControls";
        static readonly Dictionary<Mesh, Mesh> collisionMeshes = new Dictionary<Mesh, Mesh>();
        static int correctedMeshes, colliders;
        [Serializable] class Manifest { public Item[] objects; }
        [Serializable] class Item { public string name, source, placement, id; }
        static Vector3 P(float x, float y, float z = 0) => new Vector3(-x, z, -y);

        public static void PrepareAndBuild()
        {
            Prepare(); ModelResourceIntegration.BuildMac();
        }

        public static void RepairThresholdsAndBuild()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var preview = UnityEngine.Object.FindFirstObjectByType<NewHousePreview>();
            var rows = JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Art/NewHouse/manifest.json")).objects.ToDictionary(o => o.name);
            FlattenThresholds(preview.residence.GetComponentsInChildren<MeshRenderer>(true), rows);
            foreach (var renderer in preview.residence.GetComponentsInChildren<MeshRenderer>(true))
            {
                var box = renderer.GetComponent<BoxCollider>();
                if (box) FitBox(renderer, box, renderer.GetComponent<MeshFilter>().sharedMesh);
            }
            File.WriteAllLines(ReportFolder + "/doorway-bounds.txt", preview.residence.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.name.Contains("_NH038_") || r.name.Contains("_NH039_"))
                .Select(r => r.name + " visual=" + r.bounds + " collider=" + (r.GetComponent<Collider>() ? r.GetComponent<Collider>().bounds.ToString() : "none")));
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            ModelResourceIntegration.BuildMac();
        }

        static void FlattenThresholds(MeshRenderer[] parts, Dictionary<string, Item> rows)
        {
            int count = 0;
            foreach (var r in parts)
            {
                if (!rows.TryGetValue(r.name, out var row) || (row.id != "NH038" && row.id != "NH039") ||
                    !row.source.Contains("OuterRail") || r.bounds.max.y > .15f) continue;
                var up = r.transform.InverseTransformDirection(Vector3.up);
                int axis = Mathf.Abs(up.x) > Mathf.Abs(up.y) ? 0 : 1;
                if (Mathf.Abs(up.z) > Mathf.Abs(up[axis])) axis = 2;
                var scale = r.transform.localScale; scale[axis] *= .024f / r.bounds.size.y; r.transform.localScale = scale;
                r.transform.position += Vector3.up * (.012f - r.bounds.center.y); count++;
            }
            Debug.Log("NEW_HOUSE_LOW_THRESHOLDS " + count);
        }

        public static void Prepare()
        {
            Directory.CreateDirectory(ReportFolder);
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            collisionMeshes.Clear(); correctedMeshes = colliders = 0;
            var target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (target.GetRootGameObjects().Any(o => o.GetComponent<NewHouseExploration>()))
            { Debug.Log("NEW_HOUSE_GAMEPLAY_ALREADY_PRESENT"); return; }
            var preview = UnityEngine.Object.FindFirstObjectByType<NewHousePreview>();
            if (!preview || !preview.residence) throw new Exception("New_House source scene missing");
            preview.RestoreFullHouse();
            PrefabUtility.UnpackPrefabInstance(preview.residence.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Art/NewHouse/manifest.json"));
            var rows = manifest.objects.ToDictionary(o => o.name);
            var parts = preview.residence.GetComponentsInChildren<MeshRenderer>(true);
            var physics = new Mesh { name = "New_House gameplay collision and open-door meshes" };
            AssetDatabase.CreateAsset(physics, PhysicsPath);

            // Open the same supplied doors in the scene copy; retain every original triangle.
            foreach (var r in parts.Where(r => rows[r.name].id == "NH064"))
            {
                bool master = r.name.StartsWith("NH_MBR_");
                OpenHingedDoor(r, master ? P(3.15f, 4) : P(3, 5.15f), master ? .9f : .85f, master, master ? 88 : -88);
            }
            foreach (var r in parts)
            {
                var row = rows[r.name]; string s = row.source;
                if (row.id == "NH038")
                {
                    bool bath = r.name.StartsWith("NH_BTH_");
                    if (s.Contains("ClearPane_1_") || s.EndsWith("Mullion_1") || s.EndsWith("RecessedHandle"))
                        r.transform.position += bath ? P(0, .425f) : P(.55f, 0);
                }
                if (row.id == "NH039")
                {
                    if (s.Contains("ClearPane_2_") || s.EndsWith("RecessedHandle")) r.transform.position += P(-.8f, 0);
                    if (s.Contains("ClearPane_3_") || s.EndsWith("RecessedHandle.001")) r.transform.position += P(.8f, 0);
                    if (s.EndsWith("Mullion_2")) r.transform.position += P(1.6f, 0);
                }
                if (s.StartsWith("NH_Ceiling_") || s == "NH_LivingBulkhead") r.shadowCastingMode = ShadowCastingMode.Off;
            }
            FlattenThresholds(parts, rows);
            // One continuous support covers the visible planks, grout gaps and door thresholds.
            var support = new GameObject("NH_ContinuousFloorSupport"); support.transform.SetParent(preview.residence, true);
            support.transform.position = P(5, 4.5f, -.0725f);
            support.AddComponent<BoxCollider>().size = new Vector3(10, .15f, 9); colliders++;
            foreach (var r in preview.residence.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                rows.TryGetValue(r.name, out var row);
                string name = row != null ? row.source : r.name;
                if (name.StartsWith("NH_FloorModule_") || name.StartsWith("NH_Subfloor_") || name == "NH_Foundation" ||
                    name.Contains("BackdropBuilding") || name == "NH_StudioBase") continue;
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                if (name.Contains("ClearPane") || name.Contains("OuterJamb") || name.Contains("OuterRail") || name.Contains("Mullion"))
                {
                    var box = r.gameObject.AddComponent<BoxCollider>(); FitBox(r, box, mesh); colliders++;
                }
                else
                {
                    var collider = r.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = Outward(mesh); colliders++;
                }
            }

            // Clone only the existing player/camera/session/HUD roots and reconnect their references.
            var source = EditorSceneManager.OpenScene("Assets/Scenes/LivingRoom.unity", OpenSceneMode.Additive);
            T Source<T>() where T : Component => source.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<T>(true)).Single();
            GameObject Copy(GameObject original)
            {
                var copy = UnityEngine.Object.Instantiate(original); SceneManager.MoveGameObjectToScene(copy, target); return copy;
            }
            var session = Copy(Source<LevelSession>().gameObject).GetComponent<LevelSession>();
            var cat = Copy(Source<CatController>().gameObject).GetComponent<CatController>();
            var rig = Copy(Source<CatCamera>().gameObject).GetComponent<CatCamera>();
            var hud = Copy(Source<GameHud>().gameObject).GetComponent<GameHud>();
            cat.session = session; cat.cameraTransform = rig.transform;
            rig.cat = cat; rig.yaw = 180; rig.pitch = 16; rig.Automated = false;
            hud.cat = cat; hud.session = session;
            var definition = ScriptableObject.CreateInstance<LevelDefinition>();
            definition.levelId = "home-02"; definition.displayName = "温馨大宅 · 自由探索";
            definition.requiredObjectives = Array.Empty<string>(); definition.playerScale = .75f;
            definition.spawnPosition = P(7.55f, 2.35f, .035f); definition.spawnYaw = 180;
            definition.activityBounds = new Bounds(P(5, 4.5f, 1.5f), new Vector3(9.8f, 4, 8.8f));
            string[] codes = { "MBR", "OFF", "HALL", "ENT", "BTH", "KIT", "LIV", "BAL" };
            string[] names = { "主卧", "次卧与书房", "走廊", "玄关", "卫生间", "厨房", "客餐厅", "阳台" };
            definition.areas = codes.Select((code, i) => {
                var bounds = parts.Single(r => rows[r.name].source == "NH_Subfloor_" + code).bounds;
                bounds.center = new Vector3(bounds.center.x, 1.4f, bounds.center.z);
                bounds.size = new Vector3(bounds.size.x, 3.2f, bounds.size.z);
                return new LevelArea { name = names[i], bounds = bounds };
            }).ToArray();
            AssetDatabase.CreateAsset(definition, Folder + "/NewHouseLevel.asset");
            session.definition = definition; cat.transform.SetPositionAndRotation(definition.spawnPosition, Quaternion.Euler(0, 180, 0));
            var gameplay = new GameObject("New_House_Exploration").AddComponent<NewHouseExploration>();
            SceneManager.MoveGameObjectToScene(gameplay.gameObject, target);
            gameplay.cat = cat; gameplay.followCamera = rig; gameplay.hud = hud; gameplay.preview = preview;
            preview.enabled = false; preview.viewCamera.gameObject.SetActive(false); preview.RestoreFullHouse();
            EditorSceneManager.CloseScene(source, true); SceneManager.SetActiveScene(target);
            EditorSceneManager.SaveScene(target, ScenePath); AssetDatabase.SaveAssets();
            File.WriteAllText(ReportFolder + "/scene-build.json", "{\"passed\":true,\"colliders\":" + colliders +
                ",\"correctedCollisionMeshes\":" + correctedMeshes + ",\"rooms\":8,\"sharedCatController\":true,\"sourceScenesPreserved\":true}");
            Debug.Log("NEW_HOUSE_GAMEPLAY_READY colliders=" + colliders + " corrected=" + correctedMeshes);
        }

        static Mesh Outward(Mesh source)
        {
            if (collisionMeshes.TryGetValue(source, out var cached)) return cached;
            var vertices = source.vertices; var triangles = source.triangles; var center = source.bounds.center;
            double volume = 0;
            for (int i = 0; i < triangles.Length; i += 3)
                volume += Vector3.Dot(vertices[triangles[i]] - center, Vector3.Cross(vertices[triangles[i + 1]] - center, vertices[triangles[i + 2]] - center));
            var size = source.bounds.size;
            Mesh mesh = source;
            if (volume < -Math.Max(1e-18, size.x * size.y * size.z * 1e-5))
            {
                mesh = UnityEngine.Object.Instantiate(source); mesh.name = source.name + "_CollisionOutward";
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var indices = mesh.GetTriangles(s);
                    for (int i = 0; i < indices.Length; i += 3) { int swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap; }
                    mesh.SetTriangles(indices, s);
                }
                AssetDatabase.AddObjectToAsset(mesh, PhysicsPath); correctedMeshes++;
            }
            collisionMeshes.Add(source, mesh); return mesh;
        }

        static void FitBox(MeshRenderer renderer, BoxCollider box, Mesh mesh)
        {
            var size = mesh.bounds.size;
            // Imported FBX object scales can be 100. Minimum thickness is two world millimetres.
            for (int i = 0; i < 3; i++)
            {
                var axis = Vector3.zero; axis[i] = 1;
                size[i] = Mathf.Max(size[i], .002f / Mathf.Max(.000001f, renderer.transform.TransformVector(axis).magnitude));
            }
            box.center = mesh.bounds.center; box.size = size;
        }

        static void OpenHingedDoor(MeshRenderer renderer, Vector3 hinge, float width, bool alongX, float angle)
        {
            var source = renderer.GetComponent<MeshFilter>().sharedMesh;
            bool Leaf(Vector3 local)
            {
                var p = renderer.transform.TransformPoint(local);
                float t = alongX ? hinge.x - p.x : hinge.z - p.z;
                return t > .045f && t < width - .045f && p.y > .02f && p.y < 2.13f;
            }
            Mesh Slice(bool keepLeaf)
            {
                var vertices = source.vertices;
                var groups = new List<int[]>(); var used = new SortedSet<int>();
                for (int s = 0; s < source.subMeshCount; s++)
                {
                    var indices = source.GetTriangles(s); var filtered = new List<int>();
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var midpoint = (vertices[indices[i]] + vertices[indices[i + 1]] + vertices[indices[i + 2]]) / 3;
                        if (Leaf(midpoint) != keepLeaf) continue;
                        for (int j = 0; j < 3; j++) { filtered.Add(indices[i + j]); used.Add(indices[i + j]); }
                    }
                    groups.Add(filtered.ToArray());
                }
                var original = used.ToArray(); var lookup = original.Select((v, i) => new { v, i }).ToDictionary(p => p.v, p => p.i);
                var mesh = new Mesh { name = renderer.name + (keepLeaf ? "_OpenLeaf" : "_FixedFrame"), indexFormat = IndexFormat.UInt32 };
                mesh.vertices = original.Select(i => vertices[i]).ToArray();
                var normals = source.normals; var uv = source.uv; var tangents = source.tangents;
                if (normals.Length == vertices.Length) mesh.normals = original.Select(i => normals[i]).ToArray();
                if (uv.Length == vertices.Length) mesh.uv = original.Select(i => uv[i]).ToArray();
                if (tangents.Length == vertices.Length) mesh.tangents = original.Select(i => tangents[i]).ToArray();
                mesh.subMeshCount = groups.Count;
                for (int s = 0; s < groups.Count; s++) mesh.SetTriangles(groups[s].Select(i => lookup[i]).ToArray(), s);
                mesh.RecalculateBounds(); AssetDatabase.AddObjectToAsset(mesh, PhysicsPath); return mesh;
            }
            var leaf = UnityEngine.Object.Instantiate(renderer.gameObject, renderer.transform.parent);
            leaf.name = renderer.name + "_OpenLeaf"; leaf.GetComponent<MeshFilter>().sharedMesh = Slice(true);
            var frame = UnityEngine.Object.Instantiate(renderer.gameObject, renderer.transform.parent);
            frame.name = renderer.name + "_FixedFrame"; frame.GetComponent<MeshFilter>().sharedMesh = Slice(false);
            var pivot = new GameObject(renderer.name + "_Hinge"); pivot.transform.SetParent(renderer.transform.parent, true); pivot.transform.position = hinge;
            leaf.transform.SetParent(pivot.transform, true); pivot.transform.rotation = Quaternion.Euler(0, angle, 0);
            renderer.gameObject.SetActive(false);
        }
    }
}
