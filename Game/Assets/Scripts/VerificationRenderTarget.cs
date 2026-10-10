using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CatSimulator
{
    // Verification-only: hidden Windows windows can lack a readable swap-chain.
    [DefaultExecutionOrder(10000)]
    public sealed class VerificationRenderTarget : MonoBehaviour
    {
        RenderTexture target;
        Camera cameraComponent;
        UniversalRenderPipeline.SingleCameraRequest request;
        public static void Initialize(Camera camera)
        {
            if (camera.GetComponent<VerificationRenderTarget>() != null) return;
            var capture = camera.gameObject.AddComponent<VerificationRenderTarget>();
            capture.cameraComponent = camera;
            capture.target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32)
                { name = "Verification1920x1080", antiAliasing = 4 };
            capture.target.Create();
            camera.targetTexture = capture.target;
            camera.aspect = 1920f / 1080;
            camera.enabled = false; // the explicit request below renders it exactly once per frame.
            capture.request = new UniversalRenderPipeline.SingleCameraRequest { destination = capture.target };
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
            }
        }

        void LateUpdate()
        {
            // Windows can suppress automatic rendering of a hidden window. Explicitly render
            // the actual URP camera every verification frame, including lighting and post effects.
            RenderPipeline.SubmitRenderRequest(cameraComponent, request);
        }

        public static Texture2D Capture()
        {
            var capture = FindFirstObjectByType<VerificationRenderTarget>();
            var camera = capture != null ? capture.cameraComponent : Camera.main;
            if (camera == null || camera.targetTexture == null) return ScreenCapture.CaptureScreenshotAsTexture();
            var previous = RenderTexture.active;
            var resolved = RenderTexture.GetTemporary(1920, 1080, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(camera.targetTexture, resolved);
            RenderTexture.active = resolved;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
            image.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(resolved);
            return image;
        }

        void OnDestroy()
        {
            if (cameraComponent != null) cameraComponent.targetTexture = null;
            if (target == null) return;
            target.Release();
            Destroy(target);
        }
    }
}
