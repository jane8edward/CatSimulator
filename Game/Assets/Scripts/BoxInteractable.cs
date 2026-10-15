using System.Collections;
using UnityEngine;

namespace CatSimulator
{
    public sealed class BoxInteractable : CatInteractable
    {
        public Transform inside;
        public override bool Available => true;
        public override IEnumerator Perform(CatController cat)
        {
            cat.TriggerAnimation("Jump");
            yield return cat.MoveTo(inside.position - Vector3.right * .07f, .65f, .25f);
            cat.transform.rotation = Quaternion.Euler(0, 90, 0);
            cat.TriggerAnimation("Idle");
            session.Discover(interactionId);
            yield return new WaitForSeconds(.8f);
        }
    }}
