using System;
using UnityEngine;

namespace Project.Sensors.Events
{
    public sealed class SensorEventBus : MonoBehaviour
    {
        public event Action<TouchUvEvent> TouchUvReceived;
        public event Action<PressEvent> PressReceived;
        public event Action<VectorSensorEvent> TiltReceived;
        public event Action<VectorSensorEvent> GyroReceived;
        public event Action<VectorSensorEvent> AccelerationReceived;
        public event Action<DeviceConnectionEvent> ConnectionChanged;

        public void Publish(TouchUvEvent value) => TouchUvReceived?.Invoke(value);
        public void Publish(PressEvent value) => PressReceived?.Invoke(value);
        public void Publish(VectorSensorEvent value)
        {
            switch (value.SensorType)
            {
                case SensorVectorType.Tilt: TiltReceived?.Invoke(value); break;
                case SensorVectorType.Gyro: GyroReceived?.Invoke(value); break;
                case SensorVectorType.Acceleration: AccelerationReceived?.Invoke(value); break;
            }
        }
        public void Publish(DeviceConnectionEvent value) => ConnectionChanged?.Invoke(value);
    }
}

