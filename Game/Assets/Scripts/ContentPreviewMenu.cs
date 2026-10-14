using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatSimulator
{
    // Additive entry points for art previews; the saved playable scene is unchanged.
    [DefaultExecutionOrder(-100)]
    public sealed class ContentPreviewMenu : MonoBehaviour
    {
        public const string PlayableScene = "LivingRoom";
        public const string WalkScene = "Walk_Preview";
        public const string HomeScene = "Home90_Preview";
        public static ContentPreviewMenu Instance { get; private set; }
        public static bool GameplayInputBlocked => Instance != null &&
            (Instance.IsOpen || Instance.closedAtFrame == Time.frameCount);
        public bool IsOpen { get; private set; }

        Canvas canvas;
        GameObject overlay;
        Font font;
        CatController pausedCat;
        bool previousCatPause, previousCursorVisible;
        float previousTimeScale;
        CursorLockMode previousCursorLock;
        int closedAtFrame = -1;
        int playableFrameRate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Instance != null || !Application.CanStreamedLevelBeLoaded(WalkScene) ||
                !Application.CanStreamedLevelBeLoaded(HomeScene)) return;
            var args = Environment.GetCommandLineArgs();
            if (args.Any(a => a.StartsWith("--verify-", StringComparison.Ordinal)) &&
                !args.Contains("--verify-content-menu")) return;
            var root = new GameObject("ContentPreviewMenu");
            DontDestroyOnLoad(root);
            root.AddComponent<ContentPreviewMenu>();
        }

        void Awake()
        {
            Instance = this;
            playableFrameRate = Application.targetFrameRate;
            font = Resources.Load<Font>("NotoSansSC");
            if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
            BuildMenu();
        }

        void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= SceneLoaded;
        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator Start()
        {
            yield return null; // Let the saved scene initialize its HUD and cursor first.
            EnsureInput();
            Open();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F4)) { if (IsOpen) Close(); else Open(); }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        public void Open()
        {
            if (IsOpen) return;
            pausedCat = FindFirstObjectByType<CatController>();
            previousCatPause = pausedCat != null && pausedCat.Paused;
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            if (pausedCat != null) pausedCat.Paused = true;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsOpen = true;
            overlay.SetActive(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (pausedCat != null) pausedCat.Paused = previousCatPause;
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            IsOpen = false;
            closedAtFrame = Time.frameCount;
            overlay.SetActive(false);
        }

        public void SelectScene(string scene)
        {
            if (scene != PlayableScene && scene != WalkScene && scene != HomeScene)
                throw new ArgumentException("Unknown content scene", nameof(scene));
            if (!Application.CanStreamedLevelBeLoaded(scene))
                throw new InvalidOperationException("Content scene was not included in this build: " + scene);
            Close();
            if (SceneManager.GetActiveScene().name == scene) return;
            Time.timeScale = 1;
            SceneManager.LoadScene(scene);
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            StartCoroutine(SceneReady(scene.name));
        }

        IEnumerator SceneReady(string scene)
        {
            yield return null;
            EnsureInput();
            if (scene == PlayableScene) Application.targetFrameRate = playableFrameRate;
            else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        static void EnsureInput()
        {
            // Existing gameplay creates its own input system; previews need one for this menu.
            if (EventSystem.current != null) return;
            new GameObject("ContentMenuInput", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        void BuildMenu()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var entry = Panel(transform, "OpenContent", new Vector2(1, 1), new Vector2(-155, -52),
                new Vector2(250, 58), new Color(.21f, .18f, .13f, .94f));
            var entryButton = entry.gameObject.AddComponent<Button>();
            entryButton.onClick.AddListener(Open);
            Label(entry, "内容选择 · F4", new Vector2(0, 0), new Vector2(250, 58), 23, TextAnchor.MiddleCenter);

            var shade = new GameObject("ContentSelection", typeof(RectTransform), typeof(Image));
            var shadeRect = (RectTransform)shade.transform;
            shadeRect.SetParent(transform, false);
            shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(.045f, .06f, .065f, .93f);
            overlay = shade;
            var panel = Panel(shadeRect, "ContentPanel", new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(820, 660), new Color(.15f, .13f, .10f, 1));
            Label(panel, "猫咪模拟器", new Vector2(0, 268), new Vector2(740, 60), 38, TextAnchor.MiddleCenter);
            Label(panel, "选择可玩关卡，或查看本次新增的美术内容", new Vector2(0, 208),
                new Vector2(740, 44), 23, TextAnchor.MiddleCenter);
            SceneButton(panel, "进入游戏 · 客厅与卧室", "自由探索，打翻水杯、攀爬窗帘", 106, PlayableScene);
            SceneButton(panel, "新猫 · 行走动画", "阿比尼西亚猫：播放、暂停、调速与原地行走", -16, WalkScene);
            SceneButton(panel, "90㎡住宅 · 全屋预览", "查看住宅与家具布局；任务和猫咪操控尚未接入", -138, HomeScene);
            var resume = Panel(panel, "Resume", new Vector2(.5f, .5f), new Vector2(0, -263),
                new Vector2(720, 54), new Color(.25f, .22f, .17f, 1));
            resume.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            Label(resume, "继续当前画面 · Esc     /     随时按 F4 重新选择", Vector2.zero,
                new Vector2(710, 54), 21, TextAnchor.MiddleCenter);
            overlay.SetActive(false);
        }

        void SceneButton(Transform parent, string title, string description, float y, string scene)
        {
            var card = Panel(parent, scene, new Vector2(.5f, .5f), new Vector2(0, y),
                new Vector2(720, 108), new Color(.33f, .27f, .18f, 1));
            card.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectScene(scene));
            Label(card, title, new Vector2(0, 20), new Vector2(664, 44), 27, TextAnchor.MiddleLeft);
            Label(card, description, new Vector2(0, -24), new Vector2(664, 38), 20, TextAnchor.MiddleLeft);
        }

        static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            obj.GetComponent<Image>().color = color;
            return rect;
        }

        void Label(Transform parent, string text, Vector2 position, Vector2 size, int points, TextAnchor alignment)
        {
            var obj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            var label = obj.GetComponent<Text>();
            label.font = font; label.fontSize = points; label.alignment = alignment;
            label.color = new Color(1, .95f, .84f); label.raycastTarget = false; label.text = text;
        }
    }
}
