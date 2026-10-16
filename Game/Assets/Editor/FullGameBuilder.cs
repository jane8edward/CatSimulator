using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CatSimulator.Editor
{
    public static class FullGameBuilder
    {
        public const string StartPath = "Assets/Scenes/StartMenu.unity", HomePath = "Assets/Scenes/Home90.unity";
        public static string[] ScenePaths => new[] { StartPath, "Assets/Scenes/LivingRoom.unity", HomePath,
            "Assets/Cat/Scenes/Walk_Preview.unity", "Assets/Scenes/Home90_Preview.unity", "Assets/Scenes/New_House_Preview.unity" };
        const string CatalogPath = "Assets/Resources/Gameplay/CatCatalog.asset";
        public static void EnsureAssets()
        {
            if (!File.Exists(StartPath) || !File.Exists(HomePath) || !File.Exists(CatalogPath)) Setup();
        }
        public static void SetupAndBuild() { Setup(); FirstPlayableBuilder.Build(); }
        public static void UpdateControlsAndBuild()
        {
            var home=AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Generated/Home90Level.asset");
            if(!home)throw new InvalidOperationException("Home90 level is missing");
            home.playerScale=.75f;EditorUtility.SetDirty(home);
            PlayerSettings.bundleVersion="0.2.2";AssetDatabase.SaveAssets();
            FirstPlayableBuilder.Build();
        }
        public static void RepairHomeAndBuild()
        {
            CreateHome();
            PlayerSettings.bundleVersion = "0.2.2";
            AssetDatabase.SaveAssets();
            Home90PhysicsValidation.Validate();
            FirstPlayableBuilder.Build();
        }
        [MenuItem("Cat Simulator/Create Full Game Entry And Home90")]
        public static void Setup()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory("Assets/Resources/Gameplay"); AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<CatCatalog>(CatalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<CatCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.cats = new[] {
                new CatChoice {id="original",displayName="原版猫",description="原有待机、行走、奔跑与互动动作",visualPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(BlendSwapCatIntegration.Folder+"/AnimatedCat.prefab"),visualEuler=new Vector3(0,180,0),animationKind=CatAnimationKind.Original},
                new CatChoice {id="abyssinian",displayName="阿比尼西亚猫",description="独立行走动画；互动使用这只猫的姿态过渡",visualPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(CatWalkAuthoring.PrefabPath),visualScale=.62f,visualEuler=new Vector3(0,-.1557f,0),walkSpeed=.52f,runSpeed=1.05f,animationKind=CatAnimationKind.Abyssinian}
            };
            if (catalog.cats.Any(c => c.visualPrefab == null)) throw new Exception("A cat visual is missing");
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            CreateHome(); CreateStart();
            EditorBuildSettings.scenes = ScenePaths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            PlayerSettings.bundleVersion = "0.2.2"; PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
        }

        static void CreateHome()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Home90_Preview.unity");
            var preview = GameObject.Find("H90_Residence_Preview");
            var homePrefab = PrefabUtility.SaveAsPrefabAsset(preview, "Assets/Generated/Home90Visual.prefab");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/LivingRoom.unity");
            // Save a separate scene before modifying it. Original level and cat source stay intact.
            EditorSceneManager.SaveScene(scene, HomePath);
            foreach (var name in new[] {"LivingRoom","DevelopmentVerification","BedroomLighting","LivingRoomReflections","AfternoonSun","AmbientRoomLight"})
            { var old = GameObject.Find(name); if (old != null) UnityEngine.Object.DestroyImmediate(old); }
            var room = (GameObject)PrefabUtility.InstantiatePrefab(homePrefab); room.name = "Home90Residence";
            PrefabUtility.UnpackPrefabInstance(room,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var renderers = room.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers) { r.gameObject.SetActive(true); r.enabled = true; }
            Home90CollisionBuilder.ArrangeFurniture(renderers);
            OpenDoors(room, renderers);
            TrimSkirtingAtPassages(room, renderers);
            var collisionMeshes = new Home90CollisionBuilder();
            foreach (var r in renderers)
            {
                if (!r.gameObject.activeInHierarchy) continue;
                if (r.name.Contains("StudioGround")) { r.gameObject.SetActive(false); continue; }
                // Ceiling receives indoor light without blocking all first-pass sunlight.
                if (r.name.Contains("Ceiling_")) r.shadowCastingMode = ShadowCastingMode.Off;
                var mesh = r.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) continue;
                collisionMeshes.AddCollision(r, mesh.sharedMesh);
            }
            Home90CollisionBuilder.AddSofaSupport(room,renderers);
            collisionMeshes.SaveReport();
            var session = UnityEngine.Object.FindFirstObjectByType<LevelSession>();
            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Generated/Home90Level.asset");
            if (!definition) { definition = ScriptableObject.CreateInstance<LevelDefinition>(); AssetDatabase.CreateAsset(definition,"Assets/Generated/Home90Level.asset"); }
            definition.levelId = "home90-02"; definition.displayName = "90㎡住宅 · 自由探索";
            definition.requiredObjectives = Array.Empty<string>();
            definition.playerScale=.75f;
            definition.activityBounds = new Bounds(new Vector3(-5,1.5f,-4.5f),new Vector3(9.6f,4,8.6f));
            definition.spawnPosition = new Vector3(-7.60f,.025f,-3.35f); definition.spawnYaw = 180;
            var codes = new[] {"LIV","MBR","OFF","HALL","ENT","BTH","KIT","BAL"};
            var names = new[] {"客餐厅","主卧","次卧与书房","走廊","玄关","卫生间","厨房","阳台"};
            definition.areas = codes.Select((code,i) => {
                var b = renderers.First(r => r.name.Contains("Floor_"+code+"_") && r.name.Contains("Subfloor")).bounds;
                b.center = new Vector3(b.center.x,1.4f,b.center.z); b.size = new Vector3(b.size.x,3.2f,b.size.z);
                return new LevelArea {name=names[i],bounds=b}; }).ToArray();
            EditorUtility.SetDirty(definition); session.definition = definition;
            var cat = UnityEngine.Object.FindFirstObjectByType<CatController>();
            cat.transform.position = definition.spawnPosition; cat.transform.rotation = Quaternion.Euler(0,180,0);
            cat.visualOffset = new Vector3(0,.0099f,0); cat.visual.localPosition = cat.visualOffset;
            var camera = UnityEngine.Object.FindFirstObjectByType<CatCamera>(); camera.yaw = 180;
            var sun = new GameObject("Home90Sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48,-32,0); sun.intensity = 1.35f; sun.color = new Color(1,.94f,.82f);
            sun.shadows = LightShadows.Soft; sun.shadowBias = .008f; sun.shadowNormalBias = .04f; RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.57f,.62f,.69f); RenderSettings.ambientEquatorColor = new Color(.43f,.40f,.34f); RenderSettings.ambientGroundColor = new Color(.26f,.24f,.21f);
            foreach (var area in definition.areas)
            {
                var fill = new GameObject("RoomLight_"+area.name).AddComponent<Light>();
                fill.transform.position = new Vector3(area.bounds.center.x,2.30f,area.bounds.center.z);
                fill.type = LightType.Point; fill.range = new Vector2(area.bounds.size.x,area.bounds.size.z).magnitude*.7f+1.5f;
                fill.intensity = 3.4f; fill.color = new Color(1,.92f,.79f); fill.shadows = LightShadows.None;
            }
            var probe = new GameObject("Home90Reflections").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(-5,1.2f,-4.5f); probe.size = new Vector3(10,3,9);
            probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.resolution = 128; probe.boxProjection = true; probe.cullingMask = ~(1<<8); probe.intensity = .7f;
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../.tools/gameplay-home90");
            int physical = room.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled && r.GetComponent<Collider>()!=null);
            File.WriteAllText("../.tools/gameplay-home90/home-build.txt","Physical mesh colliders: "+physical+"\nRooms: "+definition.areas.Length+"\nTasks: "+definition.requiredObjectives.Length);
        }

        static void TrimSkirtingAtPassages(GameObject room, MeshRenderer[] renderers)
        {
            // The art library's room borders cross doorway gaps. Cut only the separate gameplay copy.
            var openings = new[] {
                new Bounds(new Vector3(-3.6f,.6f,-4),new Vector3(.96f,2,.35f)),
                new Bounds(new Vector3(-3,.6f,-5.575f),new Vector3(.35f,2,.91f)),
                new Bounds(new Vector3(-4.2f,.6f,-7.825f),new Vector3(.35f,2,.91f)),
                new Bounds(new Vector3(-8.3f,.6f,-6),new Vector3(1.16f,2,.35f)),
                new Bounds(new Vector3(-7.05f,.6f,-1.6f),new Vector3(3.26f,2,.35f)),
                new Bounds(new Vector3(-4.2f,.6f,-5.085f),new Vector3(.35f,2,1.33f)),
                new Bounds(new Vector3(-3.54f,.6f,-7),new Vector3(1.14f,2,.35f))};
            foreach (var r in renderers.Where(r=>r.name.Contains("Skirting_")))
            {
                var bounds=r.bounds; bool alongX=bounds.size.x>bounds.size.z;
                var pieces=new System.Collections.Generic.List<Vector2> {new Vector2(alongX?bounds.min.x:bounds.min.z,alongX?bounds.max.x:bounds.max.z)};
                bool changed=false;
                foreach (var gap in openings.Where(g=>g.Intersects(bounds)))
                {
                    float low=alongX?gap.min.x:gap.min.z, high=alongX?gap.max.x:gap.max.z;
                    var next=new System.Collections.Generic.List<Vector2>();
                    foreach (var piece in pieces)
                    {
                        if (high<=piece.x || low>=piece.y) {next.Add(piece);continue;}
                        changed=true;
                        if (low-piece.x>.015f) next.Add(new Vector2(piece.x,Mathf.Min(low,piece.y)));
                        if (piece.y-high>.015f) next.Add(new Vector2(Mathf.Max(high,piece.x),piece.y));
                    }
                    pieces=next;
                }
                if (!changed) continue;
                r.gameObject.SetActive(false);
                for(int i=0;i<pieces.Count;i++)
                {
                    var piece=GameObject.CreatePrimitive(PrimitiveType.Cube); piece.name=r.name+"_Trim_"+i;
                    var center=bounds.center; var size=bounds.size;
                    if(alongX) {center.x=(pieces[i].x+pieces[i].y)*.5f;size.x=pieces[i].y-pieces[i].x;}
                    else {center.z=(pieces[i].x+pieces[i].y)*.5f;size.z=pieces[i].y-pieces[i].x;}
                    piece.transform.position=center; piece.transform.localScale=size; piece.transform.SetParent(room.transform,true);
                    piece.GetComponent<Renderer>().sharedMaterial=r.sharedMaterial;
                }
            }
        }

        static void OpenDoors(GameObject room, MeshRenderer[] renderers)
        {
            foreach (var slab in renderers.Where(r => r.name.Contains("Door_Slab") && !r.name.Contains("_entry_")))
            {
                string prefix = slab.name.Substring(0,slab.name.IndexOf("_P01_",StringComparison.Ordinal));
                var moving = renderers.Where(r => r.name.StartsWith(prefix+"_",StringComparison.Ordinal) &&
                    (r.name.Contains("Door_Slab") || r.name.Contains("Handle_Rose") || r.name.Contains("Lever"))).ToArray();
                if (slab.name.Contains("bath_slider")) { foreach (var r in moving) r.transform.position += Vector3.forward * .90f; continue; }
                var b = slab.bounds;
                var pivot = new GameObject("OpenDoor_"+prefix).transform; pivot.SetParent(room.transform);
                // The master leaf folds against the west wall, leaving the entry and bedside aisle open.
                bool master=slab.name.Contains("_master_");
                pivot.position = b.size.x > b.size.z ? new Vector3(master?b.min.x:b.max.x,0,b.center.z) : new Vector3(b.center.x,0,b.max.z);
                foreach (var r in moving) r.transform.SetParent(pivot,true);
                pivot.rotation = Quaternion.Euler(0,b.size.x > b.size.z && !master ? 90 : -90,0);
            }
            Home90CollisionBuilder.OpenSlidingDoors(room,renderers);
        }

        static void CreateStart()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.fieldOfView = 35; camera.nearClipPlane = .02f; camera.farClipPlane = 15;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.11f,.13f,.14f); camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var gallery = new GameObject("CatModelGallery").AddComponent<CatModelGallery>(); gallery.galleryCamera = camera;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "DisplayFloor";
            floor.transform.position = new Vector3(0,-.035f,0); floor.transform.localScale = new Vector3(8,.06f,8);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/GalleryFloor.mat");
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,"Assets/Generated/GalleryFloor.mat"); }
            material.SetColor("_BaseColor",new Color(.23f,.27f,.27f)); material.SetFloat("_Smoothness",.2f); EditorUtility.SetDirty(material);
            floor.GetComponent<Renderer>().sharedMaterial = material;
            var key = new GameObject("GallerySun").AddComponent<Light>(); key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(45,-40,0); key.intensity = 1.65f; key.color = new Color(1,.96f,.88f); key.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.6f,.65f,.72f);
            RenderSettings.ambientEquatorColor = new Color(.4f,.43f,.46f); RenderSettings.ambientGroundColor = new Color(.24f,.22f,.19f);
            RenderSettings.sun = key; EditorSceneManager.SaveScene(scene,StartPath); AssetDatabase.SaveAssets();
        }
    }
}
