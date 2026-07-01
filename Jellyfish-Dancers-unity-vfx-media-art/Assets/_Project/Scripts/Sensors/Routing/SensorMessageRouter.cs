using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Project.Sensors.Config;
using Project.Sensors.Events;
using Project.Sensors.Network;
using UnityEngine;

namespace Project.Sensors.Routing
{
    public class OscAddressState
    {
        public double LastReceivedTime;
        public long MessageCount;
        public float[] LastValues = Array.Empty<float>();
    }

    public class DeviceState
    {
        public bool IsConnected;
        public double LastPacketTime;
        public long PacketCount;
        public long DecodedMessageCount;
        public long DecodeFailureCount;
        public long MappedEventCount;
        public IPEndPoint RemoteEndpoint;
        public string LastOscAddress = string.Empty;
        public float[] LastValues = Array.Empty<float>();
        public string LastPacketPreview = string.Empty;
        public bool HasTouchBounds;
        public Vector2 ObservedTouchMin;
        public Vector2 ObservedTouchMax;
        public readonly Dictionary<string, OscAddressState> Addresses = new();
    }

    public class SensorMessageRouter : MonoBehaviour
    {
        [SerializeField] private SensorNetworkConfig config;
        [SerializeField] private SensorEventBus eventBus;

        private readonly Dictionary<string, DeviceState> states = new();
        private readonly Dictionary<string, double> lastLogTimes = new();
        private bool phoneTouchActive;
        private double lastPhoneTouchTime;

        private void Awake()
        {
            InitializeStates();
        }

        private void OnEnable()
        {
            states.Clear();
            lastLogTimes.Clear();
            phoneTouchActive = false;
            lastPhoneTouchTime = 0d;
            InitializeStates();
        }

        private bool InitializeStates()
        {
            if (config == null || config.phone == null || config.tablet == null)
            {
                return false;
            }

            EnsureState(config.phone.deviceId);
            EnsureState(config.tablet.deviceId);
            return true;
        }

        private void EnsureState(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || states.ContainsKey(deviceId))
            {
                return;
            }

            states.Add(deviceId, new DeviceState());
        }

        public void Route(ReceivedOscPacket packet)
        {
            if (!InitializeStates() ||
                !states.TryGetValue(packet.DeviceId, out var state))
            {
                return;
            }

            state.LastPacketTime = packet.ReceivedTime;
            state.PacketCount++;
            state.RemoteEndpoint = packet.RemoteEndPoint;

            if (!state.IsConnected)
            {
                state.IsConnected = true;
                eventBus.Publish(new DeviceConnectionEvent(packet.DeviceId, true, packet.RemoteEndPoint));
            }

            if (OscMessageDecoder.TryDecodePacket(packet.Data, out OscDecodedMessage[] messages))
            {
                foreach (OscDecodedMessage message in messages)
                {
                    state.DecodedMessageCount++;
                    state.LastOscAddress = message.Address;
                    state.LastValues = message.Values;
                    RecordAddressState(state, message.Address, message.Values, packet.ReceivedTime);

                    LogDecodedMessage(
                        packet.DeviceId,
                        message.Address,
                        message.Values,
                        packet.ReceivedTime
                    );

                    state.MappedEventCount += MapAndPublish(
                        packet.DeviceId,
                        message.Address,
                        message.Values,
                        packet.ReceivedTime,
                        packet.RemoteEndPoint
                    );
                }

                return;
            }

            state.DecodeFailureCount++;
            state.LastPacketPreview = DescribePacket(packet.Data);
            LogDecodeFailure(
                packet.DeviceId,
                packet.Data.Length,
                state.LastPacketPreview,
                packet.ReceivedTime
            );
        }

