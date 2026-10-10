using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

namespace CatSimulator.Editor
{
    public static class BlendSwapCatIntegration
    {
        public const string Folder="Assets/Art/BlendSwap18519";
        public const string Model=Folder+"/AnimatedCat.fbx";
        const string Controller=Folder+"/AnimatedCat.controller";
        const string Prefab=Folder+"/AnimatedCat.prefab";
        public static bool Available => File.Exists(Model);
        [Serializable] class Conversion { public NativeSpeeds nativeSpeeds; public string[] nonDeformBones; }
        [Serializable] class NativeSpeeds { public float Walk,Run; }

        [MenuItem("Cat Simulator/Install BlendSwap Cat")]
        public static void Install()
        {
            PrepareAssets();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/LivingRoom.unity");
            var cat=UnityEngine.Object.FindFirstObjectByType<CatController>();
            if(cat==null) throw new InvalidOperationException("The existing level has no CatController");
            var room=GameObject.Find("LivingRoom");
            var colliderCount=room.GetComponentsInChildren<Collider>(true).Length;
            var before=cat.visual.gameObject;
            UnityEngine.Object.DestroyImmediate(before);
            AttachVisual(cat);
            FitInteractions();
            PlayerSettings.bundleVersion="0.1.3";
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            VerifyImportedAnimations(cat);
            if(room.GetComponentsInChildren<Collider>(true).Length!=colliderCount)
                throw new InvalidOperationException("Replacing the visual changed room colliders");
            File.WriteAllText("../Source/Verification/blendswap18519-install.txt",
                "Installed into existing LivingRoom scene. Room colliders preserved: "+colliderCount+
                "\nHead: "+Find(cat.visual,"Head").position+
                "\nForward dot: "+Vector3.Dot((Find(cat.visual,"Head").position-cat.transform.position).normalized,cat.transform.forward)+
                "\nClips: "+string.Join(", ",Clips().Select(c=>c.name+" ("+c.length+"s)")));
            Debug.Log("BLENDSWAP_CAT_INSTALLED");
        }

        public static void InstallAndBuild() { Install(); FirstPlayableBuilder.Build(); }

        public static void RebuildSpaciousLevelAndBuild()
        {
            FirstPlayableBuilder.Create();
            // Create already prepares and attaches this asset. Import it once.
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Scenes/LivingRoom.unity");
            var cat=UnityEngine.Object.FindFirstObjectByType<CatController>();
            ValidateSkin(cat.visual.gameObject);
            VerifyImportedAnimations(cat);
            FirstPlayableBuilder.Build();
        }

        public static void PrepareAssets()
        {
            if(!Available) throw new FileNotFoundException("Convert cat.blend before installing the cat",Model);
            AssetDatabase.Refresh();
            foreach(var file in Directory.GetFiles(Folder+"/Textures"))
            {
                var importer=AssetImporter.GetAtPath(file.Replace('\\','/')) as TextureImporter;
                if(importer==null) continue;
                importer.textureType=TextureImporterType.Default;
                importer.sRGBTexture=!file.Contains("FurStrandAlpha");
                importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency=file.Contains("FurStrandAlpha");
                importer.mipMapsPreserveCoverage=false;
                importer.alphaTestReferenceValue=.28f;
                importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;
                importer.anisoLevel=8;importer.maxTextureSize=2048;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            var model=(ModelImporter)AssetImporter.GetAtPath(Model);
            model.globalScale=1;model.animationType=ModelImporterAnimationType.Generic;
            model.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            model.importAnimation=true;model.animationCompression=ModelImporterAnimationCompression.Off;
            model.optimizeGameObjects=false;model.importBlendShapes=false;
            model.materialImportMode=ModelImporterMaterialImportMode.None;
            model.importNormals=ModelImporterNormals.Import;model.importTangents=ModelImporterTangents.CalculateMikk;
            model.skinWeights=ModelImporterSkinWeights.Custom;
            model.maxBonesPerVertex=8;model.minBoneWeight=.00001f;
            var clips=model.defaultClipAnimations;
            foreach(var clip in clips)
            {
                clip.loopTime=new[]{"Idle","Walk","Run"}.Any(n=>clip.name.EndsWith(n,StringComparison.Ordinal));
                clip.loopPose=false;clip.keepOriginalPositionXZ=true;clip.keepOriginalPositionY=true;
            }
            model.clipAnimations=clips;model.SaveAndReimport();
            CreateMaterials();CreateController();
            var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            visual.name="BlendSwapCat";
            Configure(visual);
            PrefabUtility.SaveAsPrefabAsset(visual,Prefab);
            UnityEngine.Object.DestroyImmediate(visual);
        }

        public static void AttachVisual(CatController cat)
        {
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),cat.transform);
            visual.transform.localPosition=Vector3.zero;
            visual.transform.localRotation=Quaternion.Euler(0,180,0);
            visual.transform.localScale=Vector3.one;
            cat.visual=visual.transform;cat.animator=visual.GetComponent<Animator>();
            var collision=cat.GetComponent<CharacterController>();
            collision.height=.36f;collision.radius=.126f;collision.center=new Vector3(0,.1899f,0);
            collision.stepOffset=.063f;
            cat.walkSpeed=.78f;cat.runSpeed=1.62f;
        }

