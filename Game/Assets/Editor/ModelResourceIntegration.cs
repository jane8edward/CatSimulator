using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

namespace CatSimulator.Editor
{
    // Imports new art into independent previews. Never rebuilds the shared playable scene.
    public static class ModelResourceIntegration
    {
        const string Home = "Assets/Art/CatHome90";
        const string Preview = "Assets/Scenes/Home90_Preview.unity";
        [Serializable] class Palette { public Entry[] materials; }
        [Serializable] class Entry { public string name, texture; public float[] color; public float roughness, metallic; }

        public static void Prepare()
        {
            var scenes = EditorBuildSettings.scenes;
            var oldQualityDistance = QualitySettings.shadowDistance;
            var oldQualityResolution = QualitySettings.shadowResolution;
            var oldRunInBackground = PlayerSettings.runInBackground;
            try
            {
                AssetDatabase.Refresh();
                ConvertWalkMaterials();
                CreateHomePreview();
                CatWalkAuthoring.Validate();
                AssetDatabase.SaveAssets();
                Validate();
            }
            finally
            {
                EditorBuildSettings.scenes = scenes;
                QualitySettings.shadowDistance = oldQualityDistance;
                QualitySettings.shadowResolution = oldQualityResolution;
                PlayerSettings.runInBackground = oldRunInBackground;
            }
        }

        static void ConvertWalkMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new Exception("Shared URP shader missing");
            foreach (var path in new[] {"Assets/Cat/Materials/Ground.mat", "Assets/Cat/Materials/Abyssinian_Coat.mat"})
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                var tex = m.mainTexture; var color = m.color; var scale = m.mainTextureScale;
                m.shader = shader; m.SetColor("_BaseColor", color); m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", scale); m.SetFloat("_Smoothness", .2f);
                m.SetFloat("_Metallic", 0); EditorUtility.SetDirty(m);
            }
        }

        static void CreateHomePreview()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Home + "/H90_Residence.fbx");
            importer.importAnimation = false; importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
            Directory.CreateDirectory(Home + "/Materials"); AssetDatabase.Refresh();
            var palette = JsonUtility.FromJson<Palette>(File.ReadAllText(Home + "/palette.json"));
            var materials = new Dictionary<string, Material>();
            foreach (var e in palette.materials)
            {
                var path = Home + "/Materials/" + e.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
                // Blender stores linear values; Unity material properties serialize sRGB color.
                mat.SetColor("_BaseColor", new Color(e.color[0], e.color[1], e.color[2], e.color[3]).gamma);
                mat.SetFloat("_Smoothness", 1 - e.roughness); mat.SetFloat("_Metallic", e.metallic);
                if (!string.IsNullOrEmpty(e.texture)) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(e.texture));
                // Glass is lightly translucent in the preview; native Cycles shading remains authoritative.
                if (e.name.EndsWith("Glass") || e.name.EndsWith("Water"))
                {
                    var c = mat.GetColor("_BaseColor"); c.a = .16f; mat.SetColor("_BaseColor", c);
                    mat.SetFloat("_Surface", 1); mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); mat.SetFloat("_ZWrite", 0);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000;
                }
                EditorUtility.SetDirty(mat); materials[e.name] = mat;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Home + "/H90_Residence.fbx");
            if (!source) throw new Exception("Home model failed to import");
            var room = (GameObject)PrefabUtility.InstantiatePrefab(source); room.name = "H90_Residence_Preview";
            var renderers = room.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
                r.sharedMaterials = r.sharedMaterials.Select(m => m && materials.TryGetValue(m.name, out var n) ? n : materials["H90_MAT_Wall"]).ToArray();
                if (r.name.Contains("Ceiling_") || r.name.Contains("Bulkhead") || r.name.Contains("Downlight")) r.gameObject.SetActive(false);
                if ((r.name.Contains("_Cell_") || r.name.Contains("BathTile_Upper")) && r.bounds.min.y > 1.14f && !r.name.Contains("outer_north") && !r.name.Contains("outer_east")) r.gameObject.SetActive(false);
            }
            var sun = new GameObject("H90_Sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.intensity = 1.5f; sun.transform.rotation = Quaternion.Euler(45, -30, 0); sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.8f,.85f,.9f);
            RenderSettings.ambientEquatorColor = new Color(.6f,.6f,.57f); RenderSettings.ambientGroundColor = new Color(.4f,.36f,.3f);
            var camera = new GameObject("H90_Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            var livingFloor = renderers.First(r => r.name.Contains("Floor_LIV_") && r.name.Contains("Subfloor"));
            var balconyFloor = renderers.First(r => r.name.Contains("Floor_BAL_") && r.name.Contains("Subfloor"));
            var bedroomFloor = renderers.First(r => r.name.Contains("Floor_MBR_") && r.name.Contains("Subfloor"));
            var south = Vector3.ProjectOnPlane(balconyFloor.bounds.center-livingFloor.bounds.center, Vector3.up).normalized;
            var west = Vector3.ProjectOnPlane(bedroomFloor.bounds.center-livingFloor.bounds.center, south).normalized;
            camera.transform.position = bounds.center + (south + west) * 10 + Vector3.up * 11;
            camera.transform.LookAt(bounds.center); camera.orthographic = true; camera.orthographicSize = 7.5f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 80; camera.backgroundColor = new Color(.85f,.85f,.82f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.gameObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, Preview);
        }

        public static void Validate()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LivingRoom.unity");
            if (!UnityEngine.Object.FindFirstObjectByType<CatController>() || !UnityEngine.Object.FindFirstObjectByType<LevelSession>())
                throw new Exception("Existing playable cat or level missing");
            EditorSceneManager.OpenScene(Preview);
            var room = GameObject.Find("H90_Residence_Preview");
            var renderers = room.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length < 800) throw new Exception("House export lost mesh objects");
            foreach (var r in renderers)
                if (!r.GetComponent<MeshFilter>().sharedMesh || r.sharedMaterials.Any(m => !m || m.shader.name.Contains("InternalError")))
                    throw new Exception("Invalid home mesh/material: " + r.name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatWalkAuthoring.PrefabPath);
            var animator = prefab.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.runtimeAnimatorController)
                throw new Exception("Walk avatar/controller missing");
            var guids = AssetDatabase.FindAssets("", new[]{"Assets/Cat", Home});
            var metas = guids.Select(AssetDatabase.GUIDToAssetPath).Where(p => !File.Exists(p + ".meta")).ToArray();
            if (metas.Length > 0) throw new Exception("Missing metadata: " + string.Join(",", metas));
            Directory.CreateDirectory("../Source/Verification/ModelResources");
            File.WriteAllText("../Source/Verification/ModelResources/unity-import.json",
                "{\"passed\":true,\"homeMeshRenderers\":" + renderers.Length + ",\"walkAvatarValid\":true,\"missingMeta\":0,\"existingPlayablePresent\":true}");
            Debug.Log("MODEL_RESOURCE_IMPORT_PASS: " + renderers.Length + " home renderers; Generic walk rig and existing level valid");
        }

        public static void BuildMac()
        {
            FullGameBuilder.EnsureAssets();
            Validate();
            Directory.CreateDirectory("../Builds/Mac");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = FullGameBuilder.ScenePaths,
                locationPathName = "../Builds/Mac/CatSimulator.app", target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development });
            File.WriteAllText("../Source/Verification/ModelResources/mac-build.json", "{\"result\":\"" + report.summary.result +
                "\",\"errors\":" + report.summary.totalErrors + ",\"bytes\":" + report.summary.totalSize + "}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Mac build failed");
        }
    }
}