        private int MapAndPublish(string id, string address, float[] values, double time, IPEndPoint ep)
        {
            int mappedCount = 0;

            if (id == config.phone.deviceId)
            {
                if (TryReadVector2(address, values, config.oscMappings.phoneTouch, out Vector2 uv))
                {
                    RecordTouchBounds(id, uv);
                    uv = ConvertToUv(uv, config.oscMappings.phoneTouch);
                    eventBus.Publish(new TouchUvEvent(id, uv, time, ep));
                    mappedCount++;
                    mappedCount += PublishDerivedPhonePressOnTouch(id, time, ep);
                }

                if (TryReadScalar(address, values, config.oscMappings.phonePress, out float pressure))
                {
                    eventBus.Publish(new PressEvent(id, pressure, time, ep));
                    mappedCount++;
                }

                return mappedCount;
            }

            if (id != config.tablet.deviceId)
            {
                return 0;
            }

            if (TryReadVector3(address, values, config.oscMappings.tabletTilt, out Vector3 tilt))
            {
                eventBus.Publish(new VectorSensorEvent(id, SensorVectorType.Tilt, tilt, time, ep));
                mappedCount++;
            }

            if (TryReadVector3(address, values, config.oscMappings.tabletGyro, out Vector3 gyro))
            {
                eventBus.Publish(new VectorSensorEvent(id, SensorVectorType.Gyro, gyro, time, ep));
                mappedCount++;
            }

            if (TryReadVector3(address, values, config.oscMappings.tabletAcceleration, out Vector3 acceleration))
            {
                eventBus.Publish(new VectorSensorEvent(id, SensorVectorType.Acceleration, acceleration, time, ep));
                mappedCount++;
            }

            return mappedCount;
        }

        private static bool TryReadScalar(string address, float[] values, OscScalarMapping mapping, out float value)
        {
            value = 0f;

            if (mapping == null || !AddressMatches(address, mapping.address) || !HasIndex(values, mapping.valueIndex))
            {
                return false;
            }

            value = values[mapping.valueIndex];
            return true;
        }

        private static bool TryReadVector2(string address, float[] values, OscVector2Mapping mapping, out Vector2 value)
        {
            value = default;

            if (mapping == null ||
                !AddressMatches(address, mapping.address) ||
                !HasIndex(values, mapping.xIndex) ||
                !HasIndex(values, mapping.yIndex))
            {
                return false;
            }

            value = new Vector2(values[mapping.xIndex], values[mapping.yIndex]);
            return true;
        }

        private static bool TryReadVector3(string address, float[] values, OscVector3Mapping mapping, out Vector3 value)
        {
            value = default;

            if (mapping == null ||
                !AddressMatches(address, mapping.address) ||
                !HasIndex(values, mapping.xIndex) ||
                !HasIndex(values, mapping.yIndex) ||
                !HasIndex(values, mapping.zIndex))
            {
                return false;
            }

            value = new Vector3(
                values[mapping.xIndex],
                values[mapping.yIndex],
                values[mapping.zIndex]
            );

            return true;
        }

        private void RecordTouchBounds(string deviceId, Vector2 rawValue)
        {
            if (!states.TryGetValue(deviceId, out DeviceState state))
            {
                return;
            }

            if (!state.HasTouchBounds)
            {
                state.HasTouchBounds = true;
                state.ObservedTouchMin = rawValue;
                state.ObservedTouchMax = rawValue;
                return;
            }

            state.ObservedTouchMin = Vector2.Min(state.ObservedTouchMin, rawValue);
            state.ObservedTouchMax = Vector2.Max(state.ObservedTouchMax, rawValue);
        }

