using UnityEngine;
using Project.Sensors.Events;
using Project.VFX;

namespace Project.Input
{
    public class InputAggregator : MonoBehaviour
    {
        private InputState currentState;
        private WebcamParticleMeshRenderer webcamRenderer;
        private SensorEventBus sensorEventBus;
        private AttractorStage attractorStage;

        private Vector3 attractorPos;
        private Vector2 flowVector;
        private float burstEnergy;
        private float webcamMotionScore;
        private bool webcamActive;

        public InputState CurrentState => currentState;

        private void Awake()
        {
            webcamRenderer = FindFirstObjectByType<WebcamParticleMeshRenderer>();
            sensorEventBus = FindFirstObjectByType<SensorEventBus>();
            attractorStage = FindFirstObjectByType<AttractorStage>();

            if (sensorEventBus != null)
            {
                sensorEventBus.TouchUvReceived += OnTouchUvReceived;
                sensorEventBus.TiltReceived += OnTiltReceived;
                sensorEventBus.AccelerationReceived += OnAccelerationReceived;
                sensorEventBus.ConnectionChanged += OnConnectionChanged;
            }
        }

        private void Update()
        {
            webcamMotionScore = webcamRenderer != null ? Mathf.Clamp01(webcamRenderer.TotalMotionAmount) : 0f;
            webcamActive = webcamRenderer != null && webcamRenderer.IsUsingWebcam;

            currentState = new InputState(
                attractorPos,
                flowVector,
                burstEnergy,
                webcamMotionScore,
                webcamActive,
                0);

            burstEnergy *= 0.95f;
        }

        private void OnTouchUvReceived(TouchUvEvent evt)
        {
            if (attractorStage != null)
            {
                float x = Mathf.Lerp(attractorStage.XBounds.x, attractorStage.XBounds.y, evt.Uv.x);
                float y = Mathf.Lerp(attractorStage.YBounds.x, attractorStage.YBounds.y, evt.Uv.y);
                float z = attractorStage.ZBounds.x + (attractorStage.ZBounds.y - attractorStage.ZBounds.x) * 0.5f;
                attractorPos = new Vector3(x, y, z);
            }
        }

        private void OnTiltReceived(VectorSensorEvent evt)
        {
            flowVector = new Vector2(evt.Value.x, evt.Value.y);
        }

        private void OnAccelerationReceived(VectorSensorEvent evt)
        {
            burstEnergy = Mathf.Max(burstEnergy, evt.Value.magnitude * 0.1f);
        }

        private void OnConnectionChanged(DeviceConnectionEvent evt)
        {
            if (!evt.IsConnected)
            {
                flowVector = Vector2.zero;
                burstEnergy = 0f;
            }
        }

        private void OnDestroy()
        {
            if (sensorEventBus != null)
            {
                sensorEventBus.TouchUvReceived -= OnTouchUvReceived;
                sensorEventBus.TiltReceived -= OnTiltReceived;
                sensorEventBus.AccelerationReceived -= OnAccelerationReceived;
                sensorEventBus.ConnectionChanged -= OnConnectionChanged;
            }
        }
    }
}
