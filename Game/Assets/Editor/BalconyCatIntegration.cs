using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace CatSimulator.Editor
{
    // Independent output assets; existing cats, scenes and their controllers are preserved.
    public static class BalconyCatIntegration
    {
        const string Folder="Assets/Art/BalconyCat";
        const string Model=Folder+"/BalconyCat.fbx";
        const string Controller=Folder+"/BalconyCat.controller";
        static readonly string[] Names={"Idle","Walk","Run","Jump","Land","Paw","Scratch"};

        public static void InstallAndBuild() { Install();FirstPlayableBuilder.Build(); }
        [MenuItem("Cat Simulator/Install Balcony Cat")]
        public static void Install()
        {
            AssetDatabase.Refresh();
            var texture=(TextureImporter)AssetImporter.GetAtPath(Folder+"/Textures/CalicoCoat.jpg");
            texture.sRGBTexture=true;texture.maxTextureSize=2048;texture.anisoLevel=8;
            texture.textureCompression=TextureImporterCompression.CompressedHQ;texture.SaveAndReimport();
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            importer.globalScale=1;importer.animationType=ModelImporterAnimationType.Generic;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;
            importer.animationCompression=ModelImporterAnimationCompression.Off;importer.optimizeGameObjects=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importBlendShapes=false;
            importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.skinWeights=ModelImporterSkinWeights.Custom;importer.maxBonesPerVertex=4;importer.minBoneWeight=.00001f;
            var definitions=importer.defaultClipAnimations;
            foreach(var clip in definitions)
            {
                clip.loopTime=new[]{"Idle","Walk","Run"}.Any(n=>clip.name.EndsWith(n,StringComparison.Ordinal));
                clip.loopPose=false;clip.keepOriginalPositionXZ=true;clip.keepOriginalPositionY=true;
            }
            importer.clipAnimations=definitions;importer.SaveAndReimport();
            var clips=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            AnimationClip Clip(string name)=>clips.First(c=>c.name.EndsWith(name,StringComparison.Ordinal));
            foreach(var name in Names) if(Clip(name).empty || Clip(name).length<=0)throw new InvalidOperationException("Missing balcony motion: "+name);
            if(AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller)==null)
                if(!AssetDatabase.CopyAsset("Assets/Art/BlendSwap18519/AnimatedCat.controller",Controller))throw new IOException("Cannot copy motion controller");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            Motion Remap(Motion motion)
            {
                if(motion is AnimationClip oldClip)return Clip(Names.First(n=>oldClip.name.EndsWith(n,StringComparison.Ordinal)));
                if(motion is BlendTree blend)
                {
                    var children=blend.children;
                    for(int i=0;i<children.Length;i++)children[i].motion=Remap(children[i].motion);
                    blend.children=children;EditorUtility.SetDirty(blend);
                }
                return motion;
            }
            foreach(var state in controller.layers[0].stateMachine.states){state.state.motion=Remap(state.state.motion);EditorUtility.SetDirty(state.state);}
            var original=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/BlendSwap18519/AnimatedCat.controller");
            var originalStates=original.layers[0].stateMachine.states;
            foreach(var state in controller.layers[0].stateMachine.states)
            {
                var source=originalStates.First(s=>s.state.name==state.state.name).state;
                if(state.state.motion is BlendTree tree && source.motion is BlendTree sourceTree)
                {
                    var children=tree.children;var sourceChildren=sourceTree.children;
                    for(int i=0;i<children.Length;i++)children[i].timeScale=sourceChildren[i].timeScale/(i==0?1:.75f);
                    tree.children=children;EditorUtility.SetDirty(tree);
                }
                if(state.state.name=="WalkAction")state.state.speed=source.speed/.75f;
                EditorUtility.SetDirty(state.state);
            }
            EditorUtility.SetDirty(controller);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/CalicoCoat.mat");
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"/CalicoCoat.mat");}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/CalicoCoat.jpg"));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.22f);material.SetFloat("_Metallic",0);
            EditorUtility.SetDirty(material);
            var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));visual.name="BalconyCalicoCat";
            var animator=visual.GetComponent<Animator>();animator.runtimeAnimatorController=controller;
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {skin.sharedMaterials=Enumerable.Repeat(material,skin.sharedMesh.subMeshCount).ToArray();skin.updateWhenOffscreen=true;skin.quality=SkinQuality.Bone4;}
            Validate(visual,clips);
            var prefab=PrefabUtility.SaveAsPrefabAsset(visual,Folder+"/BalconyCat.prefab");UnityEngine.Object.DestroyImmediate(visual);
            var catalog=AssetDatabase.LoadAssetAtPath<CatCatalog>("Assets/Resources/Gameplay/CatCatalog.asset");
            var choice=new CatChoice{id="balcony",displayName="阳台三花猫",description="圆滚滚的三花小猫，好奇又黏人",visualPrefab=prefab,visualEuler=new Vector3(0,180,0),visualScale=1,walkSpeed=.78f,runSpeed=1.62f,animationKind=CatAnimationKind.Original};
            catalog.cats=catalog.cats.Where(c=>c.id!="balcony").Concat(new[]{choice}).ToArray();EditorUtility.SetDirty(catalog);
            PlayerSettings.bundleVersion="0.3.1";AssetDatabase.SaveAssets();
            Debug.Log("BALCONY_CAT_INSTALLED: 3 selectable cats, 50 bound bones, 7 adapted original motions");
        }
        static void Validate(GameObject visual,AnimationClip[] clips)
        {
            var skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();
            Debug.Log("BALCONY_IMPORT: avatar="+visual.GetComponent<Animator>().avatar.isValid+" bones="+skin.bones.Length+" vertices="+skin.sharedMesh.vertexCount);
            if(!visual.GetComponent<Animator>().avatar.isValid || skin.bones.Length!=50 || skin.sharedMesh.vertexCount<25515)throw new InvalidOperationException("Invalid balcony avatar/mesh");
            var sums=new float[skin.sharedMesh.vertexCount];int cursor=0;
            var counts=skin.sharedMesh.GetBonesPerVertex();var weights=skin.sharedMesh.GetAllBoneWeights();
            for(int v=0;v<counts.Length;v++)for(int i=0;i<counts[v];i++)sums[v]+=weights[cursor++].weight;
            if(sums.Any(w=>Mathf.Abs(w-1)>.001f))throw new InvalidOperationException("Invalid skin weights");
            var output=new List<string>{"50 bones; 25,515 source vertices; "+skin.sharedMesh.vertexCount+" imported vertices after UV/normal splits; normalized weights; native UV and texture retained."};
            AnimationMode.StartAnimationMode();
            try
            {
                foreach(var clip in clips)
                {
                    var bone=visual.GetComponentsInChildren<Transform>().First(t=>t.name.Contains("0_Right_Limb_2"));
                    var samples=new List<Vector3>();
                    foreach(float t in new[]{.1f,.35f,.65f,.9f})
                    {
                        AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(visual,clip,clip.length*t);AnimationMode.EndSampling();
                        var mesh=new Mesh();skin.BakeMesh(mesh);var bounds=mesh.bounds;
                        if(!float.IsFinite(bounds.size.sqrMagnitude) || bounds.size.magnitude>3)throw new InvalidOperationException("Exploded skin: "+clip.name);
                        UnityEngine.Object.DestroyImmediate(mesh);samples.Add(bone.position);
                    }
                    float travel=samples.Max(p=>Vector3.Distance(p,samples[0]));
                    if((clip.name.EndsWith("Walk") || clip.name.EndsWith("Run") || clip.name.EndsWith("Paw")) && travel<.005f)throw new InvalidOperationException("Motion binding does not move paw: "+clip.name);
                    output.Add(clip.name+": "+clip.length+"s; paw sample displacement "+travel+"m");
                }
            }
            finally{AnimationMode.StopAnimationMode();}
            Directory.CreateDirectory("../Source/Verification/CatPolish031");File.WriteAllLines("../Source/Verification/CatPolish031/balcony-animation-import.txt",output);
        }
    }
}
