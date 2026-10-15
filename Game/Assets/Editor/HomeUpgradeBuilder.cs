using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatSimulator.Editor
{
    public static class HomeUpgradeBuilder
    {
        const float Factor=1.18f;
        public sealed class Item
        {
            public string id;public Transform root;public MeshRenderer[] parts;public Bounds bounds;
            public Item support;
        }
        static readonly Regex PartPattern=new Regex(@"^(H90_[A-Z]+_[A-Z]+\d+_.+?_\d+)_P(\d+)_.+_E(\d+)$");
        static bool Movable(string name)=>Regex.IsMatch(name,@"^H90_[A-Z]+_(FUR|PRP|APP|CAT|LGT|CHR)\d+_");
        public static void RebuildAndBuild()
        {
            var backup=File.ReadAllText("../.tools/home-upgrade/backup-path.txt").Trim();
            var expected=Path.GetFullPath("../Source/Backups")+Path.DirectorySeparatorChar;
            if(!Path.GetFullPath(backup).StartsWith(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Invalid backup directory");
            File.Copy(Path.Combine(backup,"Home90.unity"),FullGameBuilder.HomePath,true);
            File.Copy(Path.Combine(backup,"Home90Level.asset"),"Assets/Generated/Home90Level.asset",true);
            AssetDatabase.Refresh();Apply();FirstPlayableBuilder.Build();
        }
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(FullGameBuilder.HomePath);
            var home=GameObject.Find("Home90Residence");
            if(home.GetComponent<HomeLayout>())throw new InvalidOperationException("Home layout has already been expanded");
            var parts=home.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && Movable(r.name)).ToArray();
            var items=GroupItems(parts,home.transform);
            var furniture=items.Where(i=>i.id.Contains("_FUR") || i.id.Contains("_CAT") || i.id.Contains("_APP")).ToArray();
            foreach(var item in items)
            {
                if(item.id.Contains("_Bedding_"))
                {
                    item.support=furniture.First(f=>f.id.StartsWith(item.id.Substring(0,8),StringComparison.Ordinal) && f.id.Contains("Bed_"));continue;
                }
                if(item.id.Contains("_FUR") || item.id.Contains("_CAT") || item.id.Contains("_APP") || item.bounds.min.y<.12f)continue;
                var center=item.bounds.center;
                var candidates=furniture.Where(f=>f!=item && f.parts.Any(r=>IsTop(r.name) &&
                    center.x>=r.bounds.min.x-.04f && center.x<=r.bounds.max.x+.04f && center.z>=r.bounds.min.z-.04f && center.z<=r.bounds.max.z+.04f &&
                    item.bounds.min.y>=r.bounds.max.y-.04f && item.bounds.min.y-r.bounds.max.y<.35f)).ToArray();
                item.support=candidates.OrderBy(f=>Mathf.Abs(item.bounds.min.y-f.parts.Where(r=>IsTop(r.name)).Max(r=>r.bounds.max.y))).FirstOrDefault();
                if(item.id.Contains("_DiningPendant_"))item.support=items.First(i=>i.id.Contains("_DiningTable_"));
            }
            foreach(var item in items.Where(i=>i.support!=null))item.root.SetParent(item.support.root,true);
            var support=home.transform.Find("Sofa_ContinuousSeatSupport");
            var sofa=items.First(i=>i.id.Contains("_Sofa_"));if(support)support.SetParent(sofa.root,true);
            var architecture=new GameObject("ExpandedArchitecture").transform;architecture.SetParent(home.transform,false);
            foreach(var child in home.transform.Cast<Transform>().ToArray())
                if(child!=architecture && !items.Any(i=>i.root==child))child.SetParent(architecture,true);
            architecture.localScale=new Vector3(Factor,1,Factor);
            foreach(var item in items.Where(i=>i.support==null))item.root.position=Expand(item.root.position);
            // Draw the complete dining set away from the wall, then pull chairs clear of the table legs.
            foreach(var item in items.Where(i=>i.id.Contains("_DiningTable_") || i.id.Contains("_DiningChair_") || i.id.Contains("_DiningBench_")))
                item.root.position+=Vector3.forward*.20f;
            foreach(var item in items.Where(i=>i.id.Contains("_DiningChair_")))item.root.position+=Vector3.forward*.24f;
            foreach(var item in items.Where(i=>i.id.Contains("_DiningBench_")))item.root.position+=Vector3.back*.12f;
            var fridge=items.First(i=>i.id.Contains("_Refrigerator_"));
            var fridgeBody=fridge.parts.First(r=>r.name.Contains("RefrigeratorBody"));
            fridge.root.RotateAround(fridgeBody.bounds.center,Vector3.up,-90);
            var layout=home.AddComponent<HomeLayout>();layout.horizontalExpansion=Factor;
            var session=UnityEngine.Object.FindFirstObjectByType<LevelSession>();var definition=session.definition;
            definition.displayName="宽敞住宅 · 自由探索";definition.spawnPosition=Expand(definition.spawnPosition);
            definition.activityBounds=Expand(definition.activityBounds);
            foreach(var area in definition.areas)area.bounds=Expand(area.bounds);
            EditorUtility.SetDirty(definition);
            var cat=UnityEngine.Object.FindFirstObjectByType<CatController>();cat.transform.position=definition.spawnPosition;
            foreach(var light in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>()))
                if(!light.transform.IsChildOf(home.transform)){light.transform.position=Expand(light.transform.position);if(light.type==LightType.Point)light.range*=Factor;}
            foreach(var probe in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ReflectionProbe>()))
            {probe.transform.position=Expand(probe.transform.position);probe.size=new Vector3(probe.size.x*Factor,probe.size.y,probe.size.z*Factor);}
            foreach(var pillow in items.Where(i=>i.id.Contains("_SleepingPillow_") && i.support!=null))
            {
                var mattress=pillow.support.parts.FirstOrDefault(r=>r.name.Contains("_Mattress_"));
                if(mattress)pillow.root.position+=Vector3.up*(mattress.bounds.max.y-pillow.parts.Min(r=>r.bounds.min.y)+.001f);
            }
            AddProps(items);AddSoftSurfaces(items,support);
            Settle();
            Validate(items,home);
            PlayerSettings.bundleVersion="0.3.0";
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }

        public static List<Item> GroupItems(MeshRenderer[] renderers,Transform home)
        {
            var items=new List<Item>();
            foreach(var prefix in renderers.Select(r=>(renderer:r,match:PartPattern.Match(r.name))).Where(x=>x.match.Success).GroupBy(x=>x.match.Groups[1].Value))
            {
                var group=new List<MeshRenderer>();int index=0;
                void Finish()
                {
                    if(group.Count==0)return;var b=group[0].bounds;foreach(var r in group.Skip(1))b.Encapsulate(r.bounds);
                    var root=new GameObject(prefix.Key+"_Item_"+(++index)).transform;root.SetParent(home,true);root.position=b.center;
                    foreach(var r in group)r.transform.SetParent(root,true);
                    items.Add(new Item{id=prefix.Key,root=root,parts=group.ToArray(),bounds=b});group.Clear();
                }
                foreach(var entry in prefix.OrderBy(x=>int.Parse(x.match.Groups[3].Value)))
                {if(int.Parse(entry.match.Groups[2].Value)==1)Finish();group.Add(entry.renderer);}
                Finish();
            }
            return items;
        }
        static bool IsTop(string n)=>n.Contains("_Top_") || n.Contains("_TopPanel_") || n.Contains("_Shelf_") || n.Contains("_FloatingPlatform_") || n.Contains("_Counter_") || n.Contains("_Mattress_") || n.Contains("_RoundTop_") || n.Contains("_SeatCushion_") || n.Contains("_SeatPad_") || n.Contains("_FeederTop_") || n.Contains("_WovenRug_");
        static Vector3 Expand(Vector3 p)=>new Vector3(p.x*Factor,p.y,p.z*Factor);
        static Bounds Expand(Bounds b)=>new Bounds(Expand(b.center),new Vector3(b.size.x*Factor,b.size.y,b.size.z*Factor));
        static bool IsSoft(string n)=>n.Contains("Rug_") || n.Contains("Mat_") || n.Contains("_SleepingPillow_") || n.Contains("_DecorCushion") || n.Contains("_Bedding_") ||
            n.Contains("_DrapedThrow_") || n.Contains("_WovenRug_") || n.Contains("_EntryMat_") || n.Contains("_RoundCatMat_");
        static bool Dynamic(Item item)=>!IsSoft(item.id) && (item.id.Contains("_PRP") && !item.id.Contains("_ScratchPad_") ||
            item.id.Contains("_SisalBall_") || item.id.Contains("_ScratchBall_") || item.id.Contains("_BedsideLamp_") || item.id.Contains("_Laptop_"));

        static void AddProps(List<Item> items)
        {
            var material=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Generated/BedroomPropFriction.physicMaterial");
            foreach(var item in items.Where(Dynamic))
            {
                foreach(var t in item.root.GetComponentsInChildren<Transform>())GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
                foreach(var r in item.parts)
                {
                    foreach(var old in r.GetComponents<Collider>())UnityEngine.Object.DestroyImmediate(old);
                    string n=Regex.Match(r.name,@"_P\d+_(.+)_E\d+$").Groups[1].Value;
                    if(n.Contains("Leaf") || n.Contains("Stem") || n.Contains("Vine") || n.Contains("Grass") || n.Contains("WaterSurface") || n.Contains("PagesEdge"))continue;
                    Collider collider;
                    if(item.id.Contains("_SisalBall_"))
                    {
                        var sphere=r.gameObject.AddComponent<SphereCollider>();var m=r.GetComponent<MeshFilter>().sharedMesh;
                        sphere.center=m.bounds.center;sphere.radius=Mathf.Max(m.bounds.extents.x,m.bounds.extents.y,m.bounds.extents.z);collider=sphere;
                    }
                    else
                    {
                        var box=r.gameObject.AddComponent<BoxCollider>();var m=r.GetComponent<MeshFilter>().sharedMesh;
                        box.center=m.bounds.center;box.size=m.bounds.size;collider=box;
                    }
                    collider.sharedMaterial=material;collider.contactOffset=.0015f;
                }
                if(item.root.GetComponentsInChildren<Collider>().Length==0)
                {
                    var current=item.parts[0].bounds;foreach(var r in item.parts.Skip(1))current.Encapsulate(r.bounds);
                    var box=item.root.gameObject.AddComponent<BoxCollider>();box.center=item.root.InverseTransformPoint(current.center);
                    box.size=current.size;box.sharedMaterial=material;box.contactOffset=.0015f;
                }
                var body=item.root.gameObject.AddComponent<Rigidbody>();
                body.mass=item.id.Contains("_SisalBall_") ? .045f : item.id.Contains("Plant_") ? .65f : item.id.Contains("Lamp_") ? .8f : item.id.Contains("Laptop_") ? 1.8f : .22f;
                body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                body.linearDamping=.10f;body.angularDamping=.25f;body.maxDepenetrationVelocity=.7f;body.solverIterations=12;body.solverVelocityIterations=6;
                var basePart=item.parts.FirstOrDefault(r=>r.name.Contains("CeramicPot") || r.name.Contains("RoundBase"));
                if(basePart)body.centerOfMass=item.root.InverseTransformPoint(basePart.bounds.center);
                item.root.gameObject.AddComponent<PushableProp>();
            }
        }

        static void AddSoftSurfaces(List<Item> items,Transform support)
        {
            const string path="Assets/Generated/Home90SoftMeshes.asset";
            if(AssetDatabase.LoadMainAssetAtPath(path))AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(new Mesh{name="Home soft surface meshes"},path);
            foreach(var item in items)
            foreach(var r in item.parts)
            {
                bool soft=r.name.Contains("_SeatCushion_") || r.name.Contains("_Mattress_") || r.name.Contains("_CottonPillow_") ||
                    r.name.Contains("_SageQuilt_") || r.name.Contains("_CreamSheet_") || r.name.Contains("_FoldedQuiltEdge_") || r.name.Contains("_DecorCushion_") ||
                    r.name.Contains("_SeatPad_") || r.name.Contains("_CreamBedPad_") || r.name.Contains("_KnittedPouf_");
                if(!soft)continue;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);
                var mesh=HomeSurfaceSubdivision.DenseTop(r.GetComponent<MeshFilter>().sharedMesh,r.transform,
                    r.name.Contains("Quilt") || r.name.Contains("Sheet") || r.name.Contains("Pillow") || r.name.Contains("DecorCushion"));
                AssetDatabase.AddObjectToAsset(mesh,path);r.GetComponent<MeshFilter>().sharedMesh=mesh;
                var collider=r.GetComponent<MeshCollider>();if(!collider)collider=r.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
                var deformation=r.gameObject.AddComponent<SoftPillow>();deformation.maximumSink=r.name.Contains("Pillow") ? .045f : .035f;
                var bed=item.id.Contains("Bed_")?item:item.support;
                var mattress=bed?.parts.FirstOrDefault(p=>p.name.Contains("_Mattress_"));
                if(mattress)
                {
                    deformation.useSharedHeightRange=true;deformation.deformationBaseY=mattress.bounds.min.y;deformation.deformationTopY=mattress.bounds.max.y;
                    deformation.activationCeilingY=mattress.bounds.max.y+.3f;
                }
            }
            if(support)
            {
                var collider=support.GetComponent<BoxCollider>();var b=collider.bounds;
                float top=b.max.y-.045f;support.position+=Vector3.down*.0225f;collider.size=new Vector3(collider.size.x,collider.size.y-.045f,collider.size.z);
            }
            AssetDatabase.SaveAssets();
        }
        static void Settle()
        {
            Physics.SyncTransforms();var old=Physics.simulationMode;
            try {Physics.simulationMode=SimulationMode.Script;for(int i=0;i<360;i++)Physics.Simulate(1f/120);}
            finally {Physics.simulationMode=old;}
            foreach(var body in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None)){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.Sleep();}
            Physics.SyncTransforms();
        }
        static void Validate(List<Item> items,GameObject home)
        {
            var legs=items.Where(i=>i.id.Contains("_DiningTable_") || i.id.Contains("_DiningChair_") || i.id.Contains("_DiningBench_")).ToArray();
            var collisions=new List<string>();
            for(int a=0;a<legs.Length;a++)for(int b=a+1;b<legs.Length;b++)
            foreach(var x in legs[a].parts.Where(p=>p.name.Contains("Leg_")))foreach(var y in legs[b].parts.Where(p=>p.name.Contains("Leg_")))
                if(x.bounds.Intersects(y.bounds))collisions.Add(x.name+" / "+y.name);
            Directory.CreateDirectory("../.tools/home-upgrade");
            File.WriteAllText("../.tools/home-upgrade/build-layout.txt","Expansion: "+Factor+"\nDynamic props: "+home.GetComponentsInChildren<Rigidbody>().Length+
                "\nSoft surfaces: "+home.GetComponentsInChildren<SoftPillow>().Length+"\nDining intersections: "+collisions.Count+"\n"+string.Join("\n",collisions));
            if(collisions.Count>0)throw new InvalidOperationException("Dining furniture still intersects");
            var tipped=items.Where(i=>Dynamic(i) && (i.id.Contains("Plant_") || i.id.Contains("GrassPot_")) && Vector3.Angle(i.root.up,Vector3.up)>15).ToArray();
            if(tipped.Length>0)throw new InvalidOperationException("Initial plant placement is unstable: "+string.Join(",",tipped.Select(i=>i.root.name)));
        }
    }
}
