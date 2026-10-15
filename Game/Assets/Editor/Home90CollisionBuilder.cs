using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatSimulator.Editor
{
    // Changes only the separate gameplay copy. Imported art meshes and the original level stay intact.
    public sealed class Home90CollisionBuilder
    {
        const string MeshPath="Assets/Generated/Home90SolidMeshes.asset";
        readonly Dictionary<Mesh,Mesh> meshes=new Dictionary<Mesh,Mesh>();
        readonly List<string> reversed=new List<string>();
        int solids;
        public Home90CollisionBuilder()
        {
            if (AssetDatabase.LoadMainAssetAtPath("Assets/Generated/Home90CollisionMeshes.asset")) AssetDatabase.DeleteAsset("Assets/Generated/Home90CollisionMeshes.asset");
            if (AssetDatabase.LoadMainAssetAtPath(MeshPath)) AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(new Mesh {name="Home90 outward surface meshes"},MeshPath);
        }

        public void AddCollision(MeshRenderer renderer,Mesh source)
        {
            if (!meshes.TryGetValue(source,out var collisionMesh))
            {
                collisionMesh=source;
                var vertices=source.vertices; var triangles=source.triangles; var center=source.bounds.center;
                double volume6=0;
                for(int i=0;i<triangles.Length;i+=3)
                    volume6+=Vector3.Dot(vertices[triangles[i]]-center,
                        Vector3.Cross(vertices[triangles[i+1]]-center,vertices[triangles[i+2]]-center));
                var size=source.bounds.size;
                double tolerance=Math.Max(1e-18,size.x*size.y*size.z*1e-5);
                if (volume6 < -tolerance)
                {
                    collisionMesh=UnityEngine.Object.Instantiate(source); collisionMesh.name=source.name+"_OutwardSurface";
                    for(int sub=0;sub<source.subMeshCount;sub++)
                    {
                        var indices=source.GetTriangles(sub);
                        for(int i=0;i<indices.Length;i+=3) {int swap=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=swap;}
                        collisionMesh.SetTriangles(indices,sub);
                    }
                    // Keep the artist's weighted smoothing and UVs while reversing the surface orientation.
                    collisionMesh.normals=source.normals.Select(n=>-n).ToArray();
                    var tangents=source.tangents;
                    for(int i=0;i<tangents.Length;i++)tangents[i].w=-tangents[i].w;
                    collisionMesh.tangents=tangents;
                    AssetDatabase.AddObjectToAsset(collisionMesh,MeshPath); reversed.Add(renderer.name);
                }
                meshes.Add(source,collisionMesh);
            }
            renderer.GetComponent<MeshFilter>().sharedMesh=collisionMesh;
            // Thin glass needs a solid volume from either direction, including panel edges.
            if (renderer.name.Contains("_Glass_"))
            {
                var box=renderer.gameObject.AddComponent<BoxCollider>();
                box.center=source.bounds.center; box.size=source.bounds.size;
                solids++; return;
            }
            var collider=renderer.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh=collisionMesh;
        }

        public void SaveReport()
        {
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../.tools/furniture-fix");
            File.WriteAllText("../.tools/furniture-fix/collision-repair.txt",
                "Corrected inward-facing meshes: "+reversed.Count+"\nSolid glass panels: "+solids+"\n"+string.Join("\n",reversed));
        }

        public static void ArrangeFurniture(MeshRenderer[] parts)
        {
            foreach(var r in parts)
            {
                string n=r.name; var p=r.bounds.center;
                if(n.Contains("_Sofa_01_") || (n.Contains("_DecorCushion") && p.x>-5.1f && p.x<-4.5f))
                    r.transform.position+=new Vector3(.20f,0,.40f);
                // Move the complete dining set, pendant and tabletop decorations together.
                if(n.Contains("_DiningTable_") || n.Contains("_DiningBench_") || n.Contains("_DiningChair_") ||
                   n.Contains("_DiningPendant_") || (n.Contains("_BranchingPlant_") && p.z<-5 && p.x<-6))
                    r.transform.position+=Vector3.left*.75f;
                if(n.Contains("_DiningChair_")) r.transform.position+=Vector3.back*.16f;
                if(n.Contains("_Hideout_")) r.transform.position+=new Vector3(-.40f,0,1.03f);
                if(n.Contains("_ScratchBall_")) r.transform.position+=new Vector3(-.65f,0,-.55f);
                if(n.Contains("_ScratchPad_")) r.transform.position+=new Vector3(-.49f,0,-.35f);
                if(n.Contains("_SisalBall_")) r.transform.position+=Vector3.forward*2;
                if(n.Contains("_BroadleafPlant_") && p.x<-8 && p.z<-3)
                {
                    // A smaller plant fits behind the scratch column without clipping the wall or hideout.
                    r.transform.position=new Vector3(-9.4f,0,-5.65f)+(r.transform.position-new Vector3(-9.27f,0,-4.72f))*.78f;
                    r.transform.localScale*=.78f;
                }
                if(n.Contains("_OfficeChair_")) r.transform.position+=Vector3.back*.38f;
                if(n.Contains("_Ottoman_")) r.transform.position+=new Vector3(-.48f,0,.42f);
                if(n.Contains("_MasterWardrobe_")) r.transform.position+=Vector3.forward*.25f;
                if(n.Contains("_BroadleafPlant_") && p.x>-4 && p.x<-3 && p.z>-.7f)
                    r.transform.position+=Vector3.right*.70f;
                if((n.Contains("_BedsideTable_") || n.Contains("_BedsideLamp_")) && p.x<-2.5f && p.z>-4 && p.z<-3)
                    r.transform.position+=new Vector3(.08f,0,-.14f);
            }
        }

        public static void AddSofaSupport(GameObject room,MeshRenderer[] parts)
        {
            var seats=parts.Where(r=>r.name.Contains("_Sofa_01_") && r.name.Contains("_SeatCushion_")).ToArray();
            if(seats.Length==0) throw new InvalidOperationException("Sofa seat support has no matching cushions");
            var b=seats[0].bounds;foreach(var seat in seats.Skip(1))b.Encapsulate(seat.bounds);
            // A continuous support just below the padded surface bridges seams, without filling the space below the sofa.
            b.SetMinMax(new Vector3(b.min.x+.014f,.235f,b.min.z+.014f),
                new Vector3(b.max.x-.014f,b.max.y-.012f,b.max.z-.014f));
            var support=new GameObject("Sofa_ContinuousSeatSupport");
            support.transform.position=b.center;support.transform.SetParent(room.transform,true);
            support.AddComponent<BoxCollider>().size=b.size;
        }

        public static void OpenSlidingDoors(GameObject room,MeshRenderer[] parts)
        {
            foreach(var r in parts.Where(r=>r.name.Contains("kitchen_slider")))
            {
                if(r.name.Contains("Glass_1") || r.name.Contains("_P08_Pull_Handle_")) r.transform.position+=Vector3.right*.83f;
                if(r.name.Contains("Glass_2") || r.name.Contains("_P09_Pull_Handle_")) r.transform.position+=Vector3.left*.83f;
                if(r.name.Contains("Mullion")) r.transform.position+=Vector3.right*1.08f;
            }
            foreach(var r in parts.Where(r=>r.name.Contains("balcony_slider")))
            {
                if(r.name.Contains("Glass_2")) r.transform.position+=Vector3.right*.7775f;
                if(r.name.Contains("Glass_3")) r.transform.position+=Vector3.left*.7775f;
                if(!r.name.Contains("Mullion")) continue;
                // The art mullions are one joined mesh; replace them with jambs on the parked leaves.
                r.gameObject.SetActive(false);
                foreach(float x in new[] {-6.273f,-7.827f})
                {
                    var post=GameObject.CreatePrimitive(PrimitiveType.Cube); post.name="BalconyParkedLeafMullion";
                    post.transform.position=new Vector3(x,1.1f,-1.6f);post.transform.localScale=new Vector3(.045f,2.11f,.05f);
                    post.GetComponent<Renderer>().sharedMaterial=r.sharedMaterial;post.transform.SetParent(room.transform,true);
                }
            }
        }
    }
}
