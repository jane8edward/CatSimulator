using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatSimulator
{
    public sealed class RoomRegressionVerification : MonoBehaviour
    {
        [Serializable] public class MovementCheck
        {
            public string location;
            public bool progressed, noReversal, exitedTable;
            public float travel, maxAutomaticCameraYaw, lateralDrift;
            public string[] movementBlockers;
            public Vector3 finalPosition;
        }
        [Serializable] public class CollisionCheck
        {
            public string objectName;
            public bool blocksController;
            public float stoppedBeforeSurface;
            public bool meshSurfaceHit, touchedFurniture;
            public float maximumPenetration;
        }
        [Serializable] public class Report
        {
            public bool passed, tableSofaSeparated, tableExitWorks, obstaclesKeepSteering, catBodyClearsSofa;
            public float tableSofaGap;
            public int physicalMeshCount;
            public string[] missingCollisions;
            public MovementCheck[] movement;
            public CollisionCheck[] blockers;
        }
        public Report result;

        public IEnumerator Run(CatController cat, CatCamera cameraRig, string output, string order)
        {
            var room = GameObject.Find("LivingRoom");
            var filters = room.GetComponentsInChildren<MeshFilter>(true);
            var physical = filters.Where(f => !f.name.StartsWith("COL_") && !f.name.StartsWith("FloorPlank_")
                && !f.name.Contains("Outline") && f.name != "WaterInside" && f.GetComponent<Renderer>().enabled).ToArray();
            result = new Report { physicalMeshCount = physical.Length,
                missingCollisions = physical.Where(f => f.GetComponent<Collider>() == null).Select(f => f.name).ToArray() };
            var table = room.GetComponentsInChildren<Renderer>().First(r => r.name == "GlassTableTop").bounds;
            var sofa = room.GetComponentsInChildren<Transform>().First(t => t.name == "Sofa");
            var sofaBounds = sofa.GetComponentInChildren<Renderer>().bounds;
            foreach (var r in sofa.GetComponentsInChildren<Renderer>()) sofaBounds.Encapsulate(r.bounds);
            result.tableSofaGap = table.min.z - sofaBounds.max.z;
            result.tableSofaSeparated = result.tableSofaGap >= .3f;
            var checks = new List<MovementCheck>();
            var start = new Vector3(table.center.x, .065f, table.max.z + .3f);
            yield return MoveContinuously(cat, cameraRig, start, 180, (table.size.z + .6f) / cat.walkSpeed,
                "under-table", checks, output, order);
            checks[0].exitedTable = cat.transform.position.z < table.min.z - .12f;
            result.tableExitWorks = checks[0].exitedTable && checks[0].noReversal;
            var rightWall=room.GetComponentsInChildren<Renderer>(true)
                .Where(r=>r.name=="Wall_Left" || r.name=="Wall_Right").OrderByDescending(r=>r.bounds.center.x).First().bounds;
            yield return MoveContinuously(cat, cameraRig, new Vector3(rightWall.min.x-.19f,.065f,1.65f),180,1.5f,
                "right-wall",checks,output,order);
            yield return MoveContinuously(cat, cameraRig, new Vector3(sofaBounds.min.x-.5f,.065f,sofaBounds.center.z),-90,1.3f,
                "beside-sofa",checks,output,order);
            var box=room.GetComponentsInChildren<Transform>().First(t=>t.name=="BoxInside_0");
            yield return MoveContinuously(cat, cameraRig, new Vector3(box.position.x,.065f,box.position.z+.5f),90,1.3f,
                "beside-box",checks,output,order);
            result.movement = checks.ToArray();
            result.obstaclesKeepSteering = checks.All(c => c.progressed && c.noReversal && c.lateralDrift < .05f)
                && checks.Any(c => c.maxAutomaticCameraYaw > 10);

            var sofaBase=room.GetComponentsInChildren<Renderer>().First(r=>r.name=="Sofa_Base").bounds;
            cat.Teleport(new Vector3(sofaBase.center.x,.065f,sofaBase.max.z+.8f));
            cat.transform.rotation=Quaternion.Euler(0,180,0);cameraRig.yaw=180;
            yield return new WaitForSeconds(.3f);cat.VerificationMovement=Vector2.up;
            yield return new WaitForSeconds(1.5f);cat.VerificationMovement=Vector2.zero;
            result.catBodyClearsSofa=cat.transform.position.z-cat.BodyFrontReach>=sofaBase.max.z-.015f;

            var blockers = new List<CollisionCheck>();
            foreach (var name in new[] { "Sofa_Back", "Sofa_Arm_Right", "Sofa_Base", "Back_Cushion",
                "MediaCabinet", "TVFrame", "PlantPot", "Table_Leg", "SisalPost", "Box_Front" })
            {
                var renderer = room.GetComponentsInChildren<Renderer>().First(r => r.name == name);
                var bounds = renderer.bounds;
                var probe = new GameObject("CollisionProbe"); probe.layer = 8;
                var contacts = probe.AddComponent<CollisionProbeContacts>();
                var controller = probe.AddComponent<CharacterController>();
                controller.height = .4f; controller.radius = .14f; controller.center = Vector3.up * .211f;
                controller.skinWidth = .01f; controller.stepOffset = .07f;
                controller.enabled = false;
                var direction=name=="Table_Leg" ? Vector3.right : Vector3.forward;
                probe.transform.position = name=="Table_Leg"
                    ? new Vector3(bounds.min.x-.45f,bounds.center.y-.186f,bounds.center.z)
                    : new Vector3(bounds.center.x,bounds.center.y-.211f,bounds.min.z-.45f);
                controller.enabled = true;
                var shape=probe.AddComponent<CapsuleCollider>();shape.height=controller.height;shape.radius=controller.radius;
                shape.center=controller.center;shape.isTrigger=true;
                Physics.SyncTransforms();
                var target=renderer.GetComponent<Collider>();
                var meshHit=target.Raycast(new Ray(probe.transform.position+controller.center,direction),out _,2);
                var penetration=0f;
                for (var i=0; i<70; i++)
                {
                    controller.Move(direction * .012f);
                    if(Physics.ComputePenetration(shape,probe.transform.position,probe.transform.rotation,target,target.transform.position,
                        target.transform.rotation,out _,out var depth)) penetration=Mathf.Max(penetration,depth);
                }
                var stopped = name=="Table_Leg" ? bounds.min.x-probe.transform.position.x : bounds.min.z-probe.transform.position.z;
                blockers.Add(new CollisionCheck { objectName=name, stoppedBeforeSurface=stopped,
                    meshSurfaceHit=meshHit,touchedFurniture=contacts.TouchedFurniture,maximumPenetration=penetration,
                    blocksController=meshHit && contacts.TouchedFurniture && penetration<=.02f });
                Destroy(probe); yield return null;
            }
            result.blockers = blockers.ToArray();
            result.passed = result.missingCollisions.Length == 0 && result.tableSofaSeparated && result.tableExitWorks && result.catBodyClearsSofa
                && result.obstaclesKeepSteering && blockers.All(b => b.blocksController);
            File.WriteAllText(Path.Combine(output, order+"-room-regression.json"), JsonUtility.ToJson(result,true));
        }

        IEnumerator MoveContinuously(CatController cat, CatCamera cameraRig, Vector3 start, float yaw, float duration,
            string location, List<MovementCheck> checks, string output, string order)
        {
            cat.Teleport(start); cat.transform.rotation=Quaternion.Euler(0,yaw,0); cameraRig.yaw=yaw;
            yield return new WaitForSeconds(.5f);
            var begin=cat.transform.position; var previous=begin;
            var forward=Quaternion.Euler(0,yaw,0)*Vector3.forward;
            var check=new MovementCheck {location=location,noReversal=true};
            var blockers=new HashSet<string>();
            cat.VerificationMovement=Vector2.up;
            var end=Time.time+duration;
            while(Time.time<end)
            {
                yield return null;
                var position=cat.transform.position;
                if(cat.MovementBlocker!=null) blockers.Add(cat.MovementBlocker);
                check.noReversal &= Vector3.Dot(position-previous,forward)>=-.003f;
                check.maxAutomaticCameraYaw=Mathf.Max(check.maxAutomaticCameraYaw,Mathf.Abs(cameraRig.AutomaticYawOffset));
                check.lateralDrift=Mathf.Max(check.lateralDrift,Mathf.Abs(Vector3.Dot(position-begin,Quaternion.Euler(0,yaw,0)*Vector3.right)));
                previous=position;
            }
            cat.VerificationMovement=Vector2.zero;
            check.travel=Vector3.Dot(cat.transform.position-begin,forward);check.progressed=check.travel>.5f;
            check.movementBlockers=blockers.ToArray();check.finalPosition=cat.transform.position;checks.Add(check);
            if(location=="under-table")
            {
                yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();
                var texture=VerificationRenderTarget.Capture();
                if(texture!=null) {File.WriteAllBytes(Path.Combine(output,order+"-table-exit.png"),texture.EncodeToPNG());Destroy(texture);}
            }
        }
    }
}
