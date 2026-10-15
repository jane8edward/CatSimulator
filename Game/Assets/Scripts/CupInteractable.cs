using System.Collections;
using UnityEngine;

namespace CatSimulator
{
    public sealed class CupInteractable : CatInteractable
    {
        public Rigidbody cupBody;
        public Transform actionPosition;
        public Material waterMaterial;
        public override IEnumerator Perform(CatController cat)
        {
            performed = true;
            cat.TriggerAnimation("WalkAction");
            yield return cat.MoveTo(actionPosition.position, .65f, .35f);
            var facing = Vector3.ProjectOnPlane(cupBody.position - cat.transform.position, Vector3.up);
            cat.transform.rotation = Quaternion.LookRotation(facing);
            cat.TriggerAnimation("Paw");
            yield return new WaitForSeconds(.42f);
            cupBody.isKinematic = false;
            cupBody.AddForce((facing.normalized + Vector3.up * .2f) * .2f, ForceMode.Impulse);
            cupBody.AddTorque(Vector3.Cross(Vector3.up, facing.normalized) * .035f, ForceMode.Impulse);
            WaterSpill.Create(cupBody.position, waterMaterial);
            foreach (var renderer in cupBody.GetComponentsInChildren<Renderer>())
                if (renderer.name.Contains("WaterInside")) renderer.enabled = false;
            session.CompleteObjective("spill-cup");
            yield return new WaitForSeconds(.5f);
        }
    }

}
