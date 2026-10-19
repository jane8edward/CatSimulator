using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        public bool IsReady { get; private set; }
        float previousAnimationSpeed, previousTimeScale = 1;
        static bool shadersWarmed;

        IEnumerator Start()
        {
            cat.Paused=true;
            var robot=FindFirstObjectByType<RobotVacuum>();
            float robotDeadline=Time.realtimeSinceStartup+15;
            while(robot&&!robot.Ready&&Time.realtimeSinceStartup<robotDeadline)yield return null;
            if(!shadersWarmed){yield return WarmHouseViews();shadersWarmed=true;}
            yield return null;
            SetPreview(false);
            IsReady=true;
            yield return null;
            // Use the bundled font on macOS for this scene's existing HUD.
            var font = Resources.Load<Font>("NotoSansSC");
            if (font) foreach (var text in hud.GetComponentsInChildren<Text>(true)) text.font = font;
            var nameLabel = hud.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Name");
            if(nameLabel){nameLabel.fontSize=22;nameLabel.horizontalOverflow=HorizontalWrapMode.Overflow;nameLabel.verticalOverflow=VerticalWrapMode.Overflow;nameLabel.rectTransform.sizeDelta=new Vector2(420,44);}
            var controls = hud.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Controls");
            if (controls) {controls.text = "WASD 移动 · 空格短按跳 / 长按蓄力松开 · Shift 跑 · E 互动 · 扫地机上 F 坐下/站起 · V 全屋 · Esc 菜单";controls.fontSize=19;}
        }

        IEnumerator WarmHouseViews()
        {
            // Render only this house's actual material/light combinations. Global
            // Shader.WarmupAllShaders can crash inside Unity's native warmup on this GPU.
            var camera=new GameObject("HouseMaterialWarmup").AddComponent<Camera>();
            camera.CopyFrom(followCamera.GetComponent<Camera>());camera.enabled=false;
            var settings=camera.GetUniversalAdditionalCameraData();
            var source=followCamera.GetComponent<Camera>().GetUniversalAdditionalCameraData();
            settings.renderPostProcessing=source.renderPostProcessing;settings.renderShadows=source.renderShadows;
            settings.antialiasing=source.antialiasing;settings.volumeLayerMask=source.volumeLayerMask;
            var target=RenderTexture.GetTemporary(480,270,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
            camera.targetTexture=target;
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            var points=FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None)
                .Where(i=>i.GetComponentInChildren<SoftPillow>()).Select(i=>i.Bounds.center)
                .Concat(cat.session.definition.areas.Select(a=>a.bounds.center))
                .Concat(FindObjectsByType<RobotVacuum>(FindObjectsSortMode.None).Where(r=>r.Ready).Select(r=>r.Seat)).ToArray();
            foreach(var point in points)
            {
                var offset=new Vector3(-1.5f,.9f,1.2f);
                camera.transform.SetPositionAndRotation(point+offset,Quaternion.LookRotation(-offset));
                RenderPipeline.SubmitRenderRequest(camera,request);
                yield return null;
            }
            camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);Destroy(camera.gameObject);
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
