using System;
using UnityEngine;

namespace Project.Sensors.Config
{
    [Serializable]
    public class SensorEndpointConfig
    {
        public string deviceId;
        public int listenPort;
        public float timeoutSeconds = 2f;
    }

    [Serializable]
    public class OscScalarMapping
    {
        public string address;
        public int valueIndex;
    }

    [Serializable]
    public class OscVector2Mapping
    {
        public string address;
        public int xIndex;
        public int yIndex = 1;

        [Header("UV Conversion")]
        public bool normalizeTo01;
        public Vector2 inputMin = new(-1f, -1f);
        public Vector2 inputMax = new(1f, 1f);
        public bool invertY;
        public bool clamp01 = true;
    }

    [Serializable]
    public class OscVector3Mapping
    {
        public string address;
        public int xIndex;
        public int yIndex = 1;
        public int zIndex = 2;
    }

    [Serializable]
    public class SensorOscMappings
    {
        public OscVector2Mapping phoneTouch = new() { address = "/phone/touch*", xIndex = 0, yIndex = 1, normalizeTo01 = true, inputMin = new(-1f, -1.03f), inputMax = new(1f, 0.90f), clamp01 = true };
        public OscScalarMapping phonePress = new() { address = "/phone/press", valueIndex = 0 };
        public OscVector3Mapping tabletTilt = new() { address = "/tablet/gravity", xIndex = 0, yIndex = 1, zIndex = 2 };
        public OscVector3Mapping tabletGyro = new() { address = "/tablet/gyro", xIndex = 0, yIndex = 1, zIndex = 2 };
        public OscVector3Mapping tabletAcceleration = new() { address = "/tablet/accel", xIndex = 0, yIndex = 1, zIndex = 2 };

        [Header("Phone Touch State")]
        public bool derivePhonePressFromTouch = true;

        [Min(0.05f)]
        public float phoneTouchReleaseSeconds = 0.15f;
    }

    [CreateAssetMenu(menuName = "Living Frame/Sensor Network Config")]
    public class SensorNetworkConfig : ScriptableObject
    {
        public SensorEndpointConfig phone = new() { deviceId = "phone", listenPort = 9000, timeoutSeconds = 2f };
        public SensorEndpointConfig tablet = new() { deviceId = "tablet", listenPort = 9001, timeoutSeconds = 2f };
        public SensorOscMappings oscMappings = new();

        [Header("Packet Discovery")]
        public bool logDecodedMessages = false;

        [Min(0.1f)]
        public float logIntervalSeconds = 0.5f;
    }
}
