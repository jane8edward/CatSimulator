using System.Linq;
using UnityEngine;

namespace CatSimulator
{
    // Each visual keeps its own Avatar and clips, including the balcony motion adaptation.
    [DefaultExecutionOrder(50)]
    public sealed class NativeCatAnimation : MonoBehaviour
    {
        CatController cat;
        CatChoice choice;
        Vector3 lastPosition;
        Quaternion visualRotation;
        Transform leftArm, rightArm, head;
        string action;
        float actionStarted, actionDuration;
        bool moving;
        Transform[] seatedBones;
        Vector3[] seatedPositions;
        Quaternion[] seatedRotations;
        bool seated;
        float savedAnimatorSpeed;
        bool houseGesture;
        Quaternion houseHeadRotation;
        public string CatId => choice.id;
        public void Configure(CatController player, CatChoice selected)
        {
            cat = player; choice = selected; lastPosition = cat.transform.position;
            visualRotation = cat.visual.localRotation;
            head=cat.visual.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Head" || t.name=="bone_9");
            if (choice.animationKind == CatAnimationKind.Abyssinian)
            {
                var bones = cat.visual.GetComponentsInChildren<Transform>();
                leftArm = bones.First(t => t.name == "bone_17");
                rightArm = bones.First(t => t.name == "bone_21");
                head = bones.First(t => t.name == "bone_9");
                cat.animator.Play("Walk", 0, .2f); cat.animator.Update(0); cat.animator.speed = 0;
            }
        }

        public void Locomotion(float speed)
        {
            if (choice.animationKind == CatAnimationKind.Original)
                cat.animator.SetFloat("Speed", speed, .12f, Time.deltaTime);
        }

        public void Trigger(string value)
        {
            if (choice.animationKind == CatAnimationKind.Original) { cat.animator.SetTrigger(value); return; }
            action = value; actionStarted = Time.time;
            actionDuration = value == "Scratch" ? 2.2f : value == "Paw" ? .85f : value == "Jump" ? .7f : .2f;
        }

        void LateUpdate()
        {
            if(cat && (cat.IsRiding || cat.HouseAction=="Squat"))
            {
                if(!seated)BeginSeat();
                ApplySeat();lastPosition=cat.transform.position;return;
            }
            if(seated)EndSeat();
            if(cat)cat.visual.localPosition=cat.visualOffset+Vector3.down*(.025f*cat.BodyScale*cat.JumpCharge);
            if(cat && cat.HouseAction!=null)
            {
                if(!houseGesture){houseGesture=true;if(head)houseHeadRotation=head.localRotation;}
                float envelope=Mathf.Sin(cat.HouseActionProgress*Mathf.PI);
                if(head)
                {
                    var aim=cat.HouseActionTarget-head.position;
                    float tilt=Mathf.Clamp(Mathf.Atan2(-aim.y,Vector3.ProjectOnPlane(aim,Vector3.up).magnitude)*Mathf.Rad2Deg,-15,55);
                    head.rotation=Quaternion.AngleAxis(tilt*envelope+6*Mathf.Sin(Time.time*23)*envelope,cat.transform.right)*(head.parent.rotation*houseHeadRotation);
                }
                lastPosition=cat.transform.position;return;
            }
            if(houseGesture){houseGesture=false;if(head)head.localRotation=houseHeadRotation;}
            if (choice == null || choice.animationKind == CatAnimationKind.Original) return;
            var delta = cat.transform.position - lastPosition - cat.PlatformMotion; lastPosition = cat.transform.position;
            if (cat.Paused || Time.deltaTime <= 0) { cat.animator.speed = 0; return; }
            float velocity = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude / Time.deltaTime;
            bool nowMoving = velocity > .03f;
            if (!nowMoving && moving) { cat.animator.Play("Walk", 0, .2f); cat.animator.Update(0); }
            moving = nowMoving;
            cat.animator.speed = nowMoving ? Mathf.Clamp(velocity * choice.cycleSeconds / (choice.strideMeters * choice.visualScale * cat.BodyScale), .1f, 8) : 0;
            cat.visual.localRotation = visualRotation;
            float t = Time.time - actionStarted;
            if (t >= actionDuration) return;
            if (action == "Paw" || action == "Scratch")
            {
                // Until more native clips exist, add a gesture on this cat's own front limbs.
                cat.animator.speed = 0; cat.animator.Update(0);
                float lift = action == "Paw" ? Mathf.Sin(Mathf.Clamp01(t / .85f) * Mathf.PI) * 46 : 52 + 8 * Mathf.Sin(t * 18);
                rightArm.rotation = Quaternion.AngleAxis(-lift, cat.transform.right) * rightArm.rotation;
                if (action == "Scratch")
                {
                    leftArm.rotation = Quaternion.AngleAxis(-lift + 10 * Mathf.Sin(t * 18 + Mathf.PI), cat.transform.right) * leftArm.rotation;
                    head.rotation = Quaternion.AngleAxis(-5, cat.transform.right) * head.rotation;
                }
            }
            else if (action == "Jump")
                cat.visual.localRotation = visualRotation * Quaternion.Euler(-12 * Mathf.Sin(Mathf.Clamp01(t / .7f) * Mathf.PI), 0, 0);
        }

        void BeginSeat()
        {
            savedAnimatorSpeed=cat.animator.speed;
            cat.visual.localPosition=cat.visualOffset;
            cat.animator.Play(choice.animationKind==CatAnimationKind.Original?"Idle":"Walk",0,choice.animationKind==CatAnimationKind.Original?0:.2f);
            cat.animator.Update(0);cat.animator.speed=0;
            // Cache each rig's own evaluated pose, never copy transforms between cats.
            seatedBones=cat.visual.GetComponentsInChildren<Transform>();
            seatedPositions=new Vector3[seatedBones.Length];seatedRotations=new Quaternion[seatedBones.Length];
            for(int i=0;i<seatedBones.Length;i++)
            {seatedPositions[i]=cat.transform.InverseTransformPoint(seatedBones[i].position);seatedRotations[i]=Quaternion.Inverse(cat.transform.rotation)*seatedBones[i].rotation;}
            seated=true;
        }
        void ApplySeat()
        {
            for(int i=0;i<seatedBones.Length;i++)
            {
                var bone=seatedBones[i];if(bone==cat.visual || bone.GetComponent<Renderer>() || bone.GetComponent<Animator>())continue;
                var p=seatedPositions[i];float height=p.y/cat.BodyScale;
                float rear=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.14f,.13f,p.z/cat.BodyScale));
                // Fold the hindquarters onto the pad while keeping the front paws planted.
                float torso=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.035f,.18f,height));
                p.y+=cat.BodyScale*torso*(-.14f*rear+.045f*(1-rear));
                p.z+=cat.BodyScale*rear*.065f;
                bone.SetPositionAndRotation(cat.transform.TransformPoint(p),cat.transform.rotation*seatedRotations[i]);
            }
        }
        void EndSeat()
        {
            for(int i=0;i<seatedBones.Length;i++)if(seatedBones[i])
                seatedBones[i].SetPositionAndRotation(cat.transform.TransformPoint(seatedPositions[i]),cat.transform.rotation*seatedRotations[i]);
            seated=false;cat.animator.speed=savedAnimatorSpeed;moving=false;action=null;actionDuration=0;
            cat.animator.Play(choice.animationKind==CatAnimationKind.Original?"Idle":"Walk",0,.2f);cat.animator.Update(0);
        }
    }
}
