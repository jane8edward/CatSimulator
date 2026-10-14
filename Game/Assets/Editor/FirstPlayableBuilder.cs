using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEditor.Build;

namespace CatSimulator.Editor
{
    public static class FirstPlayableBuilder
    {
        [Serializable] class Palette { public Entry[] materials; }
        [Serializable] class Entry { public string name; public float[] color; public float roughness; public int kind; }
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        const string Generated="Assets/Generated";

        [MenuItem("Cat Simulator/Create First Playable")]
        public static void Create()
        {
            Debug.Log("BUILD_STAGE: Rendering");
            Directory.CreateDirectory(Generated);Directory.CreateDirectory("Assets/Scenes");AssetDatabase.Refresh();
            SetUpRendering();CreateMaterials();ImportModels();
            Debug.Log("BUILD_STAGE: Scene");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var room=InstantiateRoom();
            var session=new GameObject("LevelSession").AddComponent<LevelSession>();
            var definition=ScriptableObject.CreateInstance<LevelDefinition>();definition.spawnPosition=new Vector3(-.8125f,.06f,1f);definition.spawnYaw=180;
            definition.activityBounds=new Bounds(new Vector3(0,1.5f,0),new Vector3(8.7f,4,7.45f));
            definition=SaveAsset(definition,Generated+"/LivingRoomLevel.asset");session.definition=definition;
            var player=new GameObject("PlayerCat");player.layer=8;player.transform.position=definition.spawnPosition;player.transform.rotation=Quaternion.Euler(0,definition.spawnYaw,0);
            var cc=player.AddComponent<CharacterController>();cc.height=.40f;cc.radius=.14f;cc.center=new Vector3(0,.211f,0);cc.stepOffset=.07f;cc.skinWidth=.01f;
            var cat=player.AddComponent<CatController>();cat.session=session;cat.walkSpeed=.86f;cat.runSpeed=1.8f;
            GameObject visual;
            if(BlendSwapCatIntegration.Available)
            {
                BlendSwapCatIntegration.PrepareAssets();BlendSwapCatIntegration.AttachVisual(cat);visual=cat.visual.gameObject;
            }
            else
            {
            visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/ReferenceCat.fbx"),player.transform);
            visual.name="ReferenceCat";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.Euler(0,180,0);
            visual.transform.localScale=Vector3.one*.75f;
            foreach(var t in visual.GetComponentsInChildren<Transform>()) t.gameObject.layer=8;
            ReplaceMaterials(visual);cat.visual=visual.transform;
            Debug.Log("BUILD_STAGE: Animator");
            var anim=visual.GetComponent<Animator>();if(anim==null) anim=visual.AddComponent<Animator>();
            anim.avatar=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/ReferenceCat.fbx").OfType<Avatar>().FirstOrDefault();
            anim.runtimeAnimatorController=CreateAnimations();anim.applyRootMotion=false;cat.animator=anim;
            }
            var camObject=new GameObject("Main Camera");camObject.tag="MainCamera";
            var camera=camObject.AddComponent<Camera>();camera.fieldOfView=53;camera.nearClipPlane=.025f;camera.farClipPlane=60;
            camera.backgroundColor=new Color(.72f,.8f,.88f);camera.clearFlags=CameraClearFlags.Skybox;
            camObject.AddComponent<AudioListener>();var follow=camObject.AddComponent<CatCamera>();follow.cat=cat;follow.yaw=180;
            camObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;cat.cameraTransform=camObject.transform;
            AddInteractions(room,session);
            if(BlendSwapCatIntegration.Available) BlendSwapCatIntegration.FitInteractions();
            Debug.Log("BUILD_STAGE: Lighting");
            var hud=new GameObject("GameHUD").AddComponent<GameHud>();hud.cat=cat;hud.session=session;
            var test=new GameObject("DevelopmentVerification").AddComponent<PlayableVerification>();test.cat=cat;test.session=session;test.cameraRig=follow;
            CreateLighting(definition);
            if(room.GetComponentsInChildren<Transform>().Any(t=>t.name=="BedroomCenter"))
            {
                SetBedroomBounds(definition);
                ConfigureBedroomLighting(room);
                test.gameObject.AddComponent<BedroomVerification>();
                test.gameObject.AddComponent<BedroomPhysicsVerification>();
            }
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/LivingRoom.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/LivingRoom.unity",true)};
            PlayerSettings.companyName="Jamari";PlayerSettings.productName="Cat Simulator";
            PlayerSettings.bundleVersion=room.GetComponentsInChildren<Transform>().Any(t=>t.name=="BedroomCenter")?"0.1.5":BlendSwapCatIntegration.Available?"0.1.3":"0.1.1";
            PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;PlayerSettings.resizableWindow=true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            QualitySettings.vSyncCount=0;QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler");if(input!=null) { input.intValue=0;settings.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.SaveAssets();
            var evidence=new { spawn=definition.spawnPosition, head=Find(visual,"Head").position, roomRenderers=room.GetComponentsInChildren<Renderer>().Length,
                clips=AssetDatabase.LoadAllAssetsAtPath(BlendSwapCatIntegration.Available?BlendSwapCatIntegration.Model:"Assets/Art/ReferenceCat.fbx").OfType<AnimationClip>().Select(c=>c.name).ToArray() };
            Debug.Log("FIRST_PLAYABLE_CREATED: "+JsonUtility.ToJson(definition));
            File.WriteAllText(Path.GetFullPath("../Source/scene-import-check.txt"),"Spawn: "+definition.spawnPosition+"\nHead: "+Find(visual,"Head").position+"\nClips: "+string.Join(",",evidence.clips));
        }

