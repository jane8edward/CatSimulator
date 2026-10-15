using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace CatSimulator
{
    public static class HomeUpgradeVerification
    {
        [Serializable] public class SoftCheck
        {public string name;public bool deforms,collisionFollows,recovers;public float indentation,surfaceDrop,recovered;}
        [Serializable] public class Result
        {
            public bool passed,expanded,fridgeFacesInterior,propsStable,propsPresented,coffeeItemsSupported,catRollsBall,bookMoves,lampTips,plantFalls,layersFollow;
            public int dynamicProps,softSurfaces;public float ballTravel,bookTravel,lampAngle;public SoftCheck[] soft;
            public string[] unstableProps,coffeeSupports;
        }
        sealed class State {public Rigidbody body;public Vector3 position;public Quaternion rotation;}
        public static IEnumerator Run(CatController cat,CatCamera rig,GameObject house,string output,Result result)
        {
            var layout=house.GetComponent<HomeLayout>();result.expanded=layout && layout.horizontalExpansion>=1.15f;
            var renderers=house.GetComponentsInChildren<MeshRenderer>();
            var fridge=renderers.First(r=>r.name.Contains("_RefrigeratorBody_"));
            var door=renderers.First(r=>r.name.Contains("_Refrigerator_") && r.name.Contains("_UpperDoor_"));
            result.fridgeFacesInterior=Vector3.Dot(Vector3.ProjectOnPlane(door.bounds.center-fridge.bounds.center,Vector3.up).normalized,Vector3.left)>.95f;
            var bodies=house.GetComponentsInChildren<Rigidbody>();result.dynamicProps=bodies.Length;
            yield return new WaitForSecondsRealtime(1);
            result.unstableProps=bodies.Where(b=>b.isKinematic || !b.useGravity || !b.GetComponentsInChildren<Collider>().Any(c=>c.enabled) || b.linearVelocity.magnitude>=.15f)
                .Select(b=>b.name+" velocity="+b.linearVelocity+" position="+b.position).ToArray();
            result.propsStable=result.unstableProps.Length==0;
            result.propsPresented=bodies.Where(b=>b.name.Contains("Plant_") || b.name.Contains("GrassPot_")).All(b=>Vector3.Angle(b.transform.up,Vector3.up)<15 && b.transform.position.y>-.02f);
            var table=renderers.First(r=>r.name.Contains("_CoffeeTable_01_P01_Top_")).bounds;
            var coffee=bodies.Where(b=>Mathf.Abs(b.worldCenterOfMass.x-table.center.x)<table.extents.x+.05f &&
                Mathf.Abs(b.worldCenterOfMass.z-table.center.z)<table.extents.z+.05f && BoundsOf(b).min.y>.2f).ToArray();
            var support=coffee.Select(b=>new {body=b,gap=SupportGap(b)}).ToArray();
            result.coffeeSupports=support.Select(s=>s.body.name+" actual-surface-gap="+s.gap.ToString("F5")+" bounds-center-gap="+CenterSupportGap(s.body).ToString("F5")).ToArray();
            result.coffeeItemsSupported=coffee.Length>=4 && support.All(s=>Mathf.Abs(s.gap)<.012f);
            var states=bodies.Select(b=>new State{body=b,position=b.position,rotation=b.rotation}).ToArray();
            void Reset()
            {
                foreach(var s in states){s.body.position=s.position;s.body.rotation=s.rotation;s.body.linearVelocity=Vector3.zero;s.body.angularVelocity=Vector3.zero;s.body.Sleep();}
                Physics.SyncTransforms();
            }
            var clear=cat.session.definition.spawnPosition;
            var ball=bodies.First(b=>b.name.Contains("_SisalBall_"));
            var sphere=ball.GetComponentInChildren<SphereCollider>();
            ball.position=clear+Vector3.up*.05f;ball.linearVelocity=Vector3.zero;ball.angularVelocity=Vector3.zero;
            cat.transform.rotation=Quaternion.identity;rig.yaw=0;cat.Teleport(clear-Vector3.forward*.45f);
            yield return new WaitForSecondsRealtime(.2f);var start=ball.position;cat.VerificationMovement=Vector2.up;
            yield return new WaitForSecondsRealtime(1.2f);cat.VerificationMovement=Vector2.zero;
            result.ballTravel=Vector3.ProjectOnPlane(ball.position-start,Vector3.up).magnitude;result.catRollsBall=result.ballTravel>.08f;
            Reset();cat.Teleport(clear);
            var book=bodies.Where(b=>b.name.Contains("_Book_")).OrderByDescending(b=>b.worldCenterOfMass.y).First();
            // Exercise a free book, so a neighbouring tabletop prop cannot block this test.
            book.position=clear+Vector3.forward*.75f+Vector3.up*.15f;
            book.linearVelocity=Vector3.zero;book.angularVelocity=Vector3.zero;
            yield return new WaitForSecondsRealtime(.5f);start=book.position;
            book.AddForce(Vector3.right*.30f,ForceMode.Impulse);yield return new WaitForSecondsRealtime(.7f);
            result.bookTravel=Vector3.Distance(book.position,start);result.bookMoves=result.bookTravel>.03f;Reset();
            var lamp=bodies.First(b=>b.name.Contains("_BedsideLamp_"));var rotation=lamp.rotation;
            lamp.AddForceAtPosition(Vector3.forward*.8f,lamp.worldCenterOfMass+Vector3.up*.28f,ForceMode.Impulse);
            yield return new WaitForSecondsRealtime(1.1f);result.lampAngle=Quaternion.Angle(lamp.rotation,rotation);result.lampTips=result.lampAngle>12;Reset();
            var plant=bodies.First(b=>b.name.Contains("_TablePlant_"));plant.position=clear+Vector3.up*1.1f;
            plant.linearVelocity=Vector3.zero;plant.angularVelocity=Vector3.zero;
            yield return new WaitForSecondsRealtime(2.5f);
            var plantBounds=BoundsOf(plant);result.plantFalls=plantBounds.min.y>-.035f && plantBounds.min.y<.08f && plant.linearVelocity.magnitude<.2f;Reset();
            var surfaces=house.GetComponentsInChildren<SoftPillow>();result.softSurfaces=surfaces.Length;
            var samples=new[] {
                surfaces.First(s=>s.name.Contains("_Sofa_") && s.name.Contains("_SeatCushion_2_")),
                surfaces.First(s=>s.name.Contains("_MasterBed_") && s.name.Contains("_Mattress_")),
                surfaces.First(s=>s.name.Contains("_SleepingPillow_"))};
            var checks=new List<SoftCheck>();
            foreach(var surface in samples)
            {
                var b=surface.GetComponent<Renderer>().bounds;var collider=surface.GetComponent<MeshCollider>();
                var ray=new Ray(new Vector3(b.center.x,b.max.y+.6f,b.center.z),Vector3.down);
                collider.Raycast(ray,out var rest,2);
                rig.yaw=-90;rig.pitch=30;rig.distance=1.25f;
                cat.Teleport(rest.point+Vector3.up*.045f);
                yield return new WaitForSecondsRealtime(1.25f);collider.Raycast(ray,out var loaded,2);
                var check=new SoftCheck{name=surface.name,indentation=surface.CurrentIndentation,surfaceDrop=rest.point.y-loaded.point.y,
                    deforms=surface.CurrentIndentation>.01f,collisionFollows=surface.CollisionIndentation>.008f && rest.point.y-loaded.point.y>.006f};
                if(surface.name.Contains("_MasterBed_"))
                {
                    var sheet=surfaces.First(s=>s.name.StartsWith("H90_MBR_",StringComparison.Ordinal) && s.name.Contains("_CreamSheet_"));
                    var quilt=surfaces.First(s=>s.name.StartsWith("H90_MBR_",StringComparison.Ordinal) && s.name.Contains("_SageQuilt_"));
                    bool hitSheet=sheet.GetComponent<MeshCollider>().Raycast(ray,out var sheetHit,2);
                    bool hitQuilt=quilt.GetComponent<MeshCollider>().Raycast(ray,out var quiltHit,2);
                    result.layersFollow=hitSheet && hitQuilt && quiltHit.point.y-sheetHit.point.y>.012f && sheetHit.point.y-loaded.point.y>.005f;
                }
                yield return new WaitForEndOfFrame();Capture(output,"soft-"+checks.Count+"-"+cat.animationDriver.CatId);
                cat.Teleport(clear);yield return new WaitForSecondsRealtime(2);
                check.recovered=surface.CurrentIndentation;check.recovers=surface.CurrentIndentation<.004f;checks.Add(check);
            }
            result.soft=checks.ToArray();rig.pitch=16;rig.distance=2.05f;rig.yaw=180;cat.Teleport(clear);
            result.passed=result.expanded && result.fridgeFacesInterior && result.dynamicProps>=25 && result.softSurfaces>=15 && result.propsStable &&
                result.propsPresented && result.coffeeItemsSupported && result.catRollsBall && result.bookMoves && result.lampTips && result.plantFalls && result.layersFollow && checks.All(c=>c.deforms && c.collisionFollows && c.recovers);
            File.WriteAllText(Path.Combine(output,"home-upgrade-"+cat.animationDriver.CatId+".json"),JsonUtility.ToJson(result,true));
        }
        static Bounds BoundsOf(Rigidbody body)
        {
            var colliders=body.GetComponentsInChildren<Collider>().Where(c=>c.enabled).ToArray();
            var b=colliders[0].bounds;foreach(var c in colliders.Skip(1))b.Encapsulate(c.bounds);return b;
        }
        static float SupportGap(Rigidbody body)
        {
            // A tilted book's world AABB corner is below its centre, so comparing
            // AABB min.y with a centre ray invents penetration. Sample real bottoms.
            float closest=10;
            foreach(var collider in body.GetComponentsInChildren<Collider>().Where(c=>c.enabled))
            {
                var bounds=collider.bounds;
                foreach(float x in new[]{-.38f,0,.38f})foreach(float z in new[]{-.38f,0,.38f})
                {
                    var origin=new Vector3(bounds.center.x+bounds.size.x*x,bounds.min.y-.04f,bounds.center.z+bounds.size.z*z);
                    if(!collider.Raycast(new Ray(origin,Vector3.up),out var bottom,bounds.size.y+.08f))continue;
                    foreach(var hit in Physics.RaycastAll(bottom.point+Vector3.up*.025f,Vector3.down,3,~(1<<8),QueryTriggerInteraction.Ignore))
                    {
                        if(hit.rigidbody==body)continue;
                        float gap=bottom.point.y-hit.point.y;
                        if(Mathf.Abs(gap)<Mathf.Abs(closest))closest=gap;
                    }
                }
            }
            return closest;
        }
        static float CenterSupportGap(Rigidbody body)
        {
            var b=BoundsOf(body);var hits=Physics.RaycastAll(new Vector3(b.center.x,b.min.y+.025f,b.center.z),Vector3.down,3,~(1<<8),QueryTriggerInteraction.Ignore)
                .Where(h=>h.rigidbody!=body).OrderBy(h=>h.distance).ToArray();
            return hits.Length>0?b.min.y-hits[0].point.y:10;
        }
        static void Capture(string output,string name)
        {
            var image=VerificationRenderTarget.Capture();if(!image)return;
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        }
    }
}