        public static void FitInteractions()
        {
            var cup=UnityEngine.Object.FindFirstObjectByType<CupInteractable>();
            if(cup==null) return;
            var direction=Vector3.ProjectOnPlane(cup.cupBody.position-cup.actionPosition.position,Vector3.up).normalized;
            var position=cup.cupBody.position-direction*.22f;
            position.y=cup.actionPosition.position.y;
            cup.actionPosition.position=position;
        }

        static void Configure(GameObject visual)
        {
            foreach(var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=8;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var name=renderer.name;
                var key=name.Contains("FurCards")?"Fur":name.Contains("Whiskers")?"Whisker":
                    name.Contains("EyeMesh_0")?"Eye_0":name.Contains("EyeMesh_1")?"Eye_1":"Coat";
                renderer.sharedMaterials=new[]{AssetDatabase.LoadAssetAtPath<Material>(Folder+"/BS18519_"+key+".mat")};
                if(key=="Fur" || key=="Whisker") renderer.shadowCastingMode=ShadowCastingMode.Off;
                if(renderer is SkinnedMeshRenderer skin) {skin.updateWhenOffscreen=true;skin.quality=SkinQuality.Auto;}
            }
            ValidateSkin(visual);
            var animator=visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            animator.avatar=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().First();
            animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        }

        static void CreateMaterials()
        {
            foreach(var name in new[]{"Coat","Fur","Whisker","Eye_0","Eye_1"})
            {
                var material=new Material(Shader.Find(name=="Fur"?"CatSimulator/OriginalGroom":"Universal Render Pipeline/Lit"));
                material.name="BS18519_"+name;
                if(name=="Coat" || name=="Fur") material.SetTexture("_BaseMap",Texture("CatCoat.png"));
                if(name=="Fur")
                {
                    material.SetTexture("_AlphaMap",Texture("FurStrandAlpha.png"));
                    material.SetFloat("_Cutoff",.28f);material.SetFloat("_Smoothness",.16f);
                }
                else
                {
                    material.SetColor("_BaseColor",name=="Whisker"?new Color(.78f,.75f,.7f):Color.white);
                    material.SetFloat("_Smoothness",name.StartsWith("Eye")?.86f:name=="Whisker"?.25f:.18f);
                    if(name.StartsWith("Eye")) material.SetTexture("_BaseMap",Texture("EyeBaked_"+name.Last()+".png"));
                }
                Save(material,Folder+"/BS18519_"+name+".mat");
            }
        }

