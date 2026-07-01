using System;
using System.Collections;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class ThoughtEssenceWheel : MonoBehaviour
{
    [SerializeField] private Text questionText;
    [SerializeField] private Slider emotionSlider;
    [SerializeField] private Button sendButton;
    [SerializeField] private InputField ipInputField;
    [SerializeField] private Text statusText;

    private string targetIp = "127.0.0.1";

    [Serializable]
    private struct EmotionPayload
    {
        public float h;
        public float spd;
        public int pat;
    }

    private void Awake()
    {
        sendButton.onClick.AddListener(OnSendClicked);
    }

    private void OnSendClicked()
    {
        if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
        {
            targetIp = ipInputField.text.Trim();
        }
        float sliderValue = emotionSlider != null ? emotionSlider.value : 0.5f;
        float speed = 1.1f;
        int pattern = 1;
        EvaluatePayload(sliderValue, out speed, out pattern);
        EmotionPayload payload = new EmotionPayload
        {
            h = sliderValue,
            spd = speed,
            pat = pattern
        };
        string json = JsonUtility.ToJson(payload);
        StartCoroutine(SendRequestCoroutine(json));
    }

    private void EvaluatePayload(float h, out float speed, out int pattern)
    {
        h = Mathf.Clamp01(h);
        if (h < 0.5f)
        {
            float t = h / 0.5f;
            speed = Mathf.Lerp(0.7f, 1.1f, t);
        }
        else
        {
            float t = (h - 0.5f) / 0.5f;
            speed = Mathf.Lerp(1.1f, 1.5f, t);
        }
        if (h < 0.25f)
        {
            pattern = 0;
        }
        else if (h < 0.75f)
        {
            pattern = 1;
        }
        else
        {
            pattern = 2;
        }
    }

    private IEnumerator SendRequestCoroutine(string payloadJson)
    {
        if (statusText != null) statusText.text = "Sending...";
        sendButton.interactable = false;
        bool success = false;
        string errorMsg = "";
        yield return StartCoroutine(SendTcpMessage(payloadJson, (res, err) =>
        {
            success = res;
            errorMsg = err;
        }));
        if (statusText != null)
        {
            statusText.text = success ? "Sent successfully!" : "Failed: " + errorMsg;
        }
        sendButton.interactable = true;
    }

    private IEnumerator SendTcpMessage(string payload, Action<bool, string> callback)
    {
        bool done = false;
        bool success = false;
        string err = "";
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    var result = client.BeginConnect(targetIp, 9100, null, null);
                    var successConnect = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3.0));
                    if (!successConnect)
                    {
                        throw new Exception("Timeout connecting to " + targetIp);
                    }
                    client.EndConnect(result);
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] data = Encoding.UTF8.GetBytes(payload + "\n");
                        stream.Write(data, 0, data.Length);
                        stream.Flush();
                    }
                }
                success = true;
            }
            catch (Exception e)
            {
                err = e.Message;
                success = false;
            }
            finally
            {
                System.Threading.Volatile.Write(ref done, true);
            }
        });
        while (!System.Threading.Volatile.Read(ref done))
        {
            yield return null;
        }
        callback(success, err);
    }
}
