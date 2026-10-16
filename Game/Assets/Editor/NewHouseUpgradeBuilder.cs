using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatSimulator.Editor
{
    public static class NewHouseUpgradeBuilder
    {
        const string ScenePath = "Assets/Scenes/New_House_Preview.unity";
        const string MeshPath = "Assets/Art/NewHouse/Gameplay/NewHouseUpgradeMeshes.asset";
        const float Factor = 1.2f;
        [Serializable] class Row { public string name, placement, id; }
        [Serializable] class Manifest { public Row[] objects; }
        static readonly List<NewHouseItem> items = new List<NewHouseItem>();
        static Transform home;
        static PhysicsMaterial contact;
        static Vector3 Expand(Vector3 p) => new Vector3(p.x * Factor, p.y, p.z * Factor);
        static Bounds Expand(Bounds b) => new Bounds(Expand(b.center), Vector3.Scale(b.size, new Vector3(Factor, 1, Factor)));
        static NewHouseItem Item(string fragment) => items.Single(i => i.placement.Contains(fragment));
        static void Move(NewHouseItem item, Vector3 center) => item.transform.position += center - item.Bounds.center;
        static void Bottom(NewHouseItem item, float height) => item.transform.position += Vector3.up * (height - item.Bounds.min.y);
        static void Support(string child, string parent)
        {
            var a = Item(child); var b = Item(parent); a.support = b; a.transform.SetParent(b.transform, true);
        }
        static MeshRenderer[] Parts(NewHouseItem item) => item.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.gameObject.activeInHierarchy && r.GetComponentInParent<NewHouseItem>(true) == item).ToArray();
        static Mesh Save(Mesh mesh) { AssetDatabase.AddObjectToAsset(mesh, MeshPath); return mesh; }
        public static void ReapplyAndBuild() { Reapply(); FirstPlayableBuilder.Build(); }
        public static void ConfigureWindowsAndBuild()
        {
            // D3D12Core crashes during device teardown on the development laptop; the
            // same rendered scene and mode/reload sequence exits cleanly with D3D11.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
            AssetDatabase.SaveAssets();FirstPlayableBuilder.Build();
        }
        public static void BakeVisibilityAndBuild()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var preview=UnityEngine.Object.FindFirstObjectByType<NewHousePreview>();preview.RestoreFullHouse();
            preview.viewCamera.useOcclusionCulling=false;
            UnityEngine.Object.FindFirstObjectByType<CatCamera>().GetComponent<Camera>().useOcclusionCulling=true;
            foreach(var renderer in preview.residence.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.GetComponentInParent<Rigidbody>() || renderer.GetComponent<SoftPillow>())continue;
                var flags=GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)|StaticEditorFlags.OccludeeStatic;
                if(renderer.name.StartsWith("NH_Wall_")||renderer.name.StartsWith("NH_Ceiling_"))flags|=StaticEditorFlags.OccluderStatic;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,flags);
            }
            var definition=UnityEngine.Object.FindFirstObjectByType<LevelSession>().definition;
            var volume=UnityEngine.Object.FindFirstObjectByType<OcclusionArea>();
            if(!volume)volume=new GameObject("NewHouseOcclusionVolume").AddComponent<OcclusionArea>();
            volume.transform.position=definition.activityBounds.center;
            volume.size=definition.activityBounds.size+new Vector3(5,2,5);
            var serializedVolume=new SerializedObject(volume);var viewVolume=serializedVolume.FindProperty("m_IsViewVolume");if(viewVolume!=null){viewVolume.boolValue=true;serializedVolume.ApplyModifiedPropertiesWithoutUndo();}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            StaticOcclusionCulling.smallestOccluder=.45f;StaticOcclusionCulling.smallestHole=.08f;StaticOcclusionCulling.backfaceThreshold=100;
            if(!StaticOcclusionCulling.Compute())throw new InvalidOperationException("House occlusion bake failed");
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();FirstPlayableBuilder.Build();
        }
        public static void OptimizeSunAndBuild()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Single(l=>l.type==LightType.Directional);
            var data=sun.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            if(!data)data=sun.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            data.softShadowQuality=UnityEngine.Rendering.Universal.SoftShadowQuality.Low;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();FirstPlayableBuilder.Build();
        }
        public static void OptimizeGlassAndBuild()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            foreach(string path in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Art/NewHouse/Materials"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.Contains("Glass.mat")))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);material.SetFloat("_ReceiveShadows",0);material.EnableKeyword("_RECEIVE_SHADOWS_OFF");EditorUtility.SetDirty(material);
            }
            var preview=UnityEngine.Object.FindFirstObjectByType<NewHousePreview>();
            foreach(var renderer in preview.residence.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterials.All(m=>m&&m.HasProperty("_Surface")&&m.GetFloat("_Surface")>.5f)))renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();FirstPlayableBuilder.Build();
        }

        public static void Reapply()
        {
            var backup=File.ReadAllText("../.tools/newhouse-upgrade/backup-path.txt").Trim();
            if(!Path.GetFullPath(backup).StartsWith(Path.GetFullPath("../Source/Backups")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Invalid backup path");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            File.Copy(Path.Combine(backup,"Game/Assets/Scenes/New_House_Preview.unity"),ScenePath,true);
            File.Copy(Path.Combine(backup,"Game/Assets/Art/NewHouse/Gameplay/NewHouseLevel.asset"),"Assets/Art/NewHouse/Gameplay/NewHouseLevel.asset",true);
            AssetDatabase.DeleteAsset(MeshPath);AssetDatabase.DeleteAsset("Assets/Art/NewHouse/Gameplay/NewHousePropContact.physicMaterial");AssetDatabase.Refresh();Apply();
        }
        [MenuItem("Cat Simulator/Upgrade New House Layout")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var preview = UnityEngine.Object.FindFirstObjectByType<NewHousePreview>(); home = preview.residence;
            if (home.GetComponent<NewHouseLayout>()) throw new InvalidOperationException("New house upgrade already applied; restore the explicit backup before rebuilding it.");
            items.Clear(); preview.RestoreFullHouse();
            AssetDatabase.CreateAsset(new Mesh { name = "New house expanded doors and soft furnishings" }, MeshPath);
            contact = new PhysicsMaterial("NewHouse_RealisticPropContact") { staticFriction = .6f, dynamicFriction = .45f, bounciness = .025f,
                frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum };
            AssetDatabase.CreateAsset(contact, "Assets/Art/NewHouse/Gameplay/NewHousePropContact.physicMaterial");
            var rows = JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Art/NewHouse/manifest.json")).objects.ToDictionary(r => r.name);
            var renderers = home.GetComponentsInChildren<MeshRenderer>(true);
            var originalLightPositions = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).ToDictionary(l => l, l => l.transform.position);

            // Expand moving leaves in their closed pose. Their kinematic owners keep unit scale,
            // so collision sweeps and the stored hinge/slider trajectories remain in world metres.
            foreach (var door in UnityEngine.Object.FindObjectsByType<NewHouseDoor>(FindObjectsSortMode.None))
            {
                door.anchor.position = Expand(door.anchor.position);
                foreach (var p in door.panels)
                {
                    var t = p.body.transform;
                    t.SetPositionAndRotation(p.closedPosition, p.closedRotation);
                    var leaves=t.GetComponentsInChildren<MeshFilter>().Select(f=>new {filter=f,position=f.transform.position,world=f.sharedMesh.vertices.Select(v=>Expand(f.transform.TransformPoint(v))).ToArray()}).ToArray();
                    t.position=Expand(p.closedPosition);
                    foreach (var leaf in leaves)
                    {
                        var filter=leaf.filter;
                        var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh); mesh.name += "_ExpandedLeaf";
                        filter.transform.position = Expand(leaf.position);
                        mesh.vertices = leaf.world.Select(v => filter.transform.InverseTransformPoint(v)).ToArray();mesh.RecalculateBounds();filter.sharedMesh = Save(mesh);
                    }
                    p.shape.center = Vector3.Scale(p.shape.center, new Vector3(Factor, 1, Factor));
                    p.shape.size = Vector3.Scale(p.shape.size, new Vector3(Factor, 1, Factor));
                    p.closedPosition = Expand(p.closedPosition); p.openPosition = Expand(p.openPosition);
                    t.SetPositionAndRotation(door.startsOpen ? p.openPosition : p.closedPosition, door.startsOpen ? p.openRotation : p.closedRotation);
                }
            }
            bool Architectural(Row row) => string.IsNullOrEmpty(row.placement) || new[] { "NH026", "NH038", "NH039", "NH040", "NH041", "NH049", "NH064", "NH065" }.Contains(row.id);
            foreach (var group in renderers.Where(r => rows.TryGetValue(r.name, out var row) && !Architectural(row)).GroupBy(r => rows[r.name].placement))
            {
                NewHouseItem item;
                var ball = group.First().GetComponentInParent<NewHouseBall>();
                if (ball) item = ball.gameObject.AddComponent<NewHouseItem>();
                else
                {
                    var b = group.First().bounds; foreach (var r in group.Skip(1)) b.Encapsulate(r.bounds);
                    item = new GameObject(group.Key).AddComponent<NewHouseItem>();item.transform.SetParent(home, true);item.transform.position = b.center;
                    foreach (var r in group) r.transform.SetParent(item.transform, true);
                }
                item.placement = group.Key;items.Add(item);
            }
            // Items on the same support move together instead of drifting off the tabletop.
            foreach (var child in new[] { "LIV_NH008", "LIV_NH012", "LIV_NH029_PlantTrailing_02", "LIV_NH072" }) Support(child, "LIV_NH060");
            Support("LIV_NH031_PlantBranching_02", "LIV_NH071");Support("LIV_NH027", "LIV_NH071");
            Support("LIV_NH014", "LIV_NH062");Support("LIV_NH031_PlantBranching_03", "LIV_NH062");Support("LIV_NH028", "LIV_NH062");
            foreach (var child in new[] { "LIV_NH016_DecorCushion_Sage_02", "LIV_NH016_DecorCushion_Sage_03", "LIV_NH022", "LIV_NH021" }) Support(child, "LIV_NH007");
            foreach (var child in new[] { "MBR_NH048_SleepingPillow_01", "MBR_NH048_SleepingPillow_02", "MBR_NH033", "MBR_NH016" }) Support(child, "MBR_NH050");
            Support("MBR_NH044_BedsideLamp_01", "MBR_NH067_BedsideTable_01");Support("MBR_NH044_BedsideLamp_02", "MBR_NH067_BedsideTable_02");
            foreach (var child in new[] { "OFF_NH008", "OFF_NH024" }) Support(child, "OFF_NH063");
            Support("OFF_NH031", "OFF_NH056");Support("OFF_NH048", "OFF_NH051");Support("OFF_NH033", "OFF_NH051");
            foreach (var child in new[] { "BAL_NH016", "BAL_NH022", "BAL_NH021", "BAL_NH036", "BAL_NH029" }) Support(child, "BAL_NH074");
            Support("BAL_NH030", "BAL_NH069");
            Support("BAL_NH042", "BAL_NH047");Support("BAL_NH046_TowelStack_Towel_03", "BAL_NH047");Support("BAL_NH046_TowelStack_Top_04", "BAL_NH047");
            foreach (var child in new[] { "ENT_NH013", "ENT_NH046" }) Support(child, "ENT_NH058");
            foreach (var child in new[] { "BTH_NH042", "BTH_NH046" }) Support(child, "BTH_NH004");

            // The generated bed covers, perch and counter infills belong to their furniture.
            foreach (var r in renderers.Where(r => r.name.StartsWith("NH_MasterBedCover") || r.name.StartsWith("NH_GuestBedCover") || r.name.StartsWith("NH_WindowPerch_")))
                r.transform.SetParent(Item(r.name.StartsWith("NH_Master") ? "MBR_NH050" : r.name.StartsWith("NH_Guest") ? "OFF_NH051" : "MBR_NH073").transform, true);
            var stone = renderers.Single(r => r.name.StartsWith("NH_LaundryStoneTop"));stone.transform.SetParent(Item("BAL_NH018").transform, true);
            var architecture = new GameObject("NH_ExpandedArchitecture").transform;architecture.SetParent(home, false);
            var moving = UnityEngine.Object.FindObjectsByType<NewHouseDoor>(FindObjectsSortMode.None).SelectMany(d => d.panels).Select(p => p.body.transform).ToArray();
            foreach (var r in renderers)
            {
                if (r.GetComponentInParent<NewHouseItem>() || moving.Any(t => r.transform.IsChildOf(t))) continue;
                r.transform.SetParent(architecture, true);
            }
            var floor = home.Find("NH_ContinuousFloorSupport");if (floor) floor.SetParent(architecture, true);
            architecture.localScale = new Vector3(Factor, 1, Factor);
            foreach (var item in items.Where(i => !i.support)) item.transform.position = Expand(item.transform.position);

            ArrangeLiving();ArrangeBedroom();ArrangeBalcony(stone);
            // Built-in L counter follows the enlarged walls; loose countertop items retain size.
            var kitchen = Item("KIT_NH066");kitchen.transform.localScale = new Vector3(Factor, 1, Factor);
            foreach (var id in new[] { "KIT_NH017", "KIT_NH020", "KIT_NH014", "KIT_NH030", "KIT_NH043", "KIT_NH061_ChoppingBoard", "KIT_NH061_Small" })
                Bottom(Item(id), id.Contains("NH020") ? .80f : id.Contains("NH017") ? .911f : .903f);
            Item("KIT_NH034").transform.RotateAround(Item("KIT_NH034").Bounds.center, Vector3.up, 180);
            var boardLarge=Item("KIT_NH061_ChoppingBoard");var boardSmall=Item("KIT_NH061_Small");
            foreach(var board in new[]{boardLarge,boardSmall})board.transform.RotateAround(board.Bounds.center,Vector3.forward,90);
            Move(boardLarge,new Vector3(-11.38f,.916f,-9.80f));Bottom(boardLarge,.903f);
            Move(boardSmall,new Vector3(-11.37f,.94f,-9.80f));Bottom(boardSmall,boardLarge.Bounds.max.y+.002f);
            // Wardrobe's textured door face was against the partition; move and turn it inward.
            var wardrobe = Item("MBR_NH076");wardrobe.transform.RotateAround(wardrobe.Bounds.center, Vector3.up, 180);
            wardrobe.transform.position+=Vector3.left*.28f;
            foreach (var seam in Parts(wardrobe).Where(r => r.name.EndsWith("0067") || r.name.EndsWith("0068"))) seam.gameObject.SetActive(false);
            var water = Item("LIV_NH011");water.transform.localScale *= .75f;Bottom(water, .004f);
            var bowls = Item("LIV_NH068");bowls.transform.localScale = new Vector3(.60f, .36f, .60f);Bottom(bowls, .004f);

            var layout = home.gameObject.AddComponent<NewHouseLayout>();layout.horizontalExpansion = Factor;
            var session = UnityEngine.Object.FindFirstObjectByType<LevelSession>();var definition = session.definition;
            definition.displayName = "温馨大宅 · 自由探索";definition.playerScale = .675f;definition.spawnPosition = Expand(definition.spawnPosition);
            definition.activityBounds = Expand(definition.activityBounds);foreach (var area in definition.areas) area.bounds = Expand(area.bounds);EditorUtility.SetDirty(definition);
            UnityEngine.Object.FindFirstObjectByType<CatController>().transform.position = definition.spawnPosition;
            var originalCenter=preview.houseCenter;preview.houseCenter = Expand(originalCenter);
            foreach (var view in preview.views) { view.position = view.ortho?preview.houseCenter+(view.position-originalCenter)*Factor:Expand(view.position);if (view.ortho) view.size *= Factor;view.name = view.name.Replace("90㎡", "宽敞住宅"); }
            foreach (var pair in originalLightPositions) { pair.Key.transform.position = Expand(pair.Value);if (pair.Key.type != LightType.Directional) pair.Key.range *= Factor; }
            foreach (var probe in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) { probe.transform.position = Expand(probe.transform.position);probe.size = Vector3.Scale(probe.size,new Vector3(Factor,1,Factor)); }
            AddTableSupports();
            SeatPlantBases();
            foreach (var item in items.Where(i => i.gameObject.activeInHierarchy)) if (Dynamic(item)) AddBody(item);
            AddSoftSurfaces();Settle();
            PlayerSettings.bundleVersion = "0.3.2";
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            var summary = "Expansion="+Factor+"; catScale="+definition.playerScale+"; dynamicProps="+items.Count(i=>i.GetComponent<Rigidbody>())+"; softSurfaces="+home.GetComponentsInChildren<SoftPillow>().Length;
            File.WriteAllText("../.tools/newhouse-upgrade/layout-applied.txt", summary);Debug.Log("NEW_HOUSE_UPGRADE_READY "+summary);
        }

        static void ArrangeLiving()
        {
            var sofa=Item("LIV_NH007");var b=sofa.Bounds;
            // Seat faces -X. Separate cushions and lay the throw over the far armrest.
            var sage1=Item("LIV_NH016_DecorCushion_Sage_02");var sage2=Item("LIV_NH016_DecorCushion_Sage_03");var cream=Item("LIV_NH022");
            Move(sage1,new Vector3(b.center.x-.01f,.69f,b.center.z+.71f));
            Move(sage2,new Vector3(b.center.x-.01f,.69f,b.center.z-.43f));
            Move(cream,new Vector3(b.center.x-.01f,.69f,b.center.z+.12f));
            var blanket=Item("LIV_NH021");blanket.transform.localScale *= .70f;Move(blanket,new Vector3(b.center.x-.03f,.74f,b.center.z-.99f));
            var table=Item("LIV_NH062");Move(table,new Vector3(-7.62f,.375f,-6.27f));
            var bench=Item("LIV_NH052");Move(bench,new Vector3(-7.62f,.40f,-6.89f));
            Move(Item("LIV_NH059_DiningChair_01"),new Vector3(-7.19f,.395f,-5.59f));
            Move(Item("LIV_NH059_DiningChair_02"),new Vector3(-8.05f,.395f,-5.59f));
            // Actual table top is the support, not the renderer's decorative bounding box.
            foreach(var fragment in new[]{"LIV_NH029_PlantTrailing_02","LIV_NH012","LIV_NH008","LIV_NH072"})Bottom(Item(fragment),fragment.Contains("NH072")?.383f:.385f);
            var book=Item("LIV_NH008");var tray=Item("LIV_NH072");
            var coffee=Item("LIV_NH060").Bounds.center;
            Move(tray,new Vector3(coffee.x+.10f,.399f,coffee.z-.13f));
            Move(book,new Vector3(coffee.x-.24f,.401f,coffee.z-.14f));
            Move(Item("LIV_NH012"),new Vector3(coffee.x-.21f,.44f,coffee.z+.18f));Bottom(Item("LIV_NH012"),.384f);
            Move(Item("LIV_NH029_PlantTrailing_02"),new Vector3(coffee.x+.39f,.555f,coffee.z+.07f));Bottom(Item("LIV_NH029_PlantTrailing_02"),.384f);
            var tv=Item("LIV_NH027");
            foreach(var r in Parts(tv))
            {
                var filter=r.GetComponent<MeshFilter>();var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var center=r.bounds.center;
                var vertices=mesh.vertices;
                for(int i=0;i<vertices.Length;i++){var w=r.transform.TransformPoint(vertices[i]);w.y-=(w.z-center.z)*.028f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.52f,.72f,w.y));vertices[i]=r.transform.InverseTransformPoint(w);}
                mesh.name+="_LevelScreen";mesh.vertices=vertices;mesh.RecalculateBounds();filter.sharedMesh=Save(mesh);
                var collider=r.GetComponent<MeshCollider>();if(collider)collider.sharedMesh=Outward(mesh);
            }
            Bottom(tv,.452f);
            var hanging=Item("LIV_NH029_PlantTrailing_01");hanging.transform.localScale*=.85f;Move(hanging,new Vector3(-11.51f,2.475f,-5.748f));Bottom(hanging,2.218f);
        }

        static void ArrangeBedroom()
        {
            Item("MBR_NH016").gameObject.SetActive(false);Item("MBR_NH033").gameObject.SetActive(false);
            var bed=Item("MBR_NH050");var bounds=bed.Bounds;
            for(int i=1;i<=2;i++)
            {
                var pillow=Item("MBR_NH048_SleepingPillow_0"+i);
                Move(pillow,new Vector3(bounds.center.x+(i==1?.42f:-.42f),.65f,bounds.min.z+.45f));
            }
            // One smooth cover and two sleeping pillows; remove the extra decorative folded duvet.
            var cover=bed.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.StartsWith("NH_MasterBedCover"));
            var cb=cover.bounds;
            cover.transform.position+=new Vector3(bounds.center.x-cb.center.x,.575f-cb.center.y,(bounds.center.z+.26f)-cb.center.z);
            foreach(var pillow in new[]{Item("MBR_NH048_SleepingPillow_01"),Item("MBR_NH048_SleepingPillow_02")})Bottom(pillow,.573f);
        }

        static void ArrangeBalcony(MeshRenderer stone)
        {
            // Rebuild the lower laundry unit as a genuine 72 cm washer bay and a side cupboard.
            var storage=Item("BAL_NH018");
            var body=Parts(storage).Single(r=>r.name.Contains("NH018"));
            var original=body.GetComponent<MeshFilter>().sharedMesh;
            var clipped=ClipAbove(original,body.transform,.94f);body.GetComponent<MeshFilter>().sharedMesh=Save(clipped);
            body.GetComponent<MeshCollider>().sharedMesh=Outward(clipped);
            var b=storage.Bounds;float centerZ=storage.transform.position.z;
            const string woodPath="Assets/Art/NewHouse/Gameplay/NewHouseLaundryWood.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(woodPath);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Warm laundry oak"};material.SetColor("_BaseColor",new Color(.66f,.46f,.27f));material.SetFloat("_Smoothness",.25f);AssetDatabase.CreateAsset(material,woodPath);}
            void Panel(string name,Vector3 center,Vector3 size)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(storage.transform,true);go.transform.position=center;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
            float x=storage.transform.position.x;
            Panel("Laundry_LeftSide",new Vector3(x,.45f,centerZ-.632f),new Vector3(.66f,.9f,.035f));
            Panel("Laundry_RightSide",new Vector3(x,.45f,centerZ+.632f),new Vector3(.66f,.9f,.035f));
            Panel("Laundry_BayDivider",new Vector3(x,.45f,centerZ+.105f),new Vector3(.66f,.9f,.025f));
            Panel("Laundry_SideCupboard",new Vector3(x-.31f,.45f,centerZ+.37f),new Vector3(.035f,.88f,.49f));
            Panel("Laundry_SideShelf",new Vector3(x,.035f,centerZ+.37f),new Vector3(.66f,.05f,.49f));
            var washer=Item("BAL_NH047");Move(washer,new Vector3(x-.035f,.425f,centerZ-.265f));
            var top=stone.bounds.max.y;
            Move(Item("BAL_NH042"),new Vector3(x-.03f,top+.104f,centerZ-.34f));
            Move(Item("BAL_NH046_TowelStack_Towel_03"),new Vector3(x-.02f,top+.037f,centerZ-.04f));
            Move(Item("BAL_NH046_TowelStack_Top_04"),new Vector3(x-.02f,top+.103f,centerZ-.04f));
            Bottom(Item("BAL_NH029"),.453f);Bottom(Item("BAL_NH030"),.703f);
            // Move the floor plant clear of the laundry aisle and keep it grounded.
            var floorPlant=Item("BAL_NH032");Move(floorPlant,new Vector3(-6.83f,.27f,-.58f));Bottom(floorPlant,.004f);
        }

        static bool Dynamic(NewHouseItem item)
        {
            string n=item.placement;
            return new[]{"NH008_","NH011_","NH012_","NH013_","NH014_","NH015_","NH024_","NH029_","NH030_","NH031_","NH032_","NH036_","NH042_","NH043_","NH044_","NH061_","NH068_","NH070_","NH072_","NH080_"}.Any(n.Contains);
        }
        static void AddTableSupports()
        {
            foreach(var id in new[]{"LIV_NH060","LIV_NH062","LIV_NH071","OFF_NH063","OFF_NH056","MBR_NH067_BedsideTable_01","MBR_NH067_BedsideTable_02","ENT_NH058","BTH_NH004","BAL_NH069","BAL_NH074"})
            {
                var item=Item(id);var b=item.Bounds;var go=new GameObject("ContinuousTabletopSupport");go.transform.SetParent(item.transform,true);
                go.transform.position=new Vector3(b.center.x,b.max.y-.018f,b.center.z);var box=go.AddComponent<BoxCollider>();box.size=new Vector3(b.size.x-.018f,.035f,b.size.z-.018f);box.contactOffset=.0015f;
            }
            var grass=Item("KIT_NH030");Move(grass,new Vector3(-8.57f,1.04f,-10.23f));Bottom(grass,.903f);
            var kitchen=Item("KIT_NH066");var bounds=kitchen.Bounds;
            foreach(bool back in new[]{false,true})
            {
                var go=new GameObject(back?"KitchenBackCounterSupport":"KitchenSideCounterSupport");go.transform.SetParent(home,true);
                go.transform.position=new Vector3(back?bounds.center.x:bounds.min.x+.34f,.882f,back?bounds.min.z+.34f:bounds.center.z);
                var box=go.AddComponent<BoxCollider>();box.size=back?new Vector3(bounds.size.x,.035f,.68f):new Vector3(.68f,.035f,bounds.size.z);box.contactOffset=.0015f;
            }
        }
        static void AddBody(NewHouseItem item)
        {
            if(item.GetComponent<NewHouseBall>()){item.GetComponent<Rigidbody>().maxDepenetrationVelocity=.7f;return;}
            // Rigidbody owners use metres. Preserve the already resized visual children.
            if(item.transform.lossyScale!=Vector3.one || Quaternion.Angle(item.transform.rotation,Quaternion.identity)>.001f)
            {
                var children=item.transform.Cast<Transform>().ToArray();foreach(var child in children)child.SetParent(home,true);
                item.transform.localScale=Vector3.one;item.transform.rotation=Quaternion.identity;foreach(var child in children)child.SetParent(item.transform,true);
            }
            float foot=PlantBase(item);
            item.hasContactBase=true;var cb=item.Bounds.center;item.contactBaseLocal=item.transform.InverseTransformPoint(new Vector3(cb.x,foot,cb.z));
            foreach(var r in Parts(item))foreach(var c in r.GetComponents<Collider>())UnityEngine.Object.DestroyImmediate(c);
            foreach(var t in item.GetComponentsInChildren<Transform>())GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            var b=item.Bounds;string n=item.placement;
            Collider collider;
            if(n.Contains("NH029")||n.Contains("NH030")||n.Contains("NH031")||n.Contains("NH032"))
            {
                var pot=item.gameObject.AddComponent<BoxCollider>();
                float radius=Mathf.Min(b.extents.x,b.extents.z)*.52f;float height=Mathf.Max(radius*2,b.size.y*.42f);
                pot.size=new Vector3(radius*2,height,radius*2);
                pot.center=item.transform.InverseTransformPoint(new Vector3(b.center.x,foot+height*.5f,b.center.z));collider=pot;
                var foliage=item.gameObject.AddComponent<SphereCollider>();foliage.radius=Mathf.Min(b.extents.x,b.extents.z)*.62f;
                foliage.center=item.transform.InverseTransformPoint(new Vector3(b.center.x,b.min.y+b.size.y*.75f,b.center.z));foliage.sharedMaterial=contact;foliage.contactOffset=.0015f;
            }
            else
            {
                var box=item.gameObject.AddComponent<BoxCollider>();box.center=item.transform.InverseTransformPoint(b.center);box.size=b.size;collider=box;
            }
            collider.sharedMaterial=contact;collider.contactOffset=.0015f;
            var body=item.gameObject.AddComponent<Rigidbody>();
            body.mass=n.Contains("NH024")?1.4f:n.Contains("NH044")?.65f:n.Contains("NH061")?(n.Contains("Small")?.5f:1.15f):n.Contains("Plant")||n.Contains("GrassPot")?Mathf.Clamp(b.size.y*1.6f,.5f,1.4f):n.Contains("NH008")?.32f:n.Contains("NH080")?.08f:n.Contains("NH036")?.45f:.25f;
            body.linearDamping=.1f;body.angularDamping=.25f;body.maxDepenetrationVelocity=.7f;
            body.solverIterations=12;body.solverVelocityIterations=8;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.centerOfMass=item.transform.InverseTransformPoint(new Vector3(b.center.x,foot+b.size.y*(n.Contains("Plant")||n.Contains("Lamp")?.18f:.42f),b.center.z));
            item.gameObject.AddComponent<PushableProp>();
        }
        static float PlantBase(NewHouseItem item)
        {
            var b=item.Bounds;
            if(!item.placement.Contains("Plant")&&!item.placement.Contains("GrassPot"))return b.min.y;
            var heights=new List<float>();
            foreach(var offset in new[]{Vector2.zero,new Vector2(.015f,0),new Vector2(-.015f,0),new Vector2(0,.015f),new Vector2(0,-.015f)})
            foreach(var collider in Parts(item).SelectMany(r=>r.GetComponents<MeshCollider>()))
                if(collider.Raycast(new Ray(new Vector3(b.center.x+offset.x,b.min.y-.05f,b.center.z+offset.y),Vector3.up),out var hit,b.size.y+.1f))heights.Add(hit.point.y);
            heights.Sort();return heights.Count>0?heights[heights.Count/2]:b.min.y;
        }
        static void SeatPlantBases()
        {
            Physics.SyncTransforms();
            foreach(var item in items.Where(i=>i.placement.Contains("Plant")||i.placement.Contains("GrassPot")))
            {
                float target=item.support?item.support.Bounds.max.y:.004f;
                if(item.placement.Contains("LIV_NH029_PlantTrailing_01"))target=2.218f;
                if(item.placement.Contains("KIT_NH030"))target=.903f;
                item.transform.position+=Vector3.up*(target-PlantBase(item));
            }
            Physics.SyncTransforms();
        }

        static void AddSoftSurfaces()
        {
            foreach(var item in items.Where(i=>i.gameObject.activeInHierarchy))foreach(var r in Parts(item))
            {
                string n=item.placement;
                bool sofa=n.Contains("NH007"),bed=n.Contains("NH050")||n.Contains("NH051");
                if(!sofa&&!bed&&!new[]{"NH016_","NH022_","NH048_","NH021_","NH023_","NH033_","NH073_"}.Any(n.Contains))continue;
                if(r.name.StartsWith("NH_Window"))continue;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);
                var filter=r.GetComponent<MeshFilter>();
                var mesh=HomeSurfaceSubdivision.DenseTop(filter.sharedMesh,r.transform,true);mesh=Outward(mesh,false);filter.sharedMesh=Save(mesh);
                foreach(var c in r.GetComponents<Collider>())UnityEngine.Object.DestroyImmediate(c);
                var collider=r.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.contactOffset=.0015f;
                var soft=r.gameObject.AddComponent<SoftPillow>();soft.maximumSink=n.Contains("NH048")?.045f:sofa?.035f:.03f;
                if(sofa||bed)
                {
                    soft.useSharedHeightRange=true;soft.deformationBaseY=sofa?.30f:.35f;soft.deformationTopY=sofa?.48f:n.Contains("NH050")?.575f:.55f;
                    soft.activationCeilingY=soft.deformationTopY+.25f;
                }
                if(n.Contains("NH073")){soft.maximumSink=.025f;soft.useSharedHeightRange=true;soft.deformationBaseY=.50f;soft.deformationTopY=.56f;}
                soft.collisionTemplate=Save(ContactGrid(r,collider,sofa||bed?soft.deformationBaseY:r.bounds.min.y));
                collider.sharedMesh=soft.collisionTemplate;
                if((sofa||bed)&&!r.name.Contains("Cover"))
                {
                    var structure=new GameObject("FixedFurnitureFrame");structure.transform.SetParent(r.transform,false);
                    var lower=UnityEngine.Object.Instantiate(mesh);lower.name+="_FixedLowerFrame";var vertices=lower.vertices;
                    for(int slot=0;slot<lower.subMeshCount;slot++)
                    {
                        var old=lower.GetTriangles(slot);var kept=new List<int>();for(int t=0;t<old.Length;t+=3)
                        {float y=(r.transform.TransformPoint(vertices[old[t]]).y+r.transform.TransformPoint(vertices[old[t+1]]).y+r.transform.TransformPoint(vertices[old[t+2]]).y)/3;if(y<soft.deformationBaseY)kept.AddRange(new[]{old[t],old[t+1],old[t+2]});}
                        lower.SetTriangles(kept,slot);
                    }
                    if(lower.triangles.Length>0)structure.AddComponent<MeshCollider>().sharedMesh=Save(lower);
                    else{UnityEngine.Object.DestroyImmediate(lower);UnityEngine.Object.DestroyImmediate(structure);}
                }
            }
            // Solid support below the deformable cushions prevents falling inside a scanned mesh.
            foreach(var code in new[]{"LIV_NH007","MBR_NH050","OFF_NH051"})
            {
                var item=Item(code);var b=item.Bounds;bool sofa=code.Contains("NH007");
                var go=new GameObject("ContinuousSoftSupport");go.transform.SetParent(item.transform,true);
                go.transform.position=new Vector3(b.center.x+(sofa?-.08f:0),sofa?.33f:.42f,b.center.z+(sofa?0:.02f));
                var box=go.AddComponent<BoxCollider>();box.size=new Vector3(sofa?.69f:b.size.x-.1f,sofa?.14f:.18f,sofa?b.size.z-.34f:b.size.z-.20f);box.contactOffset=.0015f;
            }
        }
        static Mesh ContactGrid(MeshRenderer renderer,MeshCollider source,float bottom)
        {
            Physics.SyncTransforms();var b=renderer.bounds;
            int nx=Mathf.Max(3,Mathf.CeilToInt(b.size.x/.055f)),nz=Mathf.Max(3,Mathf.CeilToInt(b.size.z/.055f));
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                var point=new Vector3(Mathf.Lerp(b.min.x+.001f,b.max.x-.001f,(float)x/nx),b.max.y+.05f,Mathf.Lerp(b.min.z+.001f,b.max.z-.001f,(float)z/nz));
                float y=source.Raycast(new Ray(point,Vector3.down),out var hit,b.size.y+.2f)?hit.point.y:bottom;
                vertices.Add(renderer.transform.InverseTransformPoint(new Vector3(point.x,Mathf.Max(bottom,y),point.z)));
            }
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x,c=a+nx+1;triangles.AddRange(new[]{a,c,a+1,a+1,c,c+1});}
            var edge=new List<int>();for(int x=0;x<=nx;x++)edge.Add(x);for(int z=1;z<=nz;z++)edge.Add(z*(nx+1)+nx);for(int x=nx-1;x>=0;x--)edge.Add(nz*(nx+1)+x);for(int z=nz-1;z>0;z--)edge.Add(z*(nx+1));
            int start=vertices.Count;
            foreach(int index in edge){var w=renderer.transform.TransformPoint(vertices[index]);w.y=bottom;vertices.Add(renderer.transform.InverseTransformPoint(w));}
            for(int i=0;i<edge.Count;i++){int j=(i+1)%edge.Count;triangles.AddRange(new[]{edge[i],edge[j],start+i,edge[j],start+j,start+i});}
            var mesh=new Mesh{name=renderer.name+"_SoftContactGrid"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
        static Mesh Outward(Mesh source,bool save=true)
        {
            var mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name+"_Outward";
            var vertices=mesh.vertices;var triangles=mesh.triangles;double volume=0;var center=mesh.bounds.center;
            for(int i=0;i<triangles.Length;i+=3)volume+=Vector3.Dot(vertices[triangles[i]]-center,Vector3.Cross(vertices[triangles[i+1]]-center,vertices[triangles[i+2]]-center));
            if(volume<0)for(int s=0;s<mesh.subMeshCount;s++){var t=mesh.GetTriangles(s);for(int i=0;i<t.Length;i+=3)(t[i+1],t[i+2])=(t[i+2],t[i+1]);mesh.SetTriangles(t,s);}
            if(!save)mesh.RecalculateNormals();return save?Save(mesh):mesh;
        }
        static Mesh ClipAbove(Mesh source,Transform transform,float height)
        {
            var vertices=source.vertices;var normals=source.normals;var uvs=source.uv;
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int[]>();
            for(int s=0;s<source.subMeshCount;s++)
            {
                var output=new List<int>();var t=source.GetTriangles(s);
                for(int i=0;i<t.Length;i+=3)
                {
                    var polygon=new List<(Vector3 p,Vector3 n,Vector2 uv)>();for(int k=0;k<3;k++){int index=t[i+k];polygon.Add((vertices[index],normals[index],uvs[index]));}
                    var kept=new List<(Vector3 p,Vector3 n,Vector2 uv)>();
                    for(int k=0;k<polygon.Count;k++)
                    {
                        var a=polygon[k];var b=polygon[(k+1)%polygon.Count];float da=transform.TransformPoint(a.p).y-height,db=transform.TransformPoint(b.p).y-height;
                        if(da>=0)kept.Add(a);if((da>=0)!=(db>=0)){float f=da/(da-db);kept.Add((Vector3.Lerp(a.p,b.p,f),Vector3.Lerp(a.n,b.n,f).normalized,Vector2.Lerp(a.uv,b.uv,f)));}
                    }
                    int start=v.Count;foreach(var a in kept){v.Add(a.p);n.Add(a.n);uv.Add(a.uv);}for(int k=1;k<kept.Count-1;k++)output.AddRange(new[]{start,start+k,start+k+1});
                }
                indices.Add(output.ToArray());
            }
            var mesh=new Mesh{name=source.name+"_UpperLaundryUnit",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.subMeshCount=indices.Count;for(int s=0;s<indices.Count;s++)mesh.SetTriangles(indices[s],s);mesh.RecalculateBounds();return mesh;
        }
        static void Settle()
        {
            Physics.SyncTransforms();var mode=Physics.simulationMode;
            try{Physics.simulationMode=SimulationMode.Script;for(int i=0;i<480;i++)Physics.Simulate(1f/120);}
            finally{Physics.simulationMode=mode;}
            foreach(var body in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(b=>!b.isKinematic)){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.Sleep();}
            Physics.SyncTransforms();
        }
    }
}
