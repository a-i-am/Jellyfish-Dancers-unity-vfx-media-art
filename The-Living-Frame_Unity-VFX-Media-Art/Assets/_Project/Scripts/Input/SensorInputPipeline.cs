using UnityEngine;

namespace Project.Input
{
    public class SensorInputPipeline : MonoBehaviour
    {
        public struct SensorData
        {
            public Vector2 TouchUV;
            public Vector2 Tilt;
            public Vector3 Acceleration;
            public bool IsConnected;
        }

        private SensorData lastSensorData;

        public Vector2 GetTouchUV() => lastSensorData.TouchUV;
        public Vector2 GetTilt() => lastSensorData.Tilt;
        public Vector3 GetAcceleration() => lastSensorData.Acceleration;
        public bool IsConnected() => lastSensorData.IsConnected;

        public void UpdateTouchUV(Vector2 uv)
        {
            lastSensorData.TouchUV = new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
        }

        public void UpdateTilt(Vector2 tilt)
        {
            lastSensorData.Tilt = tilt;
        }

        public void UpdateAcceleration(Vector3 acc)
        {
            lastSensorData.Acceleration = acc;
        }

        public void SetConnected(bool connected)
        {
            lastSensorData.IsConnected = connected;
        }
    }
}
