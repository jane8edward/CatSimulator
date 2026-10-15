using System.Collections.Generic;
using UnityEngine;
namespace CatSimulator.Editor
{
    public static class HomeSurfaceSubdivision
    {
        public static Mesh DenseTop(Mesh source,Transform transform,bool allFaces=false)
        {
            var v=new List<Vector3>(source.vertices);var uv=new List<Vector2>(source.uv);
            var n=new List<Vector3>(source.normals);var tangents=new List<Vector4>(source.tangents);
            while(uv.Count<v.Count)uv.Add(Vector2.zero);while(n.Count<v.Count)n.Add(Vector3.up);
            while(tangents.Count<v.Count)tangents.Add(new Vector4(1,0,0,1));
            var sub=new List<int[]>();for(int i=0;i<source.subMeshCount;i++)sub.Add(source.GetTriangles(i));
            float bottom=float.PositiveInfinity;foreach(var point in v)bottom=Mathf.Min(bottom,transform.TransformPoint(point).y);
            for(int level=0;level<6;level++)
            {
                bool changed=false;var edges=new Dictionary<ulong,int>();
                int Mid(int a,int b)
                {
                    ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
                    if(edges.TryGetValue(key,out int found))return found;
                    int index=v.Count;edges.Add(key,index);v.Add((v[a]+v[b])*.5f);uv.Add((uv[a]+uv[b])*.5f);
                    n.Add((n[a]+n[b]).normalized);tangents.Add((tangents[a]+tangents[b])*.5f);return index;
                }
                for(int s=0;s<sub.Count;s++)
                {
                    var next=new List<int>();var tri=sub[s];
                    for(int i=0;i<tri.Length;i+=3)
                    {
                        int a=tri[i],b=tri[i+1],c=tri[i+2];
                        float longest=Mathf.Max(transform.TransformVector(v[a]-v[b]).magnitude,
                            transform.TransformVector(v[b]-v[c]).magnitude,transform.TransformVector(v[c]-v[a]).magnitude);
                        if(longest<=.105f || (!allFaces && Mathf.Max(transform.TransformPoint(v[a]).y,transform.TransformPoint(v[b]).y,transform.TransformPoint(v[c]).y)<=bottom+.0001f))
                        {next.Add(a);next.Add(b);next.Add(c);continue;}
                        int ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);changed=true;
                        next.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});
                    }
                    sub[s]=next.ToArray();
                }
                if(!changed)break;
            }
            var mesh=new Mesh {name=source.name+"_SoftGrid",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetNormals(n);mesh.SetTangents(tangents);mesh.subMeshCount=sub.Count;
            for(int i=0;i<sub.Count;i++)mesh.SetTriangles(sub[i],i);
            mesh.RecalculateBounds();return mesh;
        }
    }
}
