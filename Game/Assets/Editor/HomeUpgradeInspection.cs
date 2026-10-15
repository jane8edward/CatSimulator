using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace CatSimulator.Editor
{
    public static class HomeUpgradeInspection
    {
        [Serializable] class Part {public string name,parent,grandparent,path;public Vector3 center,size,min,max;}
        [Serializable] class Report {public Part[] parts;public string[] roots;}
        public static void Inspect()
        {
            EditorSceneManager.OpenScene(FullGameBuilder.HomePath);
            var root=GameObject.Find("Home90Residence");
            var report=new Report {roots=root.transform.Cast<Transform>().Select(t=>t.name).ToArray(),parts=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).Select(r=>new Part {
                name=r.name,parent=r.transform.parent.name,grandparent=r.transform.parent.parent?.name,path=PathOf(r.transform),
                center=r.bounds.center,size=r.bounds.size,min=r.bounds.min,max=r.bounds.max}).ToArray()};
            Directory.CreateDirectory("../.tools/home-upgrade");File.WriteAllText("../.tools/home-upgrade/inspection.json",JsonUtility.ToJson(report,true));
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        public static void InspectPropPoses()
        {
            EditorSceneManager.OpenScene(FullGameBuilder.HomePath);
            using(var log=new StreamWriter("../.tools/home-upgrade/prop-poses.txt"))
                foreach(var body in GameObject.Find("Home90Residence").GetComponentsInChildren<Rigidbody>())
                    log.WriteLine(body.name+" pos="+body.position+" up-angle="+Vector3.Angle(body.transform.up,Vector3.up)+" com="+body.worldCenterOfMass);
        }
    }
}
