using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Project.Sensors.Network
{
    public sealed class JellyfishTcpServer : MonoBehaviour
    {
        [SerializeField] private int port = 9100;

        private TcpListener listener;
        private CancellationTokenSource cts;
        private readonly ConcurrentQueue<SpawnRequest> requestQueue = new();

        public struct SpawnRequest
        {
            public float h;
            public float spd;
            public int pat;
        }

        public bool TryDequeueRequest(out SpawnRequest request)
        {
            return requestQueue.TryDequeue(out request);
        }

        private void OnEnable()
        {
            cts = new CancellationTokenSource();
            StartServer();
        }

        private void OnDisable()
        {
            StopServer();
        }

        private void StartServer()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                Debug.Log($"[JellyfishTcpServer] Listening on port {port}");
                Task.Run(() => AcceptLoopAsync(cts.Token), cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JellyfishTcpServer] {e.Message}");
            }
        }

        private void StopServer()
        {
            cts?.Cancel();
            try
            {
                listener?.Stop();
            }
            catch (Exception) { }
            cts?.Dispose();
            cts = null;
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    Debug.Log("[JellyfishTcpServer] Client connected");
                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (!token.IsCancellationRequested && client.Connected)
                {
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                    {
                        break;
                    }
                    Debug.Log($"[JellyfishTcpServer] Received raw: {line}");
                    ParseAndQueueRequest(line);
                }
            }
        }

        [System.Serializable]
        private class OldParams
        {
            public string label;
            public string category;
            public float intensity;
        }

        [System.Serializable]
        private class OldRequest
        {
            public string jsonrpc;
            public string method;
            public OldParams @params;
        }

        private void ParseAndQueueRequest(string json)
        {
            try
            {
                if (json.Contains("\"h\"") || json.Contains("\"spd\"") || json.Contains("\"pat\""))
                {
                    SpawnRequest req = JsonUtility.FromJson<SpawnRequest>(json);
                    requestQueue.Enqueue(req);
                    Debug.Log($"[JellyfishTcpServer] Queued JSON payload: h={req.h}, spd={req.spd}, pat={req.pat}");
                }
                else if (json.Contains("\"jsonrpc\"") && json.Contains("\"params\""))
                {
                    OldRequest old = JsonUtility.FromJson<OldRequest>(json);
                    if (old != null && old.@params != null)
                    {
                        float h = 0.5f;
                        float spd = old.@params.intensity;
                        int pat = 1;
                        string cat = old.@params.category != null ? old.@params.category.ToLowerInvariant() : "";
                        if (cat == "calm" || cat == "fatigue")
                        {
                            h = 0.0f;
                            pat = 0;
                        }
                        else if (cat == "joy" || cat == "hope")
                        {
                            h = 0.5f;
                            pat = 1;
                        }
                        else if (cat == "anger" || cat == "anxiety" || cat == "sadness" || cat == "pressure")
                        {
                            h = 1.0f;
                            pat = 2;
                        }
                        SpawnRequest req = new SpawnRequest { h = h, spd = spd, pat = pat };
                        requestQueue.Enqueue(req);
                        Debug.Log($"[JellyfishTcpServer] Queued translated legacy payload: h={req.h}, spd={req.spd}, pat={req.pat}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JellyfishTcpServer] Parse error: {e.Message}");
            }
        }
    }
}
