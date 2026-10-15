using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatSimulator.Editor
{
    public static class Home90PhysicsValidation
    {
        [Serializable] public sealed class Passage
        {
            public string name;public float clearWidth;public bool clear;public string[] blockers;
            public Vector3 from,to;
            public Passage(string label,Vector3 start,Vector3 end,float width)
            {name=label;from=start;to=end;clearWidth=width;}
        }
        [Serializable] public sealed class Report {public bool passed;public Passage[] passages;public string[] badSeatFaces,placementConflicts;}
        public static void Validate()
        {
            EditorSceneManager.OpenScene(FullGameBuilder.HomePath);Physics.SyncTransforms();
            var report=new Report {passages=new[] {
                new Passage("客厅—走廊",new Vector3(-4.65f,0,-5.08f),new Vector3(-3.65f,0,-5.08f),.70f),
                new Passage("主卧",new Vector3(-3.6f,0,-4.4f),new Vector3(-3.6f,0,-3.5f),.65f),
                new Passage("次卧与书房",new Vector3(-3.4f,0,-5.575f),new Vector3(-2.6f,0,-5.575f),.60f),
                new Passage("走廊—玄关",new Vector3(-3.6f,0,-6.7f),new Vector3(-3.6f,0,-7.3f),.70f),
                new Passage("卫生间",new Vector3(-3.8f,0,-7.825f),new Vector3(-4.6f,0,-7.825f),.60f),
                new Passage("厨房",new Vector3(-8.3f,0,-5.6f),new Vector3(-8.3f,0,-6.5f),.90f),
                new Passage("阳台",new Vector3(-7.05f,0,-2),new Vector3(-7.05f,0,-1.1f),1.20f)
            }};
            foreach(var p in report.passages)
            {
                var offset=p.to-p.from;
                var orientation=Quaternion.LookRotation(offset);
                var half=new Vector3(p.clearWidth*.5f,.8175f,.06f);
                var center=p.from+Vector3.up*.8825f;
                var overlaps=Physics.OverlapBox(center,half,orientation,~(1<<8),QueryTriggerInteraction.Ignore);
                var hits=Physics.BoxCastAll(center,half,offset.normalized,orientation,offset.magnitude,~(1<<8),QueryTriggerInteraction.Ignore);
                p.blockers=overlaps.Select(h=>h.name).Concat(hits.Select(h=>h.collider.name)).Distinct().ToArray();
                p.clear=p.blockers.Length==0;
            }
            var renderers=GameObject.Find("Home90Residence").GetComponentsInChildren<MeshRenderer>();
            report.badSeatFaces=renderers
                .Where(r=>r.name.Contains("_Sofa_01_") && r.name.Contains("_SeatCushion_"))
                .Where(r=> {
                    var b=r.bounds;
                    return !r.GetComponent<Collider>().Raycast(new Ray(new Vector3(b.center.x,b.max.y+.1f,b.center.z),Vector3.down),out var hit,1) || hit.point.y<b.max.y-.02f;
                }).Select(r=>r.name).ToArray();
            var hideout=GroupBounds(renderers,r=>r.name.Contains("_Hideout_"));
            var column=GroupBounds(renderers,r=>r.name.Contains("_ScratchColumn_"));
            var console=GroupBounds(renderers,r=>r.name.Contains("_TVConsole_"));
            var baseBounds=GroupBounds(renderers,r=>r.name.Contains("_ScratchColumn_") && r.name.Contains("_P01_Base_"));
            var pot=GroupBounds(renderers,r=>r.name.Contains("_BroadleafPlant_") && r.name.Contains("_CeramicPot_") && r.bounds.center.x<-8);
            var pad=GroupBounds(renderers,r=>r.name.Contains("_ScratchPad_"));
            var ball=GroupBounds(renderers,r=>r.name.Contains("_ScratchBall_") && r.name.Contains("_P01_Base_"));
            var conflicts=new System.Collections.Generic.List<string>();
            if(hideout.Intersects(column))conflicts.Add("Hideout / scratch column");
            if(hideout.Intersects(console))conflicts.Add("Hideout / TV console");
            if(hideout.Intersects(pot))conflicts.Add("Hideout / plant pot");
            if(hideout.Intersects(pad))conflicts.Add("Hideout / scratch pad");
            if(hideout.Intersects(ball))conflicts.Add("Hideout / scratch ball");
            if(baseBounds.Intersects(pot))conflicts.Add("Scratch column / plant pot");
            if(baseBounds.Intersects(pad))conflicts.Add("Scratch column / scratch pad");
            if(baseBounds.Intersects(ball))conflicts.Add("Scratch column / scratch ball");
            report.placementConflicts=conflicts.ToArray();
            report.passed=report.badSeatFaces.Length==0 && report.passages.All(p=>p.clear) && conflicts.Count==0;
            Directory.CreateDirectory("../.tools/furniture-fix");
            File.WriteAllText("../.tools/furniture-fix/geometry.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Home90 furniture or doorway clearance validation failed; see geometry.json");
        }
        static Bounds GroupBounds(MeshRenderer[] renderers,Func<MeshRenderer,bool> predicate)
        {
            var parts=renderers.Where(predicate).ToArray();
            if(parts.Length==0)throw new InvalidOperationException("A required furniture group is missing");
            var b=parts[0].bounds;foreach(var part in parts.Skip(1))b.Encapsulate(part.bounds);return b;
        }
    }
}
