using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatSimulator.Editor
{
    public static class NewHouseLayoutInspection
    {
        [Serializable] class Row { public string name,source,placement,id; }
        [Serializable] class Manifest { public Row[] objects; }
        [Serializable] class Part { public string name,parent; public Vector3 position,euler,scale; public Bounds bounds,localBounds; public int vertices; }
        [Serializable] class Group { public string placement,id; public Bounds bounds; public Part[] parts; }
        [Serializable] class Report { public Group[] groups; }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/New_House_Preview.unity");
            var home=UnityEngine.Object.FindFirstObjectByType<NewHousePreview>().residence;
            var map=JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Art/NewHouse/manifest.json")).objects.ToDictionary(r=>r.name);
            var groups=home.GetComponentsInChildren<MeshRenderer>(true).Where(r=>map.ContainsKey(r.name)).GroupBy(r=>map[r.name].placement??"").Select(g=>{
                var bounds=g.First().bounds;foreach(var r in g.Skip(1))bounds.Encapsulate(r.bounds);
                return new Group {placement=g.Key,id=map[g.First().name].id,bounds=bounds,parts=g.Select(r=>new Part {name=r.name,parent=r.transform.parent.name,position=r.transform.position,euler=r.transform.eulerAngles,scale=r.transform.lossyScale,bounds=r.bounds,localBounds=r.GetComponent<MeshFilter>().sharedMesh.bounds,vertices=r.GetComponent<MeshFilter>().sharedMesh.vertexCount}).ToArray()};
            }).ToArray();
            Directory.CreateDirectory("../.tools/newhouse-upgrade");
            File.WriteAllText("../.tools/newhouse-upgrade/layout-"+(home.GetComponent<NewHouseLayout>()?"after":"before")+".json",JsonUtility.ToJson(new Report {groups=groups},true));
            Debug.Log("NEW_HOUSE_INSPECTION_DONE");
        }
        public static void Render()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/New_House_Preview.unity");
            var renderers=UnityEngine.Object.FindFirstObjectByType<NewHousePreview>().residence.GetComponentsInChildren<MeshRenderer>(true);
            var camera=new GameObject("InspectionCamera").AddComponent<Camera>();camera.orthographic=true;camera.nearClipPlane=.02f;camera.farClipPlane=50;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.65f,.68f,.7f);
            var rt=new RenderTexture(1000,1000,24);rt.Create();camera.targetTexture=rt;
            foreach(string id in new[]{"NH034","NH076","NH027","NH007","NH050","NH018"})
            {
                foreach(var r in renderers)r.enabled=r.name.Contains("_"+id+"_");
                var visible=renderers.Where(r=>r.enabled).ToArray();var b=visible[0].bounds;foreach(var r in visible.Skip(1))b.Encapsulate(r.bounds);
                for(int side=0;side<4;side++)
                {
                    var direction=Quaternion.Euler(0,side*90,0)*new Vector3(0,.22f,1);
                    camera.transform.SetPositionAndRotation(b.center+direction*5,Quaternion.LookRotation(-direction));camera.orthographicSize=Mathf.Max(b.size.y,Mathf.Max(b.size.x,b.size.z))*.65f;
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                    RenderTexture.active=rt;var image=new Texture2D(1000,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,1000),0,0);image.Apply();
                    File.WriteAllBytes("../.tools/newhouse-upgrade/"+id+"-"+side+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                }
            }
            RenderTexture.active=null;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
        public static void RenderLayout()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/New_House_Preview.unity");
            var preview=UnityEngine.Object.FindFirstObjectByType<NewHousePreview>();preview.SetView(0);
            var camera=preview.viewCamera;camera.gameObject.SetActive(true);
            var rt=new RenderTexture(1920,1080,24){antiAliasing=4};rt.Create();camera.targetTexture=rt;camera.aspect=1920f/1080;
            var status=new System.Text.StringBuilder();Physics.SyncTransforms();
            foreach(var item in UnityEngine.Object.FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None).Where(i=>i.GetComponent<Rigidbody>()))
            {
                var b=item.Bounds;var hit=Physics.RaycastAll(new Vector3(b.center.x,b.min.y+.02f,b.center.z),Vector3.down,3).Where(h=>h.collider.GetComponentInParent<NewHouseItem>()!=item).OrderBy(h=>h.distance).FirstOrDefault();
                status.AppendLine(item.placement+" bottom="+b.min.y.ToString("F4")+" up="+Vector3.Angle(item.transform.up,Vector3.up).ToString("F2")+" support="+(hit.collider?hit.collider.name:"NONE")+" gap="+(hit.collider?(b.min.y-hit.point.y).ToString("F4"):"?"));
            }
            File.WriteAllText("../.tools/newhouse-upgrade/prop-placement.txt",status.ToString());
            void Capture(string name)
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                var resolved=RenderTexture.GetTemporary(1920,1080,0,RenderTextureFormat.ARGB32);Graphics.Blit(rt,resolved);RenderTexture.active=resolved;
                var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes("../.tools/newhouse-upgrade/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=null;RenderTexture.ReleaseTemporary(resolved);
            }
            Capture("overview-after");preview.SetView(1);Capture("top-after");
            foreach(var pair in new[]{("living-after",new Vector3(-8.2f,.7f,-4.65f),new Vector3(3,4,4),3.2f),("bedroom-after",new Vector3(-2.6f,.9f,-2.8f),new Vector3(3,4,4),2.7f),("laundry-after",new Vector3(-5.9f,1.1f,-1.05f),new Vector3(-3,2,-2),1.45f),("kitchen-after",new Vector3(-10.1f,1,-9),new Vector3(-3,4,3),2.8f)})
            {
                preview.SetView(0);camera.orthographic=true;camera.orthographicSize=pair.Item4;camera.transform.SetPositionAndRotation(pair.Item2+pair.Item3,Quaternion.LookRotation(-pair.Item3));Capture(pair.Item1);
            }
            camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
