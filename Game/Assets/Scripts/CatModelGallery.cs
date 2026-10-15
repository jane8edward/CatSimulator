using UnityEngine;
using UnityEngine.EventSystems;

namespace CatSimulator
{
    public sealed class CatModelGallery : MonoBehaviour
    {
        public Camera galleryCamera;
        public bool allowOrbit;
        GameObject model;
        Animator animator;
        CatChoice choice;
        bool walking;
        float yaw = 25, distance = 1.32f;
        public string DisplayedId => choice != null ? choice.id : "";
        void Start() => Show(CatCatalog.Selected.id);
        public void Show(string id)
        {
            choice = System.Array.Find(CatCatalog.Current.cats, c => c.id == id);
            if (model != null) { model.SetActive(false); Destroy(model); }
            model = Instantiate(choice.visualPrefab, transform);
            foreach (var playback in model.GetComponentsInChildren<CatWalkPlayback>()) { playback.enabled = false; Destroy(playback); }
            model.transform.localPosition = new Vector3(0, .002f, 0);
            model.transform.localRotation = Quaternion.Euler(choice.visualEuler);
            model.transform.localScale = Vector3.one * choice.visualScale;
            animator = model.GetComponent<Animator>();
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            walking = false; SetWalking(false);
        }
        public void SetWalking(bool value)
        {
            walking = value;
            if (choice.animationKind == CatAnimationKind.Original)
            {
                animator.speed = 1; animator.SetFloat("Speed", value ? 1 : 0);
                animator.Play("Locomotion", 0, 0); animator.Update(0);
            }
            else { animator.Play("Walk", 0, .2f); animator.Update(0); animator.speed = value ? 1 : 0; }
        }
        void Update()
        {
            if (model == null) return;
            if (allowOrbit && Input.mousePosition.x < Screen.width * .55f &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                if (Input.GetMouseButton(0)) yaw -= Input.GetAxis("Mouse X") * 3;
                distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * .12f, .8f, 2);
            }
            else if (!allowOrbit) yaw += Time.unscaledDeltaTime * 8;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            galleryCamera.transform.position = new Vector3(-.34f, .54f, distance);
            galleryCamera.transform.LookAt(new Vector3(-.34f, .22f, 0));
        }
    }
}
