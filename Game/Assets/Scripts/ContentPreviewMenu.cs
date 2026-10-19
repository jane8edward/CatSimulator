using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatSimulator
{
    [DefaultExecutionOrder(-100)]
    public sealed class ContentPreviewMenu : MonoBehaviour
    {
        public const string StartScene = "StartMenu", PlayableScene = "LivingRoom", HomeScene = "New_House_Preview";
        public const string NewHouseScene = "New_House_Preview";
        public static ContentPreviewMenu Instance { get; private set; }
        public static bool GameplayInputBlocked => Instance != null && (Instance.IsOpen || Instance.closedAtFrame == Time.frameCount);
        public bool IsOpen { get; private set; }
        public string CurrentPage => page.ToString();
        enum Page { Home, Levels, Cats }
        Page page, nextPage;
        Canvas canvas;
        GameObject content, shade;
        RectTransform panel;
        Font font;
        CatController pausedCat;
        float oldTimeScale;
        bool oldCatPause, oldCursorVisible;
        CursorLockMode oldCursorLock;
        int closedAtFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var args = Environment.GetCommandLineArgs();
            bool check = args.Any(a => a.StartsWith("--verify-", StringComparison.Ordinal));
            if (check && !args.Contains("--verify-game-flow"))
            {
                if (SceneManager.GetActiveScene().name == StartScene) SceneManager.LoadScene(PlayableScene);
                return;
            }
            if (Instance != null || !Application.CanStreamedLevelBeLoaded(StartScene) || CatCatalog.Current == null) return;
            var root = new GameObject("GameMenu"); DontDestroyOnLoad(root); root.AddComponent<ContentPreviewMenu>();
        }

        void Awake()
        {
            Instance = this;
            font = Resources.Load<Font>("NotoSansSC");
            if (!font) font = Font.CreateDynamicFontFromOSFont(new[] {"Microsoft YaHei","Arial"},24);
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            shade = new GameObject("ModalShade",typeof(RectTransform),typeof(Image));
            var rt = (RectTransform)shade.transform; rt.SetParent(transform,false); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero; shade.GetComponent<Image>().color = new Color(.025f,.035f,.04f,.84f);
            panel = Panel(transform,"MenuPanel",new Vector2(.77f,.5f),Vector2.zero,new Vector2(720,760),new Color(.12f,.14f,.15f,.98f));
            content = panel.gameObject; content.SetActive(false); shade.SetActive(false);
        }
        void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= SceneLoaded;
        void OnDestroy() { if (Instance == this) Instance = null; }
        IEnumerator Start() { yield return null; Ready(SceneManager.GetActiveScene().name); }
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            pausedCat = null; StartCoroutine(AfterLoad(scene.name));
        }
        IEnumerator AfterLoad(string scene) { yield return null; Ready(scene); }
        void Ready(string scene)
        {
            if (EventSystem.current == null) new GameObject("MenuInput",typeof(EventSystem),typeof(StandaloneInputModule));
            if (scene == StartScene)
            {
                Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                IsOpen = true; content.SetActive(true); shade.SetActive(false); ShowPage(nextPage); nextPage = Page.Home;
            }
            else { IsOpen = false; content.SetActive(false); shade.SetActive(false); }
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F4) && SceneManager.GetActiveScene().name != StartScene)
            { if (IsOpen) Close(); else Open(); }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (page != Page.Home) ShowPage(Page.Home);
                else Close();
            }
        }
        public void Open()
        {
            if (IsOpen) return;
            pausedCat = FindFirstObjectByType<CatController>(); oldCatPause = pausedCat != null && pausedCat.Paused;
            oldTimeScale = Time.timeScale; oldCursorLock = Cursor.lockState; oldCursorVisible = Cursor.visible;
            if (pausedCat != null) pausedCat.Paused = true;
            Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            IsOpen = true; content.SetActive(true); shade.SetActive(true); ShowPage(Page.Home);
        }
        public void Close()
        {
            if (SceneManager.GetActiveScene().name == StartScene) return;
            if (!IsOpen) return;
            if (pausedCat != null) pausedCat.Paused = oldCatPause;
            Time.timeScale = oldTimeScale; Cursor.lockState = oldCursorLock; Cursor.visible = oldCursorVisible;
            IsOpen = false; closedAtFrame = Time.frameCount; content.SetActive(false); shade.SetActive(false);
        }
        public void ReturnToStart() => LoadStart(Page.Home);
        void LoadStart(Page target)
        {
            nextPage = target; Time.timeScale = 1; IsOpen = false;
            SceneManager.LoadScene(StartScene);
        }
        public void LoadLevel(int level)
        {
            if (level != 1 && level != 2) throw new ArgumentOutOfRangeException(nameof(level));
            Time.timeScale = 1; IsOpen = false; closedAtFrame = Time.frameCount;
            content.SetActive(false); shade.SetActive(false);
            SceneManager.LoadScene(level == 1 ? PlayableScene : HomeScene);
        }
        public void ShowLevels() => ShowPage(Page.Levels);
        public void ShowCats()
        {
            if (SceneManager.GetActiveScene().name == StartScene) ShowPage(Page.Cats);
            else LoadStart(Page.Cats);
        }

        void ShowPage(Page value)
        {
            page = value;
            foreach (Transform child in panel) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var gallery = FindFirstObjectByType<CatModelGallery>();
            if (gallery != null) gallery.allowOrbit = value == Page.Cats;
            string title = value == Page.Home ? "猫咪模拟器" : value == Page.Levels ? "选择关卡" : "查看与选择猫咪";
            Label(panel,title,new Vector2(0,300),new Vector2(660,62),36,TextAnchor.MiddleCenter);
            if (value == Page.Home)
            {
                Label(panel,"好奇、调皮，也有一点黏人",new Vector2(0,234),new Vector2(660,44),23,TextAnchor.MiddleCenter);
                Card("StartGame","开始游戏","先选择想要进入的关卡",86,ShowLevels);
                Card("CatModels","查看猫咪模型","看看猫咪们，并选择你的主角",-56,ShowCats);
                Label(panel,"当前猫咪："+CatCatalog.Selected.displayName,new Vector2(0,-216),new Vector2(650,48),24,TextAnchor.MiddleCenter);
                if (SceneManager.GetActiveScene().name != StartScene)
                    Label(panel,"Esc 继续当前游戏",new Vector2(0,-290),new Vector2(650,44),21,TextAnchor.MiddleCenter);
            }
            else if (value == Page.Levels)
            {
                Label(panel,"主角："+CatCatalog.Selected.displayName,new Vector2(0,234),new Vector2(660,44),23,TextAnchor.MiddleCenter);
                Card("Level1","第一关 · 客厅与卧室","打翻水杯、抓坏窗帘，猫爬架和纸箱也等着你",100,()=>LoadLevel(1));
                Card("Level2","第二关 · 温馨大宅","咬电脑、闯点小祸，再搭扫地机兜风",-42,()=>LoadLevel(2));
                Button(panel,"Back","返回",new Vector2(0,-222),new Vector2(620,58),()=>ShowPage(Page.Home));
            }
            else if (gallery != null)
            {
                var displayed = Array.Find(CatCatalog.Current.cats,c => c.id == gallery.DisplayedId) ?? CatCatalog.Selected;
                Label(panel,"拖动左侧模型旋转，滚轮拉近或拉远",new Vector2(0,234),new Vector2(660,44),21,TextAnchor.MiddleCenter);
                for (int index=0;index<CatCatalog.Current.cats.Length;index++)
                {
                    var c=CatCatalog.Current.cats[index];
                    float y = 161-index*80;
                    Button(panel,"Preview-"+c.id,c.displayName+(c.id == displayed.id ? "  ·  正在查看" : ""),new Vector2(0,y),new Vector2(620,66),()=>{gallery.Show(c.id);ShowPage(Page.Cats);});
                }
                Label(panel,displayed.description,new Vector2(0,-62),new Vector2(640,48),20,TextAnchor.MiddleCenter);
                Button(panel,"Standing","站姿",new Vector2(-159,-118),new Vector2(302,50),()=>gallery.SetWalking(false));
                Button(panel,"Walking","行走",new Vector2(159,-118),new Vector2(302,50),()=>gallery.SetWalking(true));
                Button(panel,"UseCat",displayed.id == CatCatalog.SelectedId ? "已选用这只猫 ✓" : "使用这只猫",new Vector2(0,-194),new Vector2(620,60),()=>{CatCatalog.Select(gallery.DisplayedId);ShowPage(Page.Cats);});
                Button(panel,"Back","返回开始界面",new Vector2(0,-280),new Vector2(620,54),()=>ShowPage(Page.Home));
            }
        }
        void Card(string name,string title,string description,float y,UnityEngine.Events.UnityAction action)
        {
            var card = Button(panel,name,"",new Vector2(0,y),new Vector2(620,112),action);
            Label(card,title,new Vector2(0,23),new Vector2(566,46),29,TextAnchor.MiddleLeft);
            Label(card,description,new Vector2(0,-26),new Vector2(566,40),20,TextAnchor.MiddleLeft);
        }
        RectTransform Button(Transform parent,string name,string title,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(parent,name,new Vector2(.5f,.5f),position,size,new Color(.34f,.29f,.21f,1));
            rect.gameObject.AddComponent<Button>().onClick.AddListener(action);
            if (title.Length > 0) Label(rect,title,Vector2.zero,size,25,TextAnchor.MiddleCenter);
            return rect;
        }
        static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {
            var obj = new GameObject(name,typeof(RectTransform),typeof(Image)); var rect = (RectTransform)obj.transform;
            rect.SetParent(parent,false); rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            obj.GetComponent<Image>().color = color; return rect;
        }
        void Label(Transform parent,string text,Vector2 position,Vector2 size,int points,TextAnchor alignment)
        {
            var obj = new GameObject("Label",typeof(RectTransform),typeof(Text)); var rect = (RectTransform)obj.transform;
            rect.SetParent(parent,false); rect.anchoredPosition = position; rect.sizeDelta = size;
            var label = obj.GetComponent<Text>(); label.font = font; label.fontSize = points; label.alignment = alignment;
            label.color = new Color(1,.95f,.84f); label.raycastTarget = false; label.text = text;
        }
    }
}