        public static void Build()
        {
            // Build the saved level; artist edits must survive ordinary rebuilds.
            if(!File.Exists("Assets/Scenes/LivingRoom.unity")) Create();
            Directory.CreateDirectory("../Builds/Windows");
            var scenes=new[]{"Assets/Scenes/LivingRoom.unity","Assets/Cat/Scenes/Walk_Preview.unity","Assets/Scenes/Home90_Preview.unity"};
            foreach(var path in scenes)
                if(!File.Exists(path)) throw new FileNotFoundException("Build scene missing",path);
            var report=UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=scenes,
                locationPathName="../Builds/Windows/CatSimulator.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            File.WriteAllText("../Source/build-result.txt",report.summary.result+"\nBytes: "+report.summary.totalSize+"\nTime: "+report.summary.totalTime);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Windows build failed");
            if(File.Exists("../THIRD_PARTY_NOTICES.md")) File.Copy("../THIRD_PARTY_NOTICES.md","../Builds/Windows/THIRD_PARTY_NOTICES.md",true);
        }

        [MenuItem("Cat Simulator/Apply Bedroom Expansion")]
        public static void ApplyBedroomExpansion()
        {
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset("Assets/Art/LivingRoom.fbx",ImportAssetOptions.ForceUpdate);
            var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/Art/LivingRoom.fbx");
            if(!importer.isReadable) { importer.isReadable=true;importer.SaveAndReimport(); }
            CreateMaterials();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/LivingRoom.unity");
            var session=UnityEngine.Object.FindFirstObjectByType<LevelSession>();
            var cat=UnityEngine.Object.FindFirstObjectByType<CatController>();
            if(session==null || cat==null) throw new InvalidOperationException("The saved level must contain its player and session.");
            var previousVisual=cat.visual;
            var previousAnimator=cat.animator;
            var previousController=cat.animator.runtimeAnimatorController;
            var previousRoom=GameObject.Find("LivingRoom");
            var room=InstantiateRoom();
            // Validate the new model before replacing the saved room; keep the player and camera.
            Find(room,"BedroomCenter");Find(room,"BedroomDoorInside");
            if(previousRoom!=null) UnityEngine.Object.DestroyImmediate(previousRoom);
            AddInteractions(room,session);
            BlendSwapCatIntegration.FitInteractions();
            SetBedroomBounds(session.definition);
            ConfigureBedroomLighting(room);
            if(cat.visual!=previousVisual || cat.animator!=previousAnimator || cat.animator.runtimeAnimatorController!=previousController)
                throw new InvalidOperationException("Room replacement changed the existing cat setup.");
            var verification=UnityEngine.Object.FindFirstObjectByType<PlayableVerification>();
            if(verification!=null && verification.GetComponent<BedroomVerification>()==null)
                verification.gameObject.AddComponent<BedroomVerification>();
            if(verification!=null && verification.GetComponent<BedroomPhysicsVerification>()==null)
                verification.gameObject.AddComponent<BedroomPhysicsVerification>();
            PlayerSettings.bundleVersion="0.1.5";
            EditorUtility.SetDirty(session.definition);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("BEDROOM_EXPANSION_APPLIED: player, camera and cat visual preserved");
        }