        private static Vector2 ConvertToUv(Vector2 rawValue, OscVector2Mapping mapping)
        {
            if (!mapping.normalizeTo01)
            {
                return rawValue;
            }

            float u = Mathf.InverseLerp(mapping.inputMin.x, mapping.inputMax.x, rawValue.x);
            float v = Mathf.InverseLerp(mapping.inputMin.y, mapping.inputMax.y, rawValue.y);

            if (mapping.invertY)
            {
                v = 1f - v;
            }

            var uv = new Vector2(u, v);
            return mapping.clamp01
                ? new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y))
                : uv;
        }

        private static bool AddressMatches(string actual, string configured)
        {
            if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(configured))
            {
                return false;
            }

            if (configured.EndsWith("*", StringComparison.Ordinal))
            {
                string prefix = configured.Substring(0, configured.Length - 1);
                return actual.StartsWith(prefix, StringComparison.Ordinal);
            }

            return actual.Equals(configured, StringComparison.Ordinal) ||
                   actual.EndsWith(configured, StringComparison.Ordinal);
        }

        private static bool HasIndex(float[] values, int index) =>
            values != null && index >= 0 && index < values.Length;

        private static void RecordAddressState(
            DeviceState deviceState,
            string address,
            float[] values,
            double receivedTime)
        {
            if (!deviceState.Addresses.TryGetValue(address, out OscAddressState addressState))
            {
                addressState = new OscAddressState();
                deviceState.Addresses.Add(address, addressState);
            }

            addressState.LastReceivedTime = receivedTime;
            addressState.MessageCount++;
            addressState.LastValues = values;
        }

        private void LogDecodedMessage(string deviceId, string address, float[] values, double now)
        {
            if (!config.logDecodedMessages || !ShouldLog(deviceId + address, now))
            {
                return;
            }

            Debug.Log($"[OSC:{deviceId}] {address} = [{string.Join(", ", values)}]", this);
        }

        private void LogDecodeFailure(string deviceId, int byteCount, string preview, double now)
        {
            if (!config.logDecodedMessages || !ShouldLog(deviceId + "#decode-failure", now))
            {
                return;
            }

            Debug.LogWarning(
                $"[OSC:{deviceId}] Decode failed, bytes={byteCount}, {preview}",
                this
            );
        }

        private static string DescribePacket(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return "empty packet";
            }

            int previewLength = Math.Min(data.Length, 32);
            var ascii = new StringBuilder(previewLength);
            var hex = new StringBuilder(previewLength * 3);

            for (int i = 0; i < previewLength; i++)
            {
                byte value = data[i];
                ascii.Append(value >= 32 && value <= 126 ? (char)value : '.');

                if (i > 0)
                {
                    hex.Append(' ');
                }

                hex.Append(value.ToString("X2"));
            }

            return $"ascii='{ascii}', hex={hex}";
        }

        private bool ShouldLog(string key, double now)
        {
            if (lastLogTimes.TryGetValue(key, out double lastTime) &&
                now - lastTime < config.logIntervalSeconds)
            {
                return false;
            }

            lastLogTimes[key] = now;
            return true;
        }

        private void Update()
        {
            if (!InitializeStates())
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            CheckTimeout(config.phone, now);
            CheckTimeout(config.tablet, now);
            UpdateDerivedPhonePress(now);
        }

        private void CheckTimeout(SensorEndpointConfig endpoint, double now)
        {
            if (endpoint == null ||
                !states.TryGetValue(endpoint.deviceId, out var state))
            {
                return;
            }

            if (state.IsConnected && (now - state.LastPacketTime > endpoint.timeoutSeconds))
            {
                state.IsConnected = false;
                eventBus.Publish(new DeviceConnectionEvent(endpoint.deviceId, false, state.RemoteEndpoint));
            }
        }

        private int PublishDerivedPhonePressOnTouch(string deviceId, double time, IPEndPoint endpoint)
        {
            if (!config.oscMappings.derivePhonePressFromTouch)
            {
                return 0;
            }

            lastPhoneTouchTime = time;

            if (phoneTouchActive)
            {
                return 0;
            }

            phoneTouchActive = true;
            eventBus.Publish(new PressEvent(deviceId, 1f, time, endpoint));
            return 1;
        }

        private void UpdateDerivedPhonePress(double now)
        {
            if (!phoneTouchActive ||
                !config.oscMappings.derivePhonePressFromTouch ||
                now - lastPhoneTouchTime <= config.oscMappings.phoneTouchReleaseSeconds)
            {
                return;
            }

            phoneTouchActive = false;

            if (!states.TryGetValue(config.phone.deviceId, out DeviceState state))
            {
                return;
            }

            eventBus.Publish(new PressEvent(
                config.phone.deviceId,
                0f,
                now,
                state.RemoteEndpoint
            ));
        }

        public DeviceState GetReadonlyState(string deviceId) => states.GetValueOrDefault(deviceId);
    }
}
