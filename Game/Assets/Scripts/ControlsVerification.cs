using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CatSimulator
{
    // Called by --verify-game-flow. Uses held W in the real coffee-table gap, with no route steering.
    public static class ControlsVerification
    {
        [Serializable] public sealed class Result
        {
            public bool passed,exitedTable,closeViewTriggered,manualTurnFollowed,cameraRecovered;
            public float maxViewAngleError,maxMoveAngleError,maxFacingAngleError,minimumDistance=100,recoveredDistance;
            public int underTableFrames,clippedFrames;
        }
        public static IEnumerator Run(CatController cat,CatCamera rig,GameObject house,string output,Result result)
        {
            var table=house.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.Contains("_CoffeeTable_01_P01_Top_")).bounds;
            rig.yaw=90;rig.pitch=16;rig.distance=2.05f;
            cat.transform.rotation=Quaternion.Euler(0,180,0);
            cat.Teleport(new Vector3(table.min.x-.5f,.04f,table.center.z));
            cat.VerificationMovement=Vector2.zero;
            yield return new WaitForSecondsRealtime(.25f);
            var trace=new StringBuilder("time,x,z,inputYaw,viewYaw,catYaw,cameraDistance,blocker\n");
            var previous=cat.transform.position;
            float began=Time.realtimeSinceStartup;
            float until=began+(table.size.x+1.05f)/cat.walkSpeed*1.5f+2;
            bool captured=false;
            cat.VerificationMovement=Vector2.up;
            while(cat.transform.position.x<table.max.x+.55f && Time.realtimeSinceStartup<until)
            {
                yield return new WaitForEndOfFrame();
                Sample(cat,rig,result,trace,ref previous,Time.realtimeSinceStartup-began>.4f);
                if(cat.transform.position.x>table.min.x+.1f && cat.transform.position.x<table.max.x-.1f)
                {
                    result.underTableFrames++;
                    if(rig.CloseView && !captured)
                    {
                        Capture(Path.Combine(output,"controls-under-table-"+cat.animationDriver.CatId+".png"));captured=true;
                    }
                }
            }
            result.exitedTable=cat.transform.position.x>=table.max.x+.5f;
            // Simulate a deliberate mouse turn while W remains held: movement must change in that frame.
            rig.yaw=0;began=Time.realtimeSinceStartup;var start=cat.transform.position;
            while(Time.realtimeSinceStartup-began<.9f)
            {
                yield return new WaitForEndOfFrame();
                Sample(cat,rig,result,trace,ref previous,Time.realtimeSinceStartup-began>.35f);
            }
            result.manualTurnFollowed=cat.transform.position.z>start.z+cat.walkSpeed*.7f && Mathf.Abs(cat.transform.position.x-start.x)<.02f;
            cat.VerificationMovement=Vector2.zero;
            yield return new WaitForSecondsRealtime(1.4f);yield return new WaitForEndOfFrame();
            result.recoveredDistance=rig.ActualDistance;
            result.cameraRecovered=rig.ActualDistance>=rig.distance-.08f && !rig.CloseView;
            Capture(Path.Combine(output,"controls-exit-"+cat.animationDriver.CatId+".png"));
            result.passed=result.exitedTable && result.manualTurnFollowed && result.cameraRecovered && result.closeViewTriggered &&
                result.underTableFrames>5 && result.maxViewAngleError<.05f && result.maxMoveAngleError<2 && result.maxFacingAngleError<2 && result.clippedFrames==0;
            File.WriteAllText(Path.Combine(output,"controls-"+cat.animationDriver.CatId+".csv"),trace.ToString());
            File.WriteAllText(Path.Combine(output,"controls-"+cat.animationDriver.CatId+".json"),JsonUtility.ToJson(result,true));
        }

        static void Sample(CatController cat,CatCamera rig,Result result,StringBuilder trace,ref Vector3 previous,bool checkFacing)
        {
            var forward=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;
            var delta=Vector3.ProjectOnPlane(cat.transform.position-previous,Vector3.up);previous=cat.transform.position;
            result.maxViewAngleError=Mathf.Max(result.maxViewAngleError,Quaternion.Angle(rig.transform.rotation,rig.ViewRotation));
            if(delta.magnitude>.0003f)result.maxMoveAngleError=Mathf.Max(result.maxMoveAngleError,Vector3.Angle(forward,delta));
            if(checkFacing)result.maxFacingAngleError=Mathf.Max(result.maxFacingAngleError,Vector3.Angle(forward,cat.transform.forward));
            result.minimumDistance=Mathf.Min(result.minimumDistance,rig.ActualDistance);
            result.closeViewTriggered|=rig.CloseView;
            if(Physics.CheckSphere(rig.transform.position,.035f*cat.BodyScale,~(1<<8),QueryTriggerInteraction.Ignore))result.clippedFrames++;
            trace.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0:F3},{1:F4},{2:F4},{3:F2},{4:F2},{5:F2},{6:F4},{7}\n",
                Time.realtimeSinceStartup,cat.transform.position.x,cat.transform.position.z,rig.yaw,rig.transform.eulerAngles.y,cat.transform.eulerAngles.y,rig.ActualDistance,cat.MovementBlocker);
        }

        static void Capture(string path)
        {
            var image=VerificationRenderTarget.Capture();
            if(image==null)return;File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        }
    }
}
