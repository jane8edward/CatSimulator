using UnityEngine;

namespace CatSimulator
{
    public sealed class CollisionProbeContacts : MonoBehaviour
    {
        public bool TouchedFurniture { get; private set; }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(hit.collider.name.StartsWith("COL_") || hit.collider.name=="FloorBase" || hit.collider.name=="RugBase") return;
            TouchedFurniture = true;
        }
    }
}