        public static void ApplyBedroomExpansionAndBuild() { ApplyBedroomExpansion(); Build(); }

        static GameObject InstantiateRoom()
        {
            var room=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/LivingRoom.fbx"));
            room.name="LivingRoom";
            ReplaceMaterials(room);
            foreach(var t in room.GetComponentsInChildren<Transform>())
            {
                if(t.name.StartsWith("COL_"))
                {
                    if(t.name.StartsWith("COL_Floor") || t.name.StartsWith("COL_Wall_") || t.name=="COL_Back" || t.name=="COL_WindowWall")
                        t.gameObject.AddComponent<BoxCollider>();
                    var renderer=t.GetComponent<Renderer>();
                    if(renderer!=null) renderer.enabled=false;
                }
                else GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic);
            }
            ConfigureRoomCollisions(room);
            if(room.GetComponentsInChildren<Transform>().Any(t=>t.name=="Bedroom_Prop_Phone")) BedroomPhysicsSetup.Apply(room);
            return room;
        }

        static void SetBedroomBounds(LevelDefinition definition)
        {
            // FBX maps Blender's -X extension onto Unity's +X.
            definition.activityBounds=new Bounds(new Vector3(2.8f,1.5f,0),new Vector3(14.3f,4,7.45f));
            EditorUtility.SetDirty(definition);
        }

        static void ConfigureBedroomLighting(GameObject room)
        {
            var previous=GameObject.Find("BedroomLighting");
            if(previous!=null) UnityEngine.Object.DestroyImmediate(previous);
            var root=new GameObject("BedroomLighting");
            foreach(var side in new[]{"Left","Right"})
            {
                var lamp=new GameObject("BedsideWarmLight_"+side).AddComponent<Light>();
                lamp.transform.SetParent(Find(room,"Bedroom_LampLight_"+side));lamp.transform.localPosition=Vector3.zero;
                lamp.type=LightType.Point;lamp.color=new Color(1,.86f,.65f);lamp.intensity=.65f;lamp.range=3.6f;
                lamp.shadows=LightShadows.None;
            }
            var window=new GameObject("BedroomWindowFill").AddComponent<Light>();
            window.transform.SetParent(root.transform);window.transform.position=Find(room,"BedroomWindowLight").position;
            window.type=LightType.Point;window.color=new Color(1,.965f,.90f);window.intensity=3.8f;window.range=6.8f;
            window.shadows=LightShadows.None;
            var bounce=new GameObject("BedroomSoftBounce").AddComponent<Light>();bounce.transform.SetParent(root.transform);
            bounce.transform.position=Find(room,"BedroomCenter").position+Vector3.up*1.2f;
            bounce.type=LightType.Point;bounce.color=new Color(1,.94f,.83f);bounce.intensity=1.25f;bounce.range=5.8f;
            bounce.shadows=LightShadows.None;
            var probe=new GameObject("BedroomReflections").AddComponent<ReflectionProbe>();probe.transform.SetParent(root.transform);
            probe.transform.position=Find(room,"BedroomCenter").position;probe.size=new Vector3(5.6f,3,6.1f);
            probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.resolution=128;
            probe.boxProjection=true;probe.cullingMask=~(1<<8);probe.intensity=.7f;
        }

        static void SetUpRendering()
        {
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            renderer=SaveAsset(renderer,Generated+"/LivingRoomRenderer.asset");
            if(renderer.rendererFeatures.Count==0)
            {
                var ao=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();ao.name="Contact shadows";
                var aoSettings=new SerializedObject(ao);aoSettings.FindProperty("m_Settings.Intensity").floatValue=.7f;
                aoSettings.FindProperty("m_Settings.Radius").floatValue=.12f;
                aoSettings.FindProperty("m_Settings.AfterOpaque").boolValue=true;
                aoSettings.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.AddObjectToAsset(ao,renderer);renderer.rendererFeatures.Add(ao);EditorUtility.SetDirty(renderer);
            }
            var pipeline=UniversalRenderPipelineAsset.Create(renderer);pipeline.msaaSampleCount=4;pipeline.renderScale=1;
            pipeline.supportsHDR=true;pipeline.supportsCameraOpaqueTexture=true;pipeline.supportsCameraDepthTexture=true;
            pipeline.shadowDistance=18;pipeline.mainLightShadowmapResolution=2048;pipeline.shadowCascadeCount=2;pipeline.useSRPBatcher=true;
            var pipelineSettings=new SerializedObject(pipeline);pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue=true;pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            pipeline=SaveAsset(pipeline,Generated+"/LivingRoomURP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        }
        static T SaveAsset<T>(T obj,string path) where T:UnityEngine.Object
        {
            var current=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if(current!=null) { EditorUtility.CopySerialized(obj,current);UnityEngine.Object.DestroyImmediate(obj); }
            else AssetDatabase.CreateAsset(obj,path);
            var stored=AssetDatabase.LoadAssetAtPath<T>(path);
            if(stored is VolumeProfile volume)
            {
                foreach(var component in volume.components)
                    if(component!=null && !AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component,volume);
                EditorUtility.SetDirty(volume);
            }
            return stored;
        }
        static void CreateMaterials()
        {
            materials.Clear();var palette=JsonUtility.FromJson<Palette>(File.ReadAllText("Assets/Art/palette.json"));
            foreach(var entry in palette.materials)
            {
                var mat=new Material(Shader.Find("CatSimulator/CozySurface")){name=entry.name};
                var color=new Color(entry.color[0],entry.color[1],entry.color[2],1);
                mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",1-entry.roughness);mat.SetFloat("_Grain",entry.kind);
                if(entry.name=="Cat_Coat") {mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_VertexColor",1);mat.SetFloat("_Smoothness",.05f);}
                if(entry.name=="Glass_Clear" || entry.name=="Water_Clear")
                {
                    mat.shader=Shader.Find("Universal Render Pipeline/Lit");mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);
                    mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite",0);mat.SetFloat("_Cull",(float)CullMode.Off);mat.SetFloat("_Smoothness",.96f);
                    mat.SetColor("_BaseColor",new Color(color.r,color.g,color.b,entry.name=="Glass_Clear"?.2f:.3f));
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;mat.SetOverrideTag("RenderType","Transparent");
                }
                if(entry.name=="Bedroom_LampGlow" || entry.name=="Bedroom_Screen" || entry.name=="Bedroom_PhoneScreen" || entry.name=="Bedroom_ChargingGreen")
                {
                    mat.shader=Shader.Find("Universal Render Pipeline/Lit");mat.SetColor("_BaseColor",color);
                    mat.SetColor("_EmissionColor",color*(entry.name=="Bedroom_LampGlow"?1.4f:.25f));
                    mat.EnableKeyword("_EMISSION");
                }
                SaveAsset(mat,Generated+"/"+entry.name+".mat");materials[entry.name]=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/"+entry.name+".mat");
            }
            var outline=new Material(Shader.Find("CatSimulator/InteractionOutline"));SaveAsset(outline,Generated+"/Outline.mat");
        }
        static void ReplaceMaterials(GameObject root)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m!=null && materials.TryGetValue(m.name,out var replacement)?replacement:m).ToArray();
                if(renderer.name=="RugFibres" || renderer.name=="CatShortFur") renderer.shadowCastingMode=ShadowCastingMode.Off;
                if(renderer.name=="RugFibres") renderer.enabled=false;
            }
        }
        static void ImportModels()
        {
            var room=(ModelImporter)AssetImporter.GetAtPath("Assets/Art/LivingRoom.fbx");room.globalScale=1;room.isReadable=true;room.SaveAndReimport();
            var cat=(ModelImporter)AssetImporter.GetAtPath("Assets/Art/ReferenceCat.fbx");cat.animationType=ModelImporterAnimationType.Generic;
            cat.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            cat.importAnimation=true;cat.animationCompression=ModelImporterAnimationCompression.Off;cat.optimizeGameObjects=false;
            var clips=cat.defaultClipAnimations;
            foreach(var clip in clips) { clip.loopTime=clip.name.EndsWith("Idle")||clip.name.EndsWith("Walk")||clip.name.EndsWith("Run"); }
            cat.clipAnimations=clips;cat.SaveAndReimport();
        }
        static RuntimeAnimatorController CreateAnimations()
        {
            var path=Generated+"/Cat.controller";if(AssetDatabase.LoadAssetAtPath<AnimatorController>(path)!=null) AssetDatabase.DeleteAsset(path);
            var controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            foreach(var trigger in new[]{"Jump","Land","Paw","Scratch","WalkAction","Idle"}) controller.AddParameter(trigger,AnimatorControllerParameterType.Trigger);
            var clips=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/ReferenceCat.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Clip(string name) => clips.First(c=>c.name.EndsWith(name,StringComparison.Ordinal));
            var sm=controller.layers[0].stateMachine;var locomotion=sm.AddState("Locomotion");sm.defaultState=locomotion;
            var blend=new BlendTree {name="Walk and run",blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(blend,controller);
            blend.AddChild(Clip("Idle"),0);blend.AddChild(Clip("Walk"),1);blend.AddChild(Clip("Run"),2.1f);locomotion.motion=blend;
            foreach(var trigger in new[]{"Jump","Land","Paw","Scratch","WalkAction","Idle"})
            {
                var state=sm.AddState(trigger);state.motion=Clip(trigger=="WalkAction"?"Walk":trigger);
                var enter=sm.AddAnyStateTransition(state);enter.hasExitTime=false;enter.duration=.08f;enter.canTransitionToSelf=true;enter.AddCondition(AnimatorConditionMode.If,0,trigger);
                var exit=state.AddTransition(locomotion);exit.hasExitTime=true;exit.exitTime=.94f;exit.duration=.10f;
            }
            return controller;
        }
        static Transform Find(GameObject root,string name) => root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        static void ConfigureRoomCollisions(GameObject room)
        {
            // One collider per physical mesh preserves gaps under tables and inside open boxes.
            foreach(var filter in room.GetComponentsInChildren<MeshFilter>(true))
            {
                var name=filter.name;
                var renderer=filter.GetComponent<Renderer>();
                if(name.StartsWith("COL_") || name.StartsWith("FloorPlank_") || name=="RugFibres"
                    || name=="WaterInside" || name=="CupGlass" || renderer==null || !renderer.enabled) continue;
                var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
            }
        }
        static void AddInteractions(GameObject room,LevelSession session)
        {
            var outline=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/Outline.mat");
            var tree=Find(room,"CatTree").gameObject.AddComponent<ClimbInteractable>();tree.session=session;tree.interactionId="cat-tree";tree.prompt="爬上猫爬架";
            tree.anchor=Find(room,"TreeInteractAnchor");tree.route=Enumerable.Range(0,5).Select(i=>Find(room,"ClimbRoute_"+i)).ToArray();tree.SetUpHighlight(outline);
            var cup=Find(room,"Cup").gameObject.AddComponent<CupInteractable>();cup.session=session;cup.interactionId="spill-cup";cup.prompt="打翻水杯";
            cup.radius=1.15f;cup.actionPosition=Find(room,"CupActionPosition");cup.waterMaterial=materials["Water_Clear"];
            var body=cup.gameObject.AddComponent<Rigidbody>();body.mass=.13f;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
            foreach(var t in cup.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            var collider=Find(room,"CupGlass").gameObject.AddComponent<MeshCollider>();collider.convex=true;cup.cupBody=body;cup.SetUpHighlight(outline);
            var curtainRoot=new GameObject("CurtainInteraction");curtainRoot.transform.SetParent(room.transform);
            foreach(var name in new[]{"CurtainIntact","CurtainDamaged"}) Find(room,name).SetParent(curtainRoot.transform,true);
            var curtain=curtainRoot.AddComponent<CurtainInteractable>();curtain.session=session;curtain.interactionId="damage-curtain";curtain.prompt="攀爬并抓坏窗帘";
            curtain.anchor=Find(room,"CurtainInteractAnchor");curtain.actionPosition=Find(room,"CurtainActionPosition");
            curtain.intact=Find(room,"CurtainIntact").gameObject;curtain.damaged=Find(room,"CurtainDamaged").gameObject;curtain.damaged.SetActive(false);curtain.SetUpHighlight(outline);
            foreach(var t in curtainRoot.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            for(var i=0;i<2;i++)
            {
                var box=Find(room,"CardboardBox_"+i).gameObject.AddComponent<BoxInteractable>();box.session=session;box.interactionId="cardboard-box";
                box.prompt="钻进纸箱";box.inside=Find(room,"BoxInside_"+i);box.SetUpHighlight(outline);
            }
        }
        static void CreateLighting(LevelDefinition definition)
        {
            new GameObject("AmbientRoomLight").AddComponent<RoomLighting>();
            var sun=new GameObject("AfternoonSun").AddComponent<Light>();sun.type=LightType.Directional;sun.color=definition.daylightColor;
            sun.intensity=1.4f;sun.shadows=LightShadows.Soft;sun.shadowBias=.02f;sun.shadowNormalBias=.06f;sun.transform.eulerAngles=definition.sunlightEuler;
            RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.52f,.59f,.69f);RenderSettings.ambientEquatorColor=new Color(.41f,.36f,.28f);RenderSettings.ambientGroundColor=new Color(.19f,.16f,.12f);
            var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetFloat("_Exposure",.9f);SaveAsset(sky,Generated+"/Sky.mat");RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/Sky.mat");
            var volume=new GameObject("ColorGrade").AddComponent<Volume>();volume.isGlobal=true;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var adjust=profile.Add<ColorAdjustments>();adjust.postExposure.Override(.15f);adjust.contrast.Override(8);adjust.saturation.Override(3);
            SaveAsset(profile,Generated+"/ColorGrade.asset");volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Generated+"/ColorGrade.asset");
            var probe=new GameObject("LivingRoomReflections").AddComponent<ReflectionProbe>();probe.transform.position=new Vector3(0,1.1f,0);
            probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.resolution=128;
            probe.size=new Vector3(8.5f,3,7.25f);probe.boxProjection=true;probe.cullingMask=~(1<<8);probe.intensity=.8f;
        }
    }
}
