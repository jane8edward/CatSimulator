using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatSimulator
{
    public enum CatAnimationKind { Original, Abyssinian }
    [Serializable]
    public sealed class CatChoice
    {
        public string id, displayName, description;
        public GameObject visualPrefab;
        public Vector3 visualEuler;
        public float visualScale = 1, walkSpeed = .78f, runSpeed = 1.62f;
        public CatAnimationKind animationKind;
        public float cycleSeconds = 1.2f, strideMeters = .28f;
    }

    public sealed class CatCatalog : ScriptableObject
    {
        public CatChoice[] cats;
        static CatCatalog loaded;
        static string selection;
        public static CatCatalog Current => loaded != null ? loaded : loaded = Resources.Load<CatCatalog>("Gameplay/CatCatalog");
        public static string SelectedId => selection ?? (selection = PlayerPrefs.GetString("selected-cat", "original"));
        public static CatChoice Selected => Current.cats.FirstOrDefault(c => c.id == SelectedId) ?? Current.cats[0];
        public static void Select(string id, bool save = true)
        {
            if (!Current.cats.Any(c => c.id == id)) throw new ArgumentException("Unknown cat: " + id);
            selection = id;
            if (save) { PlayerPrefs.SetString("selected-cat", id); PlayerPrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            loaded = null; selection = null;
            SceneManager.sceneLoaded -= ApplySelection;
            SceneManager.sceneLoaded += ApplySelection;
        }

        static void ApplySelection(Scene scene, LoadSceneMode mode)
        {
            var cat = UnityEngine.Object.FindFirstObjectByType<CatController>();
            if (cat == null || Current == null) return;
            var args = Environment.GetCommandLineArgs();
            bool legacyCheck = args.Any(a => a.StartsWith("--verify-", StringComparison.Ordinal)) && !args.Contains("--verify-game-flow") && !args.Contains("--verify-cat-polish");
            var choice = legacyCheck ? Current.cats[0] : Selected;
            if (choice.id != Current.cats[0].id)
            {
                cat.visual.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(cat.visual.gameObject);
                var visual = Instantiate(choice.visualPrefab, cat.transform);
                foreach (var playback in visual.GetComponentsInChildren<CatWalkPlayback>()) Destroy(playback);
                visual.transform.localPosition = cat.visualOffset;
                visual.transform.localRotation = Quaternion.Euler(choice.visualEuler);
                visual.transform.localScale = Vector3.one * choice.visualScale;
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8;
                cat.visual = visual.transform;
                cat.animator = visual.GetComponent<Animator>();
                cat.walkSpeed = choice.walkSpeed; cat.runSpeed = choice.runSpeed;
            }
            cat.animator.applyRootMotion = false;
            cat.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            cat.ApplyScale(cat.session.definition.playerScale);
            cat.animationDriver = cat.gameObject.AddComponent<NativeCatAnimation>();
            cat.animationDriver.Configure(cat, choice);
        }
    }
}
