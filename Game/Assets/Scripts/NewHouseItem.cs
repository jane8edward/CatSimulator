using UnityEngine;

namespace CatSimulator
{
    public sealed class NewHouseItem : MonoBehaviour
    {
        public string placement;
        public NewHouseItem support;
        public bool hasContactBase;
        public Vector3 contactBaseLocal;
        public Bounds Bounds
        {
            get
            {
                var parts = GetComponentsInChildren<MeshRenderer>();
                var bounds = new Bounds(transform.position, Vector3.zero);
                bool first = true;
                foreach (var part in parts)
                {
                    if (part.GetComponentInParent<NewHouseItem>() != this || !part.enabled) continue;
                    if (first) { bounds = part.bounds; first = false; } else bounds.Encapsulate(part.bounds);
                }
                return bounds;
            }
        }
    }
}
