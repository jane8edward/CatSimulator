using UnityEngine;

// Plays the baked walk and matches world travel to its stride.
public sealed class CatWalkPlayback : MonoBehaviour
{
    public Animator animator;
    public Transform subject;
    public Transform previewCamera;
    public Vector3 forward = Vector3.forward;
    public float cycleSeconds = 1.2f;
    public float strideMeters = 0.28f;
    public float playbackSpeed = 1f;
    public bool travelForward = true;
    public bool paused;
    public bool showControls = true;
    public float previewLoopDistance;
    public int observedFrames;
    public float elapsed;
    private Vector3 startPosition;
    private Vector3 cameraOffset;
    private Font uiFont;
    private GUIStyle labelStyle, buttonStyle;

    void Awake()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        if (!subject) subject = transform;
        if (!animator) animator = subject.GetComponent<Animator>();
        startPosition = subject.position;
        if (previewCamera) cameraOffset = previewCamera.position - subject.position;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        uiFont = Resources.Load<Font>("NotoSansSC");
    }

    void Update()
    {
        if (animator) animator.speed = paused ? 0 : playbackSpeed;
        if (paused) return;
        observedFrames++;
        elapsed += Time.deltaTime * playbackSpeed;
        if (travelForward)
        {
            subject.position += forward * (strideMeters / cycleSeconds) * Time.deltaTime * playbackSpeed;
            float distance = Vector3.Dot(subject.position - startPosition, forward);
            if (previewLoopDistance > 0 && distance >= previewLoopDistance)
                subject.position -= forward * previewLoopDistance * Mathf.Floor(distance / previewLoopDistance);
        }
    }

    void LateUpdate()
    {
        if (!previewCamera || !subject) return;
        previewCamera.position = subject.position + cameraOffset;
        previewCamera.LookAt(subject.position + Vector3.up * 0.29f);
    }

    public void ResetPreview()
    {
        subject.position = startPosition;
        elapsed = 0;
        if (animator) { animator.Play("Walk", 0, 0); animator.Update(0); }
    }

    void OnGUI()
    {
        if (!showControls) return;
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 16 };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = uiFont, fontSize = 15 };
        }
        GUILayout.BeginArea(new Rect(18, 18, 340, 155), GUI.skin.box);
        GUILayout.Label("阿比尼西亚猫 · 自然行走", labelStyle);
        GUILayout.Label(travelForward ? "向前行走" : "原地步态预览", labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(paused ? "播放" : "暂停", buttonStyle)) paused = !paused;
        if (GUILayout.Button(travelForward ? "切换原地" : "切换前进", buttonStyle)) travelForward = !travelForward;
        if (GUILayout.Button("重置", buttonStyle)) ResetPreview();
        GUILayout.EndHorizontal();
        GUILayout.Label("速度 " + playbackSpeed.ToString("0.00") + "×", labelStyle);
        playbackSpeed = GUILayout.HorizontalSlider(playbackSpeed, 0.25f, 1.5f);
        GUILayout.EndArea();
    }
}
