using Project.Sensors.Config;
using Project.Sensors.Events;
using Project.Sensors.Routing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Sensors.Diagnostics
{
    public class SensorNetworkDiagnostics : MonoBehaviour
    {
        [SerializeField] private SensorNetworkConfig config;
        [SerializeField] private SensorMessageRouter router;
        [SerializeField] private SensorEventBus eventBus;
        [SerializeField] private bool showDiagnostics = true;

        private Vector2 lastTouch;
        private float lastPress;
        private Vector3 lastTilt;
        private Vector3 lastGyro;
        private Vector3 lastAcceleration;
        private bool hasTouch;
        private bool hasPress;
        private bool hasTilt;
        private bool hasGyro;
        private bool hasAcceleration;
        private Vector2 scrollPosition;

        public bool ShowDiagnostics
        {
            get => showDiagnostics;
            set => showDiagnostics = value;
        }

        private void Reset()
        {
            router = GetComponent<SensorMessageRouter>();
            eventBus = GetComponent<SensorEventBus>();
        }

        private void Awake()
        {
            router ??= GetComponent<SensorMessageRouter>();
            eventBus ??= GetComponent<SensorEventBus>();
        }

        private void OnEnable()
        {
            if (eventBus == null)
            {
                return;
            }

            eventBus.TouchUvReceived += OnTouch;
            eventBus.PressReceived += OnPress;
            eventBus.TiltReceived += OnTilt;
            eventBus.GyroReceived += OnGyro;
            eventBus.AccelerationReceived += OnAcceleration;
        }

        private void OnDisable()
        {
            if (eventBus == null)
            {
                return;
            }

            eventBus.TouchUvReceived -= OnTouch;
            eventBus.PressReceived -= OnPress;
            eventBus.TiltReceived -= OnTilt;
            eventBus.GyroReceived -= OnGyro;
            eventBus.AccelerationReceived -= OnAcceleration;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                showDiagnostics = !showDiagnostics;
            }
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDiagnostics || config == null || router == null)
            {
                return;
            }

            float panelWidth = Mathf.Clamp(Screen.width * 0.55f, 320f, 620f);
            float panelHeight = Mathf.Clamp(Screen.height * 0.34f, 120f, 220f);
            float panelX = Screen.width - panelWidth - 12f;
            float panelY = Screen.height - panelHeight - 12f;

            GUILayout.BeginArea(
                new Rect(panelX, panelY, panelWidth, panelHeight),
                "Sensor Diagnostics  |  F3: Hide",
                GUI.skin.window
            );
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            RenderDeviceOverview(config.phone.deviceId);
            GUILayout.Label($"Touch UV: {(hasTouch ? lastTouch.ToString("F3") : "N/A")}");
            GUILayout.Label($"Press: {(hasPress ? lastPress.ToString("F3") : "N/A")}");
            GUILayout.Space(15);
            RenderDeviceOverview(config.tablet.deviceId);
            GUILayout.Label($"Tilt: {(hasTilt ? lastTilt.ToString("F3") : "N/A")}");
            GUILayout.Label($"Gyro: {(hasGyro ? lastGyro.ToString("F3") : "N/A")}");
            GUILayout.Label($"Acceleration: {(hasAcceleration ? lastAcceleration.ToString("F3") : "N/A")}");

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void RenderDeviceOverview(string deviceId)
        {
            var state = router.GetReadonlyState(deviceId);
            if (state == null)
            {
                return;
            }

            string statusTxt = state.PacketCount == 0
                ? "Waiting"
                : state.IsConnected ? "Connected" : "Timeout";
            string age = state.PacketCount == 0
                ? "N/A"
                : $"{Time.realtimeSinceStartupAsDouble - state.LastPacketTime:F2}s";

            GUILayout.Label(deviceId.ToUpper());
            GUILayout.Label($"Status: {statusTxt}");
            GUILayout.Label($"Endpoint: {state.RemoteEndpoint?.ToString() ?? "N/A"}");
            GUILayout.Label($"Packets: {state.PacketCount} | Decoded: {state.DecodedMessageCount} | Mapped: {state.MappedEventCount}");
            GUILayout.Label($"Decode failures: {state.DecodeFailureCount}");
            GUILayout.Label($"Last OSC: {state.LastOscAddress}");
            GUILayout.Label($"Last values: [{string.Join(", ", state.LastValues)}]");
            GUILayout.Label($"Packet preview: {state.LastPacketPreview}");
            if (state.HasTouchBounds)
            {
                GUILayout.Label($"Observed touch min: {state.ObservedTouchMin.ToString("F3")}");
                GUILayout.Label($"Observed touch max: {state.ObservedTouchMax.ToString("F3")}");
            }
            GUILayout.Label($"Last age: {age}");

            foreach (var pair in state.Addresses)
            {
                OscAddressState addressState = pair.Value;
                double addressAge = Time.realtimeSinceStartupAsDouble - addressState.LastReceivedTime;
                GUILayout.Label(
                    $"{pair.Key}: [{string.Join(", ", addressState.LastValues)}] " +
                    $"count={addressState.MessageCount}, age={addressAge:F2}s"
                );
            }
        }
#endif

        private void OnTouch(TouchUvEvent value)
        {
            lastTouch = value.Uv;
            hasTouch = true;
        }

        private void OnPress(PressEvent value)
        {
            lastPress = value.Pressure;
            hasPress = true;
        }

        private void OnTilt(VectorSensorEvent value)
        {
            lastTilt = value.Value;
            hasTilt = true;
        }

        private void OnGyro(VectorSensorEvent value)
        {
            lastGyro = value.Value;
            hasGyro = true;
        }

        private void OnAcceleration(VectorSensorEvent value)
        {
            lastAcceleration = value.Value;
            hasAcceleration = true;
        }
    }
}
