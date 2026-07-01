using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Project.Sensors.Config;
using Project.Sensors.Events;
using Project.Sensors.Routing;
using UnityEngine;

namespace Project.Sensors.Network
{
    public class UdpOscReceiver : MonoBehaviour
    {
        [SerializeField] private SensorNetworkConfig config;
        [SerializeField] private SensorMessageRouter router;

        private UdpClient phoneClient;
        private UdpClient tabletClient;
        private CancellationTokenSource cts;

        private readonly ConcurrentQueue<ReceivedOscPacket> packetQueue = new();

        private void OnEnable()
        {

            Application.runInBackground = true;

            if (config == null || router == null)
            {
                Debug.LogError("[UDP Receiver] Config and Router references are required.", this);
                enabled = false;
                return;
            }

            cts = new CancellationTokenSource();
            StartListening(config.phone, ref phoneClient);
            StartListening(config.tablet, ref tabletClient);
        }

        private void StartListening(SensorEndpointConfig endpoint, ref UdpClient clientRef)
        {
            try
            {
                clientRef = new UdpClient(new IPEndPoint(IPAddress.Any, endpoint.listenPort));

                UdpClient localClient = clientRef;

                Task.Run(() => ReceiveLoop(localClient, endpoint.deviceId, cts.Token), cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UDP Receiver] 소켓 바인딩 실패 (Port:{endpoint.listenPort}): {e.Message}");
            }
        }

        private async Task ReceiveLoop(UdpClient client, string deviceId, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult result = await client.ReceiveAsync();

                    packetQueue.Enqueue(new ReceivedOscPacket(
                        deviceId, result.RemoteEndPoint, result.Buffer, 0d
                    ));
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { }
            }
        }

        private void Update()
        {
            while (packetQueue.TryDequeue(out var packet))
            {
                var timestampedPacket = new ReceivedOscPacket(
                    packet.DeviceId,
                    packet.RemoteEndPoint,
                    packet.Data,
                    Time.realtimeSinceStartupAsDouble
                );

                router.Route(timestampedPacket);
            }
        }

        private void OnDisable()
        {
            cts?.Cancel();

            CloseSocket(ref phoneClient);
            CloseSocket(ref tabletClient);

            cts?.Dispose();
        }

        private void CloseSocket(ref UdpClient client)
        {
            if (client == null)
            {
                return;
            }

            client.Close();
            client.Dispose();
            client = null;
        }
    }
}
