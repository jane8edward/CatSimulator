using System.Collections;
using UnityEngine;

namespace CatSimulator
{
    public sealed class CurtainInteractable : CatInteractable
    {
        public Transform actionPosition;
        public GameObject intact;
        public GameObject damaged;
        public override IEnumerator Perform(CatController cat)
        {
            performed = true;
            cat.TriggerAnimation("WalkAction");
            yield return cat.MoveTo(actionPosition.position, .45f);
            var facing = Vector3.ProjectOnPlane(Anchor.position - cat.transform.position, Vector3.up);
            cat.transform.rotation = Quaternion.LookRotation(facing);
            var ground = cat.transform.position;
            cat.TriggerAnimation("Jump");
            yield return cat.MoveTo(ground + Vector3.up * .75f + facing.normalized * .08f, .6f, .12f);
            cat.TriggerAnimation("Scratch");
            var elapsed = 0f;
            var rotation = intact.transform.localRotation;
            while (elapsed < 2.2f)
            {
                elapsed += Time.deltaTime;
                intact.transform.localRotation = rotation * Quaternion.Euler(0, 0, Mathf.Sin(elapsed * 15) * 2.4f);
                yield return null;
            }
            intact.transform.localRotation = rotation;
            intact.SetActive(false);
            damaged.SetActive(true);
            cat.TriggerAnimation("Jump");
            yield return cat.MoveTo(ground, .5f);
            session.CompleteObjective("damage-curtain");
            cat.TriggerAnimation("Idle");
        }
    }

}
