using UnityEngine;

namespace CatSimulator
{
    // Scene-owned layout information; the first level retains its own scale and setup.
    public sealed class NewHouseLayout : MonoBehaviour
    {
        public float horizontalExpansion = 1.2f;
        public Vector3 Remap(Vector3 original) => new Vector3(original.x * horizontalExpansion, original.y, original.z * horizontalExpansion);
    }
}
