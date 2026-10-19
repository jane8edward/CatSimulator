using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CatSimulator.Editor
{
    public static class HouseTasksIntegration
    {
        const string ScenePath="Assets/Scenes/New_House_Preview.unity";
        const string Folder="Assets/Art/NewHouse/Gameplay/HouseTasks";
        static Transform root;
        static LevelSession session;
        static NewHouseItem[] items;
        static Material outline;
        public static void PrepareAndBuild(){Prepare();FirstPlayableBuilder.Build();}
        public static void FinalizeAndBuild()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var tasks=UnityEngine.Object.FindObjectsByType<HouseTaskInteractable>(FindObjectsSortMode.None);
            var shoes=tasks.Single(t=>t.kind==HouseTaskInteractable.Kind.Slippers);
            shoes.sparkMaterial=Mat("UrineStream",new Color(.65f,.57f,.2f,.55f),0,.9f,true,true);
            var laptop=tasks.Single(t=>t.kind==HouseTaskInteractable.Kind.Laptop);var b=laptop.GetComponent<NewHouseItem>().Bounds;
            laptop.actionPosition.position=new Vector3(b.min.x+.095f,.762f,b.center.z+.095f);
            laptop.anchor.position=laptop.actionPosition.position;
            var phone=tasks.Single(t=>t.kind==HouseTaskInteractable.Kind.Phone);phone.sparkMaterial.shader=Shader.Find("CatSimulator/CableSpark");EditorUtility.SetDirty(phone.sparkMaterial);
            ClearClimbLanding();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();FirstPlayableBuilder.Build();
        }
        public static void Prepare()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            if(GameObject.Find("NH_HouseTasks"))throw new InvalidOperationException("House tasks already authored; build saved scene without preparing again.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            session=UnityEngine.Object.FindFirstObjectByType<LevelSession>();
            items=UnityEngine.Object.FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None);
            outline=AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Outline.mat");
            root=new GameObject("NH_HouseTasks").transform;
            Robot();Laptop();Slippers();Phone();Food();Climb();
            ClearClimbLanding();
            session.definition.requiredObjectives=new[]{"bite-laptop","wet-slippers","climb-house-tree","cut-phone-cable","eat-cat-food"};
            session.definition.displayName="温馨大宅";
            session.definition.objectiveLabels=new[]{"咬坏书房电脑的一角","在玄关拖鞋里撒尿","爬到猫爬架顶端","咬断床头手机充电线","吃一口客厅猫粮"};
            EditorUtility.SetDirty(session.definition);
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("HOUSE_TASKS_AUTHORED");
        }
        static NewHouseItem Item(string id)=>items.Single(i=>i.placement.Contains(id));
        static void ClearClimbLanding()
        {
            var all=UnityEngine.Object.FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None);
            var plant=all.Single(i=>i.placement.Contains("LIV_NH029_PlantTrailing_01"));
            if(plant.transform.position.y<1)return;
            var console=all.Single(i=>i.placement.Contains("LIV_NH071"));
            var oldBase=plant.hasContactBase?plant.transform.TransformPoint(plant.contactBaseLocal):new Vector3(plant.transform.position.x,2.216f,plant.transform.position.z);
            plant.transform.position+=new Vector3(-11.49f,console.Bounds.max.y+.002f,-5.115f)-oldBase;
            plant.transform.SetParent(console.transform,true);plant.support=console;
        }
        static Material Mat(string name,Color color,float metallic=0,float smooth=.4f,bool transparent=false,bool unlit=false)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m)return m;
            m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name,color=color};
            m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
            if(transparent){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;}
            AssetDatabase.CreateAsset(m,path);return m;
        }
        static Transform Point(string name,Transform parent,Vector3 p)
        {var go=new GameObject(name).transform;go.SetParent(parent,true);go.position=p;return go;}
        static GameObject Part(string name,Transform parent,PrimitiveType shape,Vector3 p,Vector3 size,Material mat,bool contact=false)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,true);go.transform.position=p;go.transform.localScale=size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;
            if(contact){var c=go.AddComponent<MeshCollider>();c.sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;c.convex=true;c.contactOffset=.001f;}
            return go;
        }
        static HouseTaskInteractable Task(Transform parent,HouseTaskInteractable.Kind kind,string id,string prompt,Vector3 action,Vector3 target)
        {
            var task=parent.gameObject.AddComponent<HouseTaskInteractable>();task.kind=kind;task.interactionId=id;task.prompt=prompt;task.session=session;
            task.actionPosition=Point("ActionPosition",parent,action);task.focus=Point("ActionFocus",parent,target);
            task.anchor=Point("E_InteractionAnchor",parent,action);task.radius=kind==HouseTaskInteractable.Kind.Phone||kind==HouseTaskInteractable.Kind.Laptop?1.05f:.8f;
            return task;
        }
        static LineRenderer Line(string name,Transform parent,Vector3[] points,Material mat,float width)
        {
            var line=Point(name,parent,Vector3.zero).gameObject.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=points.Length;line.SetPositions(points);
            line.widthMultiplier=width;line.numCapVertices=3;line.numCornerVertices=3;line.sharedMaterial=mat;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return line;
        }
        static void Robot()
        {
            var robot=Point("SilverRobotVacuum",root,new Vector3(-11.46f,.0105f,-2.96f)).gameObject.AddComponent<RobotVacuum>();
            robot.definition=session.definition;robot.transform.rotation=Quaternion.Euler(0,90,0);
            var body=robot.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            var silver=Mat("VacuumSilver",new Color(.66f,.7f,.74f),.8f,.55f);var dark=Mat("VacuumBumper",new Color(.12f,.14f,.16f),.15f);
            var center=robot.transform.position;
            Part("Silver body",robot.transform,PrimitiveType.Cylinder,center+Vector3.up*RobotVacuum.Height*.5f,new Vector3(.36f,RobotVacuum.Height*.5f,.36f),silver,true);
            Part("Bumper",robot.transform,PrimitiveType.Cylinder,center+Vector3.up*.014f,new Vector3(.366f,.009f,.366f),dark);
            Part("Seat pad",robot.transform,PrimitiveType.Cylinder,center+Vector3.up*(RobotVacuum.Height+.001f),new Vector3(.27f,.002f,.27f),dark);
            Part("Forward indicator",robot.transform,PrimitiveType.Cube,center+robot.transform.forward*.125f+Vector3.up*(RobotVacuum.Height+.004f),new Vector3(.045f,.006f,.035f),Mat("VacuumIndicator",new Color(.08f,.65f,.85f),.3f));
            // A small dock makes the saved living room wall position legible as furniture.
            Part("VacuumWallDock",root,PrimitiveType.Cube,new Vector3(-11.69f,.032f,-2.96f),new Vector3(.055f,.055f,.26f),dark);
        }
        struct Vertex
        {
            public Vector3 p,n;public Vector2 uv;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)};
        }
        static Mesh BiteMesh(MeshFilter mf,Bounds bounds)
        {
            var source=mf.sharedMesh;var vertices=source.vertices;var normals=source.normals;var uv=source.uv;
            var points=new List<Vector3>();var normalList=new List<Vector3>();var uvs=new List<Vector2>();var slots=new List<int[]>();
            // Viewed from the keyboard, screen-right is the negative world-X side.
            Func<Vertex,float> side=v=>{var w=mf.transform.TransformPoint(v.p);return w.x-bounds.min.x+bounds.max.y-w.y-.064f;};
            for(int slot=0;slot<source.subMeshCount;slot++)
            {
                var indices=new List<int>();var tris=source.GetTriangles(slot);
                for(int t=0;t<tris.Length;t+=3)
                {
                    var poly=new List<Vertex>();for(int j=0;j<3;j++){int k=tris[t+j];poly.Add(new Vertex{p=vertices[k],n=normals[k],uv=uv[k]});}
                    var clipped=new List<Vertex>();
                    for(int j=0;j<poly.Count;j++)
                    {
                        var a=poly[j];var b=poly[(j+1)%poly.Count];float da=side(a),db=side(b);
                        if(da>=0)clipped.Add(a);if((da<0)!=(db<0))clipped.Add(Vertex.Lerp(a,b,da/(da-db)));
                    }
                    for(int j=1;j<clipped.Count-1;j++)foreach(var v in new[]{clipped[0],clipped[j],clipped[j+1]})
                    {indices.Add(points.Count);points.Add(v.p);normalList.Add(v.n);uvs.Add(v.uv);}
                }
                slots.Add(indices.ToArray());
            }
            var mesh=new Mesh{name="LaptopBittenUpperRight",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(points);mesh.SetNormals(normalList);mesh.SetUVs(0,uvs);mesh.subMeshCount=slots.Count;
            for(int s=0;s<slots.Count;s++)mesh.SetTriangles(slots[s],s);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/LaptopBitten.asset");return mesh;
        }
        static void Laptop()
        {
            var item=Item("Laptop");var b=item.Bounds;var mf=item.GetComponentInChildren<MeshFilter>();var original=mf.GetComponent<MeshRenderer>();
            var task=Task(item.transform,HouseTaskInteractable.Kind.Laptop,"bite-laptop","咬电脑屏幕右上角",new Vector3(b.min.x+.095f,.762f,b.center.z+.095f),new Vector3(b.min.x+.045f,b.max.y-.045f,b.min.z+.02f));
            var group=Point("DamagedLaptop",item.transform,Vector3.zero);task.damaged=group.gameObject;task.intact=original.gameObject;
            var go=new GameObject("DamagedLaptopBody");go.transform.SetParent(group,false);go.transform.SetPositionAndRotation(mf.transform.position,mf.transform.rotation);go.transform.localScale=mf.transform.lossyScale;
            var mesh=BiteMesh(mf,b);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=original.sharedMaterials;
            var contact=go.AddComponent<MeshCollider>();contact.sharedMesh=mesh;contact.convex=true;
            var crack=Mat("CrackedGlass",new Color(.7f,.9f,1,.95f),0,.8f,false,true);
            var glassCollider=mf.GetComponent<Collider>();
            Func<float,float,Vector3> screen=(x,y)=>
            {
                var point=new Vector3(x,y,b.center.z+.3f);
                if(glassCollider && glassCollider.Raycast(new Ray(point,Vector3.back),out var hit,1))return hit.point+Vector3.forward*.003f;
                return new Vector3(x,y,b.min.z+.012f+(b.max.y-y)*.25f);
            };
            var origin=screen(b.min.x+.049f,b.max.y-.046f);
            foreach(var end in new[]{new Vector2(b.center.x+.05f,b.min.y+.055f),new Vector2(b.center.x+.10f,b.max.y-.055f),new Vector2(b.min.x+.04f,b.min.y+.075f)})
            {
                var midpoint=screen(Mathf.Lerp(origin.x,end.x,.46f)+.008f,Mathf.Lerp(origin.y,end.y,.46f));
                var endpoint=screen(end.x,end.y);Line("DamagedScreenCrack",group,new[]{origin,midpoint,endpoint},crack,.0017f);
                Line("DamagedCrackBranch",group,new[]{midpoint,screen(midpoint.x+.035f,midpoint.y-.018f)},crack,.0009f);
            }
            group.gameObject.SetActive(false);task.SetUpHighlight(outline);
        }
        static GameObject Puddle(string name,Vector3 p,Vector2 size,Material mat,Transform parent)
        {
            var go=Point(name,parent,p).gameObject;var vertices=new Vector3[34];var triangles=new int[96];
            for(int i=0;i<=32;i++){float a=i*Mathf.PI/16,r=1+.07f*Mathf.Sin(a*5);vertices[i+1]=new Vector3(Mathf.Cos(a)*size.x*r,0,Mathf.Sin(a)*size.y*r);if(i<32){triangles[i*3]=0;triangles[i*3+1]=i+2;triangles[i*3+2]=i+1;}}
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return go;
        }
        static void Slippers()
        {
            var shoes=items.Where(i=>i.placement.Contains("Slipper")).ToArray();var group=Point("SlipperInteraction",root,Vector3.zero);
            foreach(var shoe in shoes)shoe.transform.SetParent(group,true);
            var target=shoes[0].Bounds.center;var task=Task(group,HouseTaskInteractable.Kind.Slippers,"wet-slippers","在拖鞋里撒尿",new Vector3(-2.21f,.014f,-9.055f),target);
            task.wetRenderers=shoes.SelectMany(s=>s.GetComponentsInChildren<MeshRenderer>()).ToArray();
            task.wetMaterial=Mat("WetSlipperFabric",new Color(.20f,.25f,.26f),0,.94f);
            task.wetMaterial.CopyPropertiesFromMaterial(task.wetRenderers[0].sharedMaterial);task.wetMaterial.color*=new Color(.55f,.55f,.55f,1);task.wetMaterial.SetFloat("_Smoothness",.93f);
            task.sparkMaterial=Mat("UrineStream",new Color(.65f,.57f,.2f,.55f),0,.9f,true,true);
            var stains=Point("DamagedWetShoesAndUrine",group,Vector3.zero);task.effect=stains.gameObject;
            var liquid=Mat("UrineWetSurface",new Color(.58f,.49f,.12f,.48f),0,.97f,true);
            Puddle("UrineFloorPuddle",new Vector3(-2.24f,.0045f,-9.12f),new Vector2(.225f,.19f),liquid,stains);
            for(int i=0;i<shoes.Length;i++)Puddle("WetSlipperInside"+i,new Vector3(shoes[i].Bounds.center.x,.017f,-9.16f),new Vector2(.045f,.073f),liquid,stains);
            stains.gameObject.SetActive(false);task.SetUpHighlight(outline);
        }
        static void Phone()
        {
            Item("BedsideLamp_02").gameObject.SetActive(false);
            var table=Item("BedsideTable_02");var b=table.Bounds;var center=new Vector3(b.center.x,.487f,b.center.z);
            var phone=Point("BedsideChargingPhone",root,center);
            var black=Mat("PhoneFrame",new Color(.035f,.042f,.05f),.65f,.7f);var white=Mat("ChargerWhite",new Color(.9f,.91f,.87f),0,.5f);
            Part("PhoneBody",phone,PrimitiveType.Cube,center,new Vector3(.077f,.012f,.156f),black,true);
            Part("PhoneScreen",phone,PrimitiveType.Cube,center+Vector3.up*.0065f,new Vector3(.067f,.001f,.139f),Mat("PhoneScreen",new Color(.015f,.035f,.047f),0,.9f));
            var indicator=Point("ChargingBatteryIndicator",phone,center+Vector3.up*.0075f);
            Part("BatteryOutline",indicator,PrimitiveType.Cube,indicator.position,new Vector3(.030f,.001f,.059f),white);
            Part("BatteryGreen",indicator,PrimitiveType.Cube,indicator.position+Vector3.up*.0007f,new Vector3(.023f,.001f,.047f),Mat("BatteryGreen",new Color(.15f,.95f,.4f),0,.5f,false,true));
            Line("ChargingBolt",indicator,new[]{center+new Vector3(.005f,.009f,.014f),center+new Vector3(-.004f,.009f,0),center+new Vector3(.003f,.009f,0),center+new Vector3(-.005f,.009f,-.014f)},white,.002f);
            var socketPoint=new Vector3(center.x,.65f,-4.735f);
            if(Physics.Raycast(new Vector3(center.x,.65f,-4.55f),Vector3.back,out var wall,.5f,~(1<<8)))socketPoint=wall.point+Vector3.forward*.008f;
            Part("WallSocketPlate",phone,PrimitiveType.Cube,socketPoint,new Vector3(.085f,.115f,.012f),white);
            foreach(float x in new[]{-.009f,.009f})Part("SocketHole",phone,PrimitiveType.Cube,socketPoint+new Vector3(x,.027f,.007f),new Vector3(.005f,.015f,.003f),black);
            Part("ChargerPlugInSocket",phone,PrimitiveType.Cube,socketPoint+new Vector3(0,-.017f,.027f),new Vector3(.043f,.045f,.044f),white);
            var port=center+new Vector3(0,0,-.080f);var cut=port+new Vector3(0,-.003f,-.055f);
            var task=Task(phone,HouseTaskInteractable.Kind.Phone,"cut-phone-cable","咬断手机充电线",new Vector3(center.x,.493f,center.z+.11f),cut);
            task.effect=indicator.gameObject;task.sparkMaterial=Mat("CableSparks",new Color(1,.8f,.25f),0,.5f,false,true);
            task.sparkMaterial.shader=Shader.Find("CatSimulator/CableSpark");
            var cablePoints=new[]{socketPoint+new Vector3(0,-.039f,.031f),socketPoint+new Vector3(0,-.09f,.06f),new Vector3(center.x,.40f,-4.55f),cut,port};
            task.intact=Line("IntactChargingCable",phone,cablePoints,white,.0045f).gameObject;
            var severed=Point("DamagedSeveredCable",phone,Vector3.zero);task.damaged=severed.gameObject;
            Line("DamagedWallCable",severed,new[]{cablePoints[0],cablePoints[1],cablePoints[2],cut+new Vector3(-.023f,-.07f,-.015f)},white,.0045f);
            Line("DamagedPhoneCable",severed,new[]{port,cut+new Vector3(.019f,-.005f,.025f)},white,.0045f);
            var copper=Mat("ExposedCopper",new Color(.72f,.31f,.10f),.75f);
            Part("DamagedCopperTip",severed,PrimitiveType.Sphere,cut+new Vector3(-.023f,-.07f,-.015f),Vector3.one*.006f,copper);
            severed.gameObject.SetActive(false);task.SetUpHighlight(outline);
        }
        static void Food()
        {
            var bowl=Item("FeedingStation");var b=bowl.Bounds;var foodCenter=new Vector3(b.center.x-.085f,.09f,b.center.z);
            var task=Task(bowl.transform,HouseTaskInteractable.Kind.Food,"eat-cat-food","吃猫粮",new Vector3(foodCenter.x,.013f,foodCenter.z+.22f),foodCenter);
            var food=Point("CatFoodPieces",bowl.transform,Vector3.zero);task.effect=food.gameObject;
            var brown=Mat("CatFood",new Color(.27f,.12f,.045f));
            for(int i=0;i<25;i++){float angle=i*2.4f,r=.049f*Mathf.Sqrt((i+.5f)/25);Part("Kibble",food,PrimitiveType.Sphere,foodCenter+new Vector3(Mathf.Cos(angle)*r,(i%3)*.002f,Mathf.Sin(angle)*r),new Vector3(.012f,.008f,.013f),brown);}
            task.SetUpHighlight(outline);
        }
        static void Climb()
        {
            var tree=Item("ScratchColumn");var component=tree.gameObject.AddComponent<ClimbInteractable>();component.interactionId="climb-house-tree";component.prompt="爬到猫爬架顶端";component.session=session;component.radius=1;
            component.anchor=Point("ClimbInteractionAnchor",tree.transform,new Vector3(-11.13f,.15f,-6.1f));
            var route=new List<Transform>();route.Add(Point("ClimbApproach",tree.transform,new Vector3(-11.23f,.012f,-5.72f)));
            for(int i=1;i<=5;i++){var platform=Item("CatPlatform_"+i+"_");var b=platform.Bounds;route.Add(Point("ClimbStep"+i,tree.transform,new Vector3(b.center.x+.035f,b.max.y+.012f,b.center.z)));}
            component.route=route.ToArray();component.SetUpHighlight(outline);
        }
        public static void Inspect()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var lines=UnityEngine.Object.FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None).OrderBy(i=>i.placement)
                .Select(i=>i.placement+" pos="+i.transform.position.ToString("F3")+" bounds="+i.Bounds.ToString("F3")+" rot="+i.transform.eulerAngles.ToString("F1")+" parts="+string.Join(";",i.GetComponentsInChildren<MeshRenderer>().Select(r=>r.name+" "+r.bounds.ToString("F3")+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"null")))));
            Directory.CreateDirectory("../Source/Verification/HouseTasks");
            File.WriteAllLines("../Source/Verification/HouseTasks/scene-inventory.txt",lines);
            var laptop=UnityEngine.Object.FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None).Single(i=>i.placement.Contains("Laptop"));
            var mf=laptop.GetComponentInChildren<MeshFilter>();
            File.WriteAllLines("../Source/Verification/HouseTasks/laptop-vertices.txt",mf.sharedMesh.vertices.Select(v=>mf.transform.TransformPoint(v)).Where(v=>v.y>.90f).Select(v=>v.ToString("F4")).Distinct());
            var bed=UnityEngine.Object.FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None).Single(i=>i.placement.Contains("NH050"));
            File.WriteAllLines("../Source/Verification/HouseTasks/bed-colliders.txt",bed.GetComponentsInChildren<Collider>().Select(c=>c.name+" "+c.bounds.ToString("F3")));
        }
    }
}
