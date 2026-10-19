using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatSimulator
{
    public abstract class CatInteractable : MonoBehaviour
    {
        public string prompt;
        public string interactionId;
        public float radius = .95f;
        public Transform anchor;
        public LevelSession session;
        protected bool performed;
        readonly List<GameObject> outlines = new List<GameObject>();
        public Transform Anchor => anchor != null ? anchor : transform;
        public virtual bool Available => !performed;
        public abstract IEnumerator Perform(CatController cat);

        public void SetUpHighlight(Material material)
        {
            foreach (var filter in GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name.StartsWith("COL_") || filter.name.Contains("Damaged")) continue;
                var outline = new GameObject("InteractionOutline");
                outline.transform.SetParent(filter.transform, false);
                var copy = outline.AddComponent<MeshFilter>();
                copy.sharedMesh = filter.sharedMesh;
                var renderer = outline.AddComponent<MeshRenderer>();
                var slots = filter.GetComponent<MeshRenderer>().sharedMaterials.Length;
                var materials = new Material[slots];
                for (var index = 0; index < slots; index++) materials[index] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outline.SetActive(false);
                outlines.Add(outline);
            }
        }

        public void Highlight(bool enabled)
        {
            // Editor-authored outlines survive scene serialization; the runtime list does not.
            if(outlines.Count==0)
                foreach(var child in GetComponentsInChildren<Transform>(true))
                    if(child.name=="InteractionOutline")outlines.Add(child.gameObject);
            foreach (var outline in outlines) if (outline != null) outline.SetActive(enabled);
        }
    }

}
