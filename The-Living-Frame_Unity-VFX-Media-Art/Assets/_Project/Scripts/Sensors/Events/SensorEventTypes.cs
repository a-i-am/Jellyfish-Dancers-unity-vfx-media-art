using System.Net;
using UnityEngine;

namespace Project.Sensors.Events
{
    public readonly struct ReceivedOscPacket
    {
        public readonly string DeviceId;
        public readonly IPEndPoint RemoteEndPoint;
        public readonly byte[] Data;
        public readonly double ReceivedTime;

        public ReceivedOscPacket(string deviceId, IPEndPoint endPoint, byte[] data, double receivedTime)
        {
            DeviceId = deviceId;
            RemoteEndPoint = endPoint;
            Data = data;
            ReceivedTime = receivedTime;
        }
    }
    public enum SensorVectorType { Tilt, Gyro, Acceleration }

    public readonly struct TouchUvEvent
    {
        public readonly string DeviceId;
        public readonly Vector2 Uv;
        public readonly double Timestamp;
        public readonly IPEndPoint Endpoint;
        public TouchUvEvent(string id, Vector2 uv, double time, IPEndPoint ep)
        {
            DeviceId = id; Uv = uv; Timestamp = time; Endpoint = ep;
        }
    }
    public readonly struct PressEvent
    {
        public readonly string DeviceId;
        public readonly float Pressure;
        public readonly double Timestamp;
        public readonly IPEndPoint Endpoint;
        public PressEvent(string id, float pressure, double time, IPEndPoint ep)
        {
            DeviceId = id; Pressure = pressure; Timestamp = time; Endpoint = ep;
        }
    }

    public readonly struct VectorSensorEvent
    {
        public readonly string DeviceId;
        public readonly SensorVectorType SensorType;
        public readonly Vector3 Value;
        public readonly double Timestamp;
        public readonly IPEndPoint Endpoint;

        public VectorSensorEvent(string id, SensorVectorType type, Vector3 val, double time, IPEndPoint ep)
        {
            DeviceId = id; SensorType = type; Value = val; Timestamp = time; Endpoint = ep;
        }
    }
    public readonly struct DeviceConnectionEvent
    {
        public readonly string DeviceId;
        public readonly bool IsConnected;
        public readonly IPEndPoint Endpoint;

        public DeviceConnectionEvent(string id, bool isConnected, IPEndPoint ep)
        {
            DeviceId = id; IsConnected = isConnected; Endpoint = ep;
        }
    }
}
