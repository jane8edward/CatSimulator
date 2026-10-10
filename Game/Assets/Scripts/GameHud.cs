using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CatSimulator
{
    public sealed class GameHud : MonoBehaviour
    {
        public CatController cat;
        public LevelSession session;
        Text tasks, prompt, nameLabel, rating;
        GameObject pause, completion, credits;
        InputField nameInput;
        Font font;
        bool resultShown;

        void Start()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var events = new GameObject("InputEvents"); events.AddComponent<EventSystem>(); events.AddComponent<StandaloneInputModule>();
            var taskPanel = Panel("TaskPanel", new Vector2(28,-28), new Vector2(465,185), new Vector2(0,1));
            nameLabel = Label(taskPanel,"Name",new Vector2(24,-20),new Vector2(420,38),27);
            tasks = Label(taskPanel,"Tasks",new Vector2(24,-70),new Vector2(420,110),23);
            prompt = Label(transform,"InteractionPrompt",new Vector2(-280,140),new Vector2(560,54),28,new Vector2(.5f,0));
            prompt.alignment = TextAnchor.MiddleCenter;
            Label(transform,"Controls",new Vector2(24,60),new Vector2(1350,38),21,new Vector2(0,0)).text = "WASD 移动  ·  鼠标转镜头  ·  空格跳跃  ·  Shift 奔跑  ·  E 互动  ·  Esc 菜单";
            Label(transform,"Version",new Vector2(-285,54),new Vector2(260,32),18,new Vector2(1,0)).text="开发原型 "+Application.version+" · 客厅";
            pause = Panel("Pause",new Vector2(-270,260),new Vector2(540,520),new Vector2(.5f,.5f)).gameObject;
            Label(pause.transform,"Title",new Vector2(30,-24),new Vector2(480,50),32).text="小猫休息一下";
            Label(pause.transform,"NameHint",new Vector2(30,-91),new Vector2(480,35),22).text="给小猫取个名字";
            var input = Panel("NameInput",new Vector2(30,-138),new Vector2(480,55),new Vector2(0,1),pause.transform);
            nameInput = input.gameObject.AddComponent<InputField>(); nameInput.textComponent=Label(input,"Value",new Vector2(12,-8),new Vector2(450,43),24);
            nameInput.characterLimit=12; nameInput.text=session.CatName; nameInput.onEndEdit.AddListener(session.Rename);
            Button(pause.transform,"继续玩",new Vector2(30,-224),()=>SetPaused(false));
            Button(pause.transform,"重新开始这一关",new Vector2(30,-300),session.Replay);
            Button(pause.transform,"退出游戏",new Vector2(30,-376),()=>Application.Quit()); pause.SetActive(false);
            Button(pause.transform,"资源署名",new Vector2(30,-450),()=>{pause.SetActive(false);credits.SetActive(true);});
            credits=Panel("Credits",new Vector2(-410,255),new Vector2(820,510),new Vector2(.5f,.5f)).gameObject;
            Label(credits.transform,"Title",new Vector2(30,-22),new Vector2(760,48),30).text="资源署名";
            var creditText=Resources.Load<TextAsset>("ThirdPartyCredits");
            Label(credits.transform,"Attribution",new Vector2(30,-85),new Vector2(760,330),21).text=creditText!=null?creditText.text:"场景与玩法由 CatSimulator 项目制作。";
            Button(credits.transform,"返回菜单",new Vector2(170,-434),()=>{credits.SetActive(false);pause.SetActive(true);});
            credits.SetActive(false);
            completion = Panel("Completion",new Vector2(-285,200),new Vector2(570,400),new Vector2(.5f,.5f)).gameObject;
            Label(completion.transform,"Title",new Vector2(35,-24),new Vector2(500,55),34).text="今天也很调皮！";
            rating=Label(completion.transform,"Rating",new Vector2(35,-102),new Vector2(500,48),30);
            Label(completion.transform,"Hint",new Vector2(35,-155),new Vector2(500,55),21).text="水杯与窗帘任务已完成。\n猫爬架和纸箱还可以继续玩。";
            Button(completion.transform,"继续探索",new Vector2(35,-238),()=>{completion.SetActive(false);SetPaused(false);});
            Button(completion.transform,"再玩一次",new Vector2(35,-314),session.Replay); completion.SetActive(false);
            session.Changed += Refresh; Refresh();
        }

        void Update()
        {
            if (!cat.Automated && Input.GetKeyDown(KeyCode.Escape)) { completion.SetActive(false);credits.SetActive(false);SetPaused(!cat.Paused);pause.SetActive(cat.Paused); }
            prompt.text = cat.Paused ? "" : cat.IsBusy ? "" : cat.Nearest != null ? "E  " + cat.Nearest.prompt : "";
        }

        void Refresh()
        {
            nameLabel.text=session.CatName+" · 客厅里的小淘气";
            tasks.text=(session.HasObjective("spill-cup")?"✓":"○")+" 打翻桌上的水杯\n"+(session.HasObjective("damage-curtain")?"✓":"○")+" 抓坏窗边的窗帘";
            rating.text = new string('★',session.Stars)+new string('☆',5-session.Stars);
            if(session.IsComplete && !resultShown && !cat.Automated) ShowResult();
        }
        public void ShowResult() { resultShown=true;completion.SetActive(true);SetPaused(true);pause.SetActive(false); }

        void SetPaused(bool value)
        {
            cat.Paused=value; Time.timeScale=value?0:1;
            Cursor.lockState=value?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=value;
            if(!value) pause.SetActive(false);
        }

        RectTransform Panel(string title,Vector2 position,Vector2 size,Vector2 anchor,Transform parent=null)
        {
            var obj=new GameObject(title,typeof(RectTransform),typeof(Image));var rt=(RectTransform)obj.transform;
            rt.SetParent(parent??transform,false);rt.anchorMin=rt.anchorMax=anchor;rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;
            obj.GetComponent<Image>().color=new Color(.075f,.063f,.045f,.8f);return rt;
        }
        Text Label(Transform parent,string title,Vector2 position,Vector2 size,int points,Vector2? anchor=null)
        {
            var obj=new GameObject(title,typeof(RectTransform),typeof(Text));var rt=(RectTransform)obj.transform;rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=anchor??new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;
            var t=obj.GetComponent<Text>();t.font=font;t.fontSize=points;t.color=new Color(1,.94f,.83f);t.raycastTarget=false;return t;
        }
        void Button(Transform parent,string title,Vector2 position,UnityEngine.Events.UnityAction action)
        {
            var rt=Panel(title,position,new Vector2(480,56),new Vector2(0,1),parent);rt.GetComponent<Image>().color=new Color(.48f,.36f,.20f,1);
            var button=rt.gameObject.AddComponent<Button>();button.onClick.AddListener(action);
            var label=Label(rt,"Label",Vector2.zero,new Vector2(480,56),25);label.alignment=TextAnchor.MiddleCenter;label.text=title;
        }
    }
}
