using System.Collections;
using UnityEngine;

namespace CatSimulator
{
    public sealed class ClimbInteractable : CatInteractable
    {
        public Transform[] route;
        public override bool Available => true;
        public override IEnumerator Perform(CatController cat)
        {
            for (var index = 0; index < route.Length; index++)
            {
                cat.TriggerAnimation(index == 0 ? "WalkAction" : "Jump");
                yield return cat.MoveTo(route[index].position, index == 0 ? .6f : .7f, index == 0 ? 0 : .2f);
                cat.TriggerAnimation("Land");
                yield return new WaitForSeconds(.16f);
            }
            session.Discover(interactionId);
        }
    }

}
