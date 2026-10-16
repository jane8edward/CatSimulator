using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatSimulator
{
    // Dedicated viewer for the reviewed Tripo residence. Does not change the playable cat.
    public sealed class NewHousePreview : MonoBehaviour
    {
        [Serializable] public class View
        {
            public string name;
            public Vector3 position, forward, up;
            public bool ortho;
            public float size, fov;
        }
        public Camera viewCamera;
        public Transform residence;
        public View[] views;
        public Renderer[] ceilings, cutWalls, pendants, distantBuildings, balconyOccluders;
        public Vector3 houseCenter;
        public int placementCount;
        public string CurrentView => views[current].name;
        int current, previousVSync, previousFrameRate;
        Vector3 pivot;
        float distance, yaw, pitch;
        GUIStyle title, caption, button, activeButton, panel;
        bool controlsVisible = true;
        public bool showControls = true;

        void Awake()
        {
            previousVSync = QualitySettings.vSyncCount;
            previousFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.53f, .53f, .50f));
            ambient.AddDirectionalLight(Vector3.up, new Color(.6f, .69f, .8f), .55f);
            RenderSettings.ambientProbe = ambient;
            SetView(0);
        }

        void OnDestroy()
        {
            QualitySettings.vSyncCount = previousVSync;
            Application.targetFrameRate = previousFrameRate;
        }

        public void SetView(int index)
        {
            if (views == null || views.Length == 0) return;
            current = Mathf.Clamp(index, 0, views.Length - 1);
            var v = views[current];
            viewCamera.transform.SetPositionAndRotation(v.position, Quaternion.LookRotation(v.forward, v.up));
            viewCamera.orthographic = v.ortho;
            // Blender's landscape orthographic scale is horizontal; preserve the source height.
            viewCamera.orthographicSize = v.size / 2f / 1.19f;
            if (current == 1) viewCamera.orthographicSize = Mathf.Max(5.9f, viewCamera.orthographicSize);
            viewCamera.fieldOfView = v.fov;
            bool overview = current < 2;
            Visible(ceilings, !overview);
            Visible(cutWalls, !overview);
            Visible(pendants, !overview);
            Visible(distantBuildings, !overview);
            Visible(balconyOccluders, current != 3);
            pivot = overview ? houseCenter : v.position + v.forward * 2;
            distance = Vector3.Distance(viewCamera.transform.position, pivot);
            var angles = viewCamera.transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x > 180 ? angles.x - 360 : angles.x;
        }

        static void Visible(Renderer[] items, bool visible)
        {
            if (items == null) return;
            foreach (var r in items) if (r) r.enabled = visible;
        }

        public void RestoreFullHouse()
        {
            Visible(ceilings, true); Visible(cutWalls, true); Visible(pendants, true);
            Visible(distantBuildings, true); Visible(balconyOccluders, true);
        }

        void Update()
        {
            if (ContentPreviewMenu.GameplayInputBlocked) return;
            for (int i = 0; i < 4; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SetView(i);
            if (Input.GetKeyDown(KeyCode.R)) SetView(current);
            if (Input.GetKeyDown(KeyCode.H)) controlsVisible = !controlsVisible;
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * 2.5f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2, -80, 88);
                var rotation = Quaternion.Euler(pitch, yaw, 0);
                if (viewCamera.orthographic)
                    viewCamera.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
                else viewCamera.transform.rotation = rotation;
            }
            if (Input.GetMouseButton(2))
            {
                var shift = (-viewCamera.transform.right * Input.GetAxis("Mouse X") -
                             viewCamera.transform.up * Input.GetAxis("Mouse Y")) * (viewCamera.orthographic ? .08f : .018f);
                viewCamera.transform.position += shift; pivot += shift;
            }
            float scroll = Input.mouseScrollDelta.y;
            if (viewCamera.orthographic)
                viewCamera.orthographicSize = Mathf.Clamp(viewCamera.orthographicSize - scroll * .35f, .5f, 14);
            else
            {
                var movement = viewCamera.transform.forward * Input.GetAxisRaw("Vertical") +
                               viewCamera.transform.right * Input.GetAxisRaw("Horizontal");
                viewCamera.transform.position += movement * Time.unscaledDeltaTime * (Input.GetKey(KeyCode.LeftShift) ? 3 : 1.2f);
                viewCamera.transform.position += viewCamera.transform.forward * scroll * .15f;
            }
        }

        void OnGUI()
        {
            if (!showControls || !controlsVisible || ContentPreviewMenu.Instance != null && ContentPreviewMenu.Instance.IsOpen) return;
            if (title == null)
            {
                var font = Resources.Load<Font>("NotoSansSC");
                title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 27, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                caption = new GUIStyle(title) { fontSize = 16, fontStyle = FontStyle.Normal };
                panel = new GUIStyle { normal = { background = Solid(new Color(.07f, .09f, .08f, .88f)) } };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18,
                    normal = { background = Solid(new Color(.11f, .16f, .13f, .96f)), textColor = Color.white },
                    hover = { background = Solid(new Color(.24f, .32f, .22f, 1)), textColor = Color.white } };
                activeButton = new GUIStyle(button) { normal = { background = Solid(new Color(.29f, .40f, .25f, 1)), textColor = Color.white } };
            }
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.Box(new Rect(18, 18, 540, 128), GUIContent.none, panel);
            GUI.Label(new Rect(34, 27, 520, 42), "New_House · 90㎡养猫之家", title);
            GUI.Label(new Rect(34, 69, 520, 30), "两室一厅 · 厨房 · 阳台 · 猫咪活动区", caption);
            GUI.Label(new Rect(34, 102, 520, 28), "住宅查看模式 · 按 V 返回猫咪操控", caption);
            string[] names = { "1 全屋", "2 俯视", "3 客厅", "4 阳台" };
            for (int i = 0; i < 4; i++)
            {
                if (GUI.Button(new Rect(24 + i * 126, Screen.height / scale - 95, 118, 38), names[i], i == current ? activeButton : button)) SetView(i);
            }
            GUI.backgroundColor = Color.white;
            GUI.Box(new Rect(18, Screen.height / scale - 50, 1160, 37), GUIContent.none, panel);
            GUI.Label(new Rect(26, Screen.height / scale - 49, 1140, 34),
                "右键旋转 · 中键平移 · 滚轮缩放 · 室内 WASD 漫游 · R 复位 · H 隐藏说明 · F4 内容选择", caption);
            GUI.matrix = Matrix4x4.identity;
        }

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void VerifyIfRequested()
        {
            if (!Environment.GetCommandLineArgs().Contains("--check-new-house-selection")) return;
            var root = new GameObject("NewHouseSelectionVerification");
            DontDestroyOnLoad(root);
            root.AddComponent<NewHouseSelectionVerification>();
        }
    }
}