        static Texture Texture(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/"+file);
        static AnimationClip[] Clips() => AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();

        static void CreateController()
        {
            var existing=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if(existing!=null) AssetDatabase.DeleteAsset(Controller);
            var controller=AnimatorController.CreateAnimatorControllerAtPath(Controller);
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            foreach(var trigger in new[]{"Jump","Land","Paw","Scratch","WalkAction","Idle"}) controller.AddParameter(trigger,AnimatorControllerParameterType.Trigger);
            var clips=Clips();
            AnimationClip Clip(string name)=>clips.First(c=>c.name.EndsWith(name,StringComparison.Ordinal));
            var stateMachine=controller.layers[0].stateMachine;
            var locomotion=stateMachine.AddState("Locomotion");stateMachine.defaultState=locomotion;
            var blend=new BlendTree{name="Original Walk and Run",blendParameter="Speed",useAutomaticThresholds=false};
            AssetDatabase.AddObjectToAsset(blend,controller);
            blend.AddChild(Clip("Idle"),0);blend.AddChild(Clip("Walk"),1);blend.AddChild(Clip("Run"),1.62f/.78f);
            var speeds=JsonUtility.FromJson<Conversion>(File.ReadAllText(Folder+"/conversion-report.json")).nativeSpeeds;
            var children=blend.children;
            children[1].timeScale=Mathf.Clamp(.78f/speeds.Walk,.7f,3f);
            children[2].timeScale=Mathf.Clamp(1.62f/speeds.Run,.7f,3f);
            blend.children=children;locomotion.motion=blend;
            foreach(var trigger in new[]{"Jump","Land","Paw","Scratch","WalkAction","Idle"})
            {
                var state=stateMachine.AddState(trigger);state.motion=Clip(trigger=="WalkAction"?"Walk":trigger);
                if(trigger=="WalkAction") state.speed=children[1].timeScale;
                var enter=stateMachine.AddAnyStateTransition(state);
                enter.hasExitTime=false;enter.duration=.08f;enter.canTransitionToSelf=true;
                enter.AddCondition(AnimatorConditionMode.If,0,trigger);
                var exit=state.AddTransition(locomotion);exit.hasExitTime=true;exit.exitTime=.96f;exit.duration=.08f;
            }
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        }

        static void VerifyImportedAnimations(CatController cat)
        {
            var results=new List<string>();
            AnimationMode.StartAnimationMode();
            try
            {
                foreach(var name in new[]{"Idle","Walk","Run","Jump","Land","Paw","Scratch"})
                {
                    var clip=Clips().First(c=>c.name.EndsWith(name,StringComparison.Ordinal));
                    if(clip.empty || clip.length<=0) throw new InvalidOperationException("Empty imported clip: "+name);
                    var bone=Find(cat.visual,name=="Paw"?"Foot_front_R":name=="Scratch"?"Head":"Foot_front_L");
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(cat.visual.gameObject,clip,clip.length*.08f);
                    AnimationMode.EndSampling();
                    var first=bone.position;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(cat.visual.gameObject,clip,clip.length*.53f);
                    AnimationMode.EndSampling();
                    var delta=Vector3.Distance(first,bone.position);
                    results.Add(name+": length="+clip.length+", sampled bone motion="+delta);
                    if(new[]{"Walk","Run","Paw","Scratch"}.Contains(name) && delta<.002f)
                        throw new InvalidOperationException("Imported animation does not move its bones: "+name);
                }
            }
            finally { AnimationMode.StopAnimationMode(); }
            File.WriteAllLines("../Source/Verification/blendswap18519-animation-import.txt",results);
        }

        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        static void ValidateSkin(GameObject visual)
        {
            var conversion=JsonUtility.FromJson<Conversion>(File.ReadAllText(Folder+"/conversion-report.json"));
            if(conversion.nonDeformBones==null) return;
            var forbidden=new HashSet<string>(conversion.nonDeformBones);
            foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            foreach(var weight in skin.sharedMesh.GetAllBoneWeights())
            {
                if(weight.weight>.00001f && forbidden.Contains(skin.bones[weight.boneIndex].name))
                    throw new InvalidOperationException("Control bone incorrectly skins "+skin.name+": "+skin.bones[weight.boneIndex].name);
            }
            Debug.Log("CAT_SKIN_VALIDATED: No controller bone influences any body, eye, fur or whisker vertex");
        }
        static void Save(Material material,string path)
        {
            var previous=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(previous==null) AssetDatabase.CreateAsset(material,path);
            else {EditorUtility.CopySerialized(material,previous);UnityEngine.Object.DestroyImmediate(material);EditorUtility.SetDirty(previous);}
        }
    }
}
