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
        public string CatId => choice.id;
        public void Configure(CatController player, CatChoice selected)
        {
            cat = player; choice = selected; lastPosition = cat.transform.position;
            visualRotation = cat.visual.localRotation;
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
            if (choice == null || choice.animationKind == CatAnimationKind.Original) return;
            var delta = cat.transform.position - lastPosition; lastPosition = cat.transform.position;
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
    }
}
