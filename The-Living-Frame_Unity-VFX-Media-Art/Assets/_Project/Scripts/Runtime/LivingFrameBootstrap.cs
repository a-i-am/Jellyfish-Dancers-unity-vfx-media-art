using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class LivingFrameBootstrap : MonoBehaviour
{
    [SerializeField] private string artworkTitle = "The Living Frame";
    [SerializeField] private WebcamParticleMeshRenderer webcamRenderer;
    [SerializeField] private MaskToShaderBinder magicMirror;
    [SerializeField] private bool enableWebcamOnStart = true;
    [SerializeField] private KeyCode enableWebcamKey = KeyCode.W;
    [SerializeField] private KeyCode disableWebcamKey = KeyCode.Escape;
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private KeyCode overlayToggleKey = KeyCode.F2;
    [SerializeField] private bool touchTogglesWebcam = true;
    [SerializeField] private bool preventDeviceSleep = true;
    [SerializeField] private int mobileTargetFrameRate = 60;
    [SerializeField] private int desktopTargetFrameRate = 60;
    [SerializeField] private float landscapeFieldOfView = 50f;
    [SerializeField] private float portraitFieldOfView = 62f;

#if UNITY_EDITOR
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
#endif
    private string controlHint = "W: start webcam  |  Esc: fallback";

    public string ArtworkTitle => artworkTitle;
    public bool ShowOverlay
    {
        get => showOverlay;
        set => showOverlay = value;
    }

    private void Reset()
    {
        if (webcamRenderer == null)
        {
            webcamRenderer = FindFirstObjectByType<WebcamParticleMeshRenderer>();
        }

        if (magicMirror == null)
        {
            magicMirror = FindFirstObjectByType<MaskToShaderBinder>();
        }
    }

    private void Start()
    {
        if (webcamRenderer == null)
        {
            webcamRenderer = FindFirstObjectByType<WebcamParticleMeshRenderer>();
        }

        if (magicMirror == null)
        {
            magicMirror = FindFirstObjectByType<MaskToShaderBinder>();
        }

        ApplyPlatformSettings();

        if (webcamRenderer != null)
        {
            enableWebcamOnStart = true;
            webcamRenderer.EnableWebcam();
        }
    }

    private void ApplyPlatformSettings()
    {
        if (Application.isMobilePlatform)
        {
            if (preventDeviceSleep)
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = mobileTargetFrameRate;
        }
        else
        {
            Application.targetFrameRate = desktopTargetFrameRate;
        }

#if UNITY_STANDALONE_WIN
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
#endif

        Camera camera = Camera.main;
        if (camera != null)
        {
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            camera.fieldOfView = aspect < 1f ? portraitFieldOfView : landscapeFieldOfView;
        }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && IsPressedThisFrame(keyboard, overlayToggleKey))
        {
            showOverlay = !showOverlay;
        }

        if (webcamRenderer == null)
        {
            return;
        }

        if (keyboard != null && IsPressedThisFrame(keyboard, enableWebcamKey))
        {
            webcamRenderer.EnableWebcam();
        }

        if (keyboard != null && IsPressedThisFrame(keyboard, disableWebcamKey))
        {
            webcamRenderer.DisableWebcam();
        }

        if (Application.isMobilePlatform && touchTogglesWebcam)
        {
            HandleTouchInput();
        }
    }

    private void HandleTouchInput()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            return;
        }

        if (!touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            return;
        }

        if (webcamRenderer.IsUsingWebcam)
        {
            webcamRenderer.DisableWebcam();
            return;
        }

        webcamRenderer.EnableWebcam();
    }

    private static bool IsPressedThisFrame(Keyboard keyboard, KeyCode keyCode)
    {
        var key = KeyControlForCode(keyboard, keyCode);
        return key != null && key.wasPressedThisFrame;
    }

    private static KeyControl KeyControlForCode(Keyboard keyboard, KeyCode keyCode)
    {
        switch (keyCode)
        {
            case KeyCode.W:
                return keyboard.wKey;
            case KeyCode.Escape:
                return keyboard.escapeKey;
            case KeyCode.F2:
                return keyboard.f2Key;
            default:
                return null;
        }
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!showOverlay)
        {
            return;
        }

        if (titleStyle == null || bodyStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 18;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;

            bodyStyle = new GUIStyle(GUI.skin.label);
            bodyStyle.fontSize = 12;
            bodyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        }

        if (webcamRenderer == null)
        {
            return;
        }

        bool showMagicMirrorState = magicMirror != null && magicMirror.isActiveAndEnabled;
        string mode = showMagicMirrorState ? magicMirror.CurrentModeLabel : webcamRenderer.CurrentModeLabel;
        string status = showMagicMirrorState ? magicMirror.CurrentStatusLabel : webcamRenderer.WebcamStatus;
        controlHint = Application.isMobilePlatform ? "Tap: toggle webcam" : "W: start webcam  |  Esc: fallback";
        float panelWidth = Mathf.Clamp(Screen.width * 0.34f, 220f, 300f);
        const float panelHeight = 96f;
        const float panelX = 12f;
        float panelY = Screen.height - panelHeight - 12f;

        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(panelX + 12f, panelY + 6f, panelWidth - 24f, 24f), artworkTitle, titleStyle);
        GUI.Label(new Rect(panelX + 12f, panelY + 32f, panelWidth - 24f, 18f), "Mode: " + mode, bodyStyle);
        GUI.Label(new Rect(panelX + 12f, panelY + 50f, panelWidth - 24f, 18f), "Status: " + status, bodyStyle);
        GUI.Label(new Rect(panelX + 12f, panelY + 68f, panelWidth - 24f, 18f), controlHint, bodyStyle);
    }
#endif
}
