using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CatSimulator
{
    // Scene-specific mode switching; movement, animation and camera remain shared gameplay code.
    public sealed class NewHouseExploration : MonoBehaviour
    {
        public CatController cat;
        public CatCamera followCamera;
        public GameHud hud;
        public NewHousePreview preview;
        public bool IsPreview { get; private set; }
        float previousAnimationSpeed, previousTimeScale = 1;

        IEnumerator Start()
        {
            SetPreview(false);
            yield return null;
            // Use the bundled font on macOS for this scene's existing HUD.
            var font = Resources.Load<Font>("NotoSansSC");
            if (font) foreach (var text in hud.GetComponentsInChildren<Text>(true)) text.font = font;
            var controls = hud.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Controls");
            if (controls) controls.text = "WASD 移动 · 鼠标转镜头 · 空格跳跃 · Shift 奔跑 · E 开关门窗 · V 全屋视图 · Esc 菜单";
        }

        void Update()
        {
            if (ContentPreviewMenu.GameplayInputBlocked) return;
            if (Input.GetKeyDown(KeyCode.V) && (IsPreview || !cat.Paused)) SetPreview(!IsPreview);
        }

        public void SetPreview(bool value)
        {
            if (value && !IsPreview) { previousAnimationSpeed = cat.animator.speed; cat.animator.speed = 0; previousTimeScale = Time.timeScale; Time.timeScale = 0; }
            if (!value && IsPreview) { cat.animator.speed = previousAnimationSpeed; Time.timeScale = previousTimeScale; }
            IsPreview = value;
            cat.Paused = value;
            preview.enabled = value;
            preview.viewCamera.gameObject.SetActive(value);
            followCamera.gameObject.SetActive(!value);
            hud.gameObject.SetActive(!value);
            followCamera.tag = value ? "Untagged" : "MainCamera";
            preview.viewCamera.tag = value ? "MainCamera" : "Untagged";
            if (value) preview.SetView(0);
            else { preview.RestoreFullHouse(); followCamera.SnapToTarget(); }
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CheckIfRequested()
        {
            if (!Environment.GetCommandLineArgs().Contains("--check-new-house-controls")) return;
            var root = new GameObject("NewHouseControlsVerification");
            DontDestroyOnLoad(root);
            root.AddComponent<NewHouseControlsVerification>();
        }
    }
}
