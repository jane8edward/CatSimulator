using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace CatSimulator.Editor
{
    public static class NewHouseDoorMeshRepair
    {
        struct Vertex
        {
            public Vector3 p, n; public Vector2 uv; public Vector4 tangent;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex {
                p = Vector3.Lerp(a.p,b.p,t), n = Vector3.Lerp(a.n,b.n,t).normalized,
                uv = Vector2.Lerp(a.uv,b.uv,t), tangent = Vector4.Lerp(a.tangent,b.tangent,t)
            };
        }
        sealed class Part
        {
            public readonly List<Vertex> vertices = new List<Vertex>();
            public readonly List<int>[] indices;
            public Part(int count) { indices = Enumerable.Range(0,count).Select(_ => new List<int>()).ToArray(); }
            public void Polygon(List<Vertex> polygon, int slot)
            {
                if (polygon.Count < 3) return;
                int start = vertices.Count; vertices.AddRange(polygon);
                for (int i=1;i<polygon.Count-1;i++)
                {
                    if (Vector3.Cross(polygon[i].p-polygon[0].p,polygon[i+1].p-polygon[0].p).sqrMagnitude < 1e-20f) continue;
                    indices[slot].Add(start); indices[slot].Add(start+i); indices[slot].Add(start+i+1);
                }
            }
            public void Write(Mesh target)
            {
                target.Clear(); target.indexFormat=IndexFormat.UInt32;
                target.SetVertices(vertices.Select(v=>v.p).ToArray()); target.SetNormals(vertices.Select(v=>v.n).ToArray());
                target.SetUVs(0,vertices.Select(v=>v.uv).ToArray()); target.SetTangents(vertices.Select(v=>v.tangent).ToArray());
                target.subMeshCount=indices.Length;
                for(int i=0;i<indices.Length;i++)target.SetTriangles(indices[i],i);
                target.RecalculateBounds(); EditorUtility.SetDirty(target);
            }
        }
        public static void Repair(Transform home)
        {
            foreach (var door in UnityEngine.Object.FindObjectsByType<NewHouseDoor>(FindObjectsSortMode.None).Where(d=>d.displayName=="主卧门" || d.displayName=="书房门"))
            {
                bool master=door.displayName=="主卧门";
                var original=home.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name.StartsWith(master?"NH_MBR_NH064_":"NH_OFF_NH064_") && !f.name.EndsWith("_OpenLeaf") && !f.name.EndsWith("_FixedFrame"));
                var frame=home.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name==original.name+"_FixedFrame");
                var leaf=home.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name==original.name+"_OpenLeaf");
                var panel=door.panels[0]; var src=original.sharedMesh;
                var positions=src.vertices; var normals=src.normals; var uv=src.uv; var tangents=src.tangents;
                var inside=new Part(src.subMeshCount); var outside=new Part(src.subMeshCount);
                float width=master?.9f:.85f;
                float Distance(Vertex v,int plane)
                {
                    var world=original.transform.TransformPoint(v.p);
                    float across=master?panel.closedPosition.x-world.x:panel.closedPosition.z-world.z;
                    return plane==0?across-.045f:plane==1?width-.045f-across:plane==2?world.y-.02f:2.13f-world.y;
                }
                for(int slot=0;slot<src.subMeshCount;slot++)
                {
                    var triangles=src.GetTriangles(slot);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        var polygon=new List<Vertex>();
                        for(int j=0;j<3;j++)
                        {
                            int v=triangles[i+j]; polygon.Add(new Vertex {p=positions[v],n=normals[v],uv=uv[v],tangent=tangents.Length>v?tangents[v]:Vector4.zero});
                        }
                        for(int plane=0;plane<4 && polygon.Count>0;plane++)
                        {
                            var kept=new List<Vertex>(); var discarded=new List<Vertex>();
                            for(int j=0;j<polygon.Count;j++)
                            {
                                var a=polygon[j]; var b=polygon[(j+1)%polygon.Count];
                                float da=Distance(a,plane), db=Distance(b,plane);
                                if(da>=0)kept.Add(a);else discarded.Add(a);
                                if((da>=0)!=(db>=0)) {var cut=Vertex.Lerp(a,b,da/(da-db));kept.Add(cut);discarded.Add(cut);}
                            }
                            outside.Polygon(discarded,slot); polygon=kept;
                        }
                        inside.Polygon(polygon,slot);
                    }
                }
                inside.Write(leaf.sharedMesh); outside.Write(frame.sharedMesh);
                var collider=frame.GetComponent<MeshCollider>(); if(collider){collider.sharedMesh=null;collider.sharedMesh=frame.sharedMesh;}
                var pivot=panel.body.transform; var rotation=pivot.rotation;
                pivot.rotation=panel.closedRotation;
                var bounds=leaf.GetComponent<MeshRenderer>().bounds;
                panel.shape.center=pivot.InverseTransformPoint(bounds.center);
                var size=bounds.size; if(master)size.z=.045f;else size.x=.045f;panel.shape.size=size;
                pivot.rotation=rotation;
            }
            AssetDatabase.SaveAssets();
        }
    }
}
