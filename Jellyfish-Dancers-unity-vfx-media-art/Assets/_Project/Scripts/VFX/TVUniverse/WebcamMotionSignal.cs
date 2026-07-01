using UnityEngine;
using UnityEngine.Rendering;

namespace Project.VFX.TVUniverse
{
    public sealed class WebcamMotionSignal : MonoBehaviour
    {
        [SerializeField] private MotionDetect motionDetect;
        [SerializeField] private float noiseFloor = 0.02f;
        [SerializeField] private float motionCeiling = 0.25f;
        [SerializeField] private float damping = 0.15f;
        [SerializeField, Range(5, 10)] private int readbackFrequency = 10;

        private float normalizedMotion;
        private float motionVelocity;
        private float lastReadbackTime;

        private Texture2D fallbackTex;
        private RenderTexture temp1x1;
        private bool isReadbackPending;

        public float NormalizedMotion => normalizedMotion;

        private void Start()
        {
            if (motionDetect == null)
            {
                motionDetect = FindObjectOfType<MotionDetect>();
            }

            temp1x1 = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32);
            fallbackTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        }

        private void Update()
        {
            if (motionDetect == null || motionDetect.MotionMap == null)
            {
                normalizedMotion = Mathf.SmoothDamp(normalizedMotion, 0f, ref motionVelocity, damping);
                return;
            }

            float interval = 1f / readbackFrequency;
            if (Time.time - lastReadbackTime >= interval && !isReadbackPending)
            {
                Graphics.Blit(motionDetect.MotionMap, temp1x1);

                if (SystemInfo.supportsAsyncGPUReadback)
                {
                    isReadbackPending = true;
                    AsyncGPUReadback.Request(temp1x1, 0, OnReadbackComplete);
                }
                else
                {
                    PerformFallbackReadback();
                }

                lastReadbackTime = Time.time;
            }
        }

        private void OnReadbackComplete(AsyncGPUReadbackRequest request)
        {
            isReadbackPending = false;

            if (request.hasError)
            {
                return;
            }

            var data = request.GetData<Color32>();
            if (data.Length > 0)
            {
                ProcessRawMotion(data[0].r / 255f);
            }
        }

        private void PerformFallbackReadback()
        {
            RenderTexture activeBefore = RenderTexture.active;
            RenderTexture.active = temp1x1;
            fallbackTex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
            fallbackTex.Apply();
            RenderTexture.active = activeBefore;

            Color pixelColor = fallbackTex.GetPixel(0, 0);
            ProcessRawMotion(pixelColor.r);
        }

        private void ProcessRawMotion(float rawValue)
        {
            float motionVal = Mathf.Max(0f, rawValue - noiseFloor);
            float targetMotion = Mathf.Clamp01(motionVal / (motionCeiling - noiseFloor));
            normalizedMotion = Mathf.SmoothDamp(normalizedMotion, targetMotion, ref motionVelocity, damping);
        }

        private void OnDestroy()
        {
            if (fallbackTex != null)
            {
                Destroy(fallbackTex);
            }

            if (temp1x1 != null)
            {
                temp1x1.Release();
            }
        }
    }
}
