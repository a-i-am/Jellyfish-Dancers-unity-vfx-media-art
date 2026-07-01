using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WebcamParticleMeshRenderer : MonoBehaviour
{
    private enum CameraFacing
    {
        Any,
        Front,
        Back
    }

    [Header("Input")]
    [SerializeField] private bool startWebcamAutomatically;
    [SerializeField] private bool useRequestedFormat;
    [SerializeField] private int requestedWebcamWidth = 1280;
    [SerializeField] private int requestedWebcamHeight = 720;
    [SerializeField] private int requestedFps = 30;
    [SerializeField] private bool mirrorX = true;
    public bool MirrorX => mirrorX;
    [SerializeField] private bool useAnimatedFallbackWhenNoCamera = true;

    [Header("Mobile")]
    [SerializeField] private bool requestMobileWebcamPermission = true;
    [SerializeField] private CameraFacing preferredMobileCamera = CameraFacing.Front;
    [SerializeField] private bool mirrorFrontFacingCamera = true;
    [SerializeField] private bool useMobileGridResolution = true;
    [SerializeField] private int mobileColumns = 96;
    [SerializeField] private int mobileRows = 54;

    [Header("Particle Grid")]
    [SerializeField] private int columns = 160;
    [SerializeField] private int rows = 90;
    [SerializeField] private float displayWidth = 3.2f;
    [SerializeField] private float displayHeight = 1.8f;
    [SerializeField] private float particleSize = 0.014f;
    [SerializeField] private float displacement = 0.65f;
    [SerializeField] private float brightnessBoost = 1.2f;
    [SerializeField] private float motionInfluence = 1.15f;
    [SerializeField] private float motionThreshold = 0.045f;
    [SerializeField] private float motionSizeBoost = 1.75f;

    [Header("Clones & Echoes")]
    [SerializeField, Range(1, 5)] private int cloneCount = 3;
    [SerializeField] private float delaySeconds = 0.6f;
    [SerializeField] private float driftSpeed = 0.35f;
    [SerializeField] private float driftRadius = 1.2f;
    [SerializeField] private float silhouetteThreshold = 0.08f;
    [SerializeField] private float silhouetteMultiplier = 2.0f;

    private WebCamTexture webcam;
    private Mesh mesh;
    private Vector3[] vertices;
    private Color32[] colors;
    private Vector2[] uvs;
    private Color32[] webcamPixels;
    private float[] previousLuma;
    private bool warnedNoCamera;
    private int framesWithoutWebcamData;
    private bool retriedWithDefaultFormat;
    private bool isRequestingPermission;
    private bool runtimeMirrorX;
    private string webcamStatus = "Fallback Motion";
    private Vector2 motionCentroid = new Vector2(0.5f, 0.5f);
    private float totalMotionAmount;
    private Vector2 motionVelocity = Vector2.zero;
    private Vector2 lastCentroid = new Vector2(0.5f, 0.5f);
    private struct HistoryFrame
    {
        public Color32[] pixels;
        public float time;
    }
    private System.Collections.Generic.List<HistoryFrame> historyBuffer = new System.Collections.Generic.List<HistoryFrame>();
    private int maxHistoryFrames;

    public Vector2 MotionCentroid => motionCentroid;
    public float TotalMotionAmount => totalMotionAmount;
    public Vector2 MotionVelocity => motionVelocity;

    public bool TryGetMotionWorldPoint(out Vector3 worldPoint)
    {
        Vector2 centroid = motionCentroid;
        float x = Mathf.Lerp(-displayWidth * 0.5f, displayWidth * 0.5f, centroid.x);
        float y = Mathf.Lerp(-displayHeight * 0.5f, displayHeight * 0.5f, centroid.y);
        worldPoint = transform.TransformPoint(new Vector3(x, y, 0f));
        return totalMotionAmount > 0.0001f;
    }

    public WebCamTexture WebcamTexture
    {
        get { return webcam; }
    }

    public bool IsUsingWebcam
    {
        get { return webcam != null && webcam.isPlaying; }
    }

    public string CurrentModeLabel
    {
        get { return IsUsingWebcam ? "Webcam Live" : "Fallback Motion"; }
    }

    public string WebcamStatus
    {
        get { return webcamStatus; }
    }

    private void Awake()
    {
        if (Application.isMobilePlatform && useMobileGridResolution)
        {
            columns = mobileColumns;
            rows = mobileRows;
        }

        columns = Mathf.Max(8, columns);
        rows = Mathf.Max(8, rows);
        mobileColumns = Mathf.Max(8, mobileColumns);
        mobileRows = Mathf.Max(8, mobileRows);
        motionInfluence = Mathf.Max(0f, motionInfluence);
        motionThreshold = Mathf.Clamp01(motionThreshold);
        motionSizeBoost = Mathf.Max(1f, motionSizeBoost);
        runtimeMirrorX = mirrorX;
        maxHistoryFrames = Mathf.Max(30, Mathf.RoundToInt(cloneCount * delaySeconds * 30f));
        BuildMesh();
    }

    private void Start()
    {
        if (!startWebcamAutomatically)
        {
            warnedNoCamera = true;
            return;
        }

        if (WebCamTexture.devices.Length == 0)
        {
            warnedNoCamera = true;
            webcamStatus = "No webcam device";
            Debug.LogWarning("No webcam device found. WebcamParticleMeshRenderer is using animated fallback colors.", this);
            return;
        }

        EnableWebcam();
    }

    private void Update()
    {
        if (webcam != null && webcam.didUpdateThisFrame)
        {
            framesWithoutWebcamData = 0;
            retriedWithDefaultFormat = false;
            webcamStatus = "Webcam Live";
            UpdateFromWebcam();
            return;
        }

        if (webcam != null)
        {
            framesWithoutWebcamData++;
        }

        bool webcamUnavailable = webcam == null || !webcam.isPlaying || framesWithoutWebcamData > 120;
        if (webcam != null && framesWithoutWebcamData > 120 && useRequestedFormat && !retriedWithDefaultFormat)
        {
            retriedWithDefaultFormat = true;
            RestartWebcamWithDefaultFormat();
            return;
        }

        if (webcamUnavailable && useAnimatedFallbackWhenNoCamera)
        {
            if (webcam != null && framesWithoutWebcamData > 120)
            {
                webcamStatus = "Fallback Motion";
            }
            UpdateFallback();
        }
    }

    private void OnDestroy()
    {
        if (webcam != null)
        {
            webcam.Stop();
        }

        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    public void EnableWebcam()
    {
        if (webcam != null && webcam.isPlaying)
        {
            return;
        }

        if (isRequestingPermission)
        {
            return;
        }

        StartCoroutine(EnableWebcamRoutine());
    }

    private IEnumerator EnableWebcamRoutine()
    {
        if (requestMobileWebcamPermission && Application.isMobilePlatform && !Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            isRequestingPermission = true;
            webcamStatus = "Requesting webcam permission";
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            isRequestingPermission = false;

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                webcamStatus = "Webcam permission denied";
                Debug.LogWarning("Webcam permission denied. WebcamParticleMeshRenderer will keep showing fallback colors.", this);
                yield break;
            }
        }

        if (WebCamTexture.devices.Length == 0)
        {
            webcamStatus = "No webcam device";
            Debug.LogWarning("No webcam device found. WebcamParticleMeshRenderer will keep showing fallback colors.", this);
            yield break;
        }

        DisableWebcam();
        webcam = CreateWebcam(useRequestedFormat);
        webcamStatus = useRequestedFormat ? "Trying requested webcam format" : "Trying default webcam format";
        webcam.Play();
        framesWithoutWebcamData = 0;
    }

    public void DisableWebcam()
    {
        if (webcam == null)
        {
            return;
        }

        webcam.Stop();
        Destroy(webcam);
        webcam = null;
        framesWithoutWebcamData = 0;
        retriedWithDefaultFormat = false;
        webcamStatus = "Fallback Motion";
        totalMotionAmount = 0f;
        motionVelocity = Vector2.zero;
        historyBuffer.Clear();
    }

    private WebCamTexture CreateWebcam(bool useRequested)
    {
        var device = SelectWebcamDevice();
        runtimeMirrorX = mirrorX || (Application.isMobilePlatform && mirrorFrontFacingCamera && device.isFrontFacing);
        var created = new WebCamTexture(device.name);
        if (useRequested)
        {
            created.requestedWidth = requestedWebcamWidth;
            created.requestedHeight = requestedWebcamHeight;
            created.requestedFPS = requestedFps;
        }
        return created;
    }

    private void RestartWebcamWithDefaultFormat()
    {
        DisableWebcam();
        webcam = CreateWebcam(false);
        webcamStatus = "Retrying webcam with default format";
        webcam.Play();
        framesWithoutWebcamData = 0;
    }

    private WebCamDevice SelectWebcamDevice()
    {
        var devices = WebCamTexture.devices;
        if (!Application.isMobilePlatform || preferredMobileCamera == CameraFacing.Any)
        {
            return devices[0];
        }

        bool wantFrontFacing = preferredMobileCamera == CameraFacing.Front;
        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i].isFrontFacing == wantFrontFacing)
            {
                return devices[i];
            }
        }

        return devices[0];
    }

    private void BuildMesh()
    {
        var meshFilter = GetComponent<MeshFilter>();
        vertices = new Vector3[columns * rows * 4 * cloneCount];
        colors = new Color32[vertices.Length];
        uvs = new Vector2[vertices.Length];
        previousLuma = new float[columns * rows];
        int[] triangles = new int[columns * rows * 6 * cloneCount];

        int vertexIndex = 0;
        int triangleIndex = 0;
        for (int c = cloneCount - 1; c >= 0; c--)
        {
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float u = columns <= 1 ? 0f : x / (float)(columns - 1);
                    float v = rows <= 1 ? 0f : y / (float)(rows - 1);
                    WriteQuad(vertexIndex, u, v, new Color32(0, 0, 0, 0), 0f, particleSize, 0f, 0f);

                    triangles[triangleIndex++] = vertexIndex;
                    triangles[triangleIndex++] = vertexIndex + 1;
                    triangles[triangleIndex++] = vertexIndex + 2;
                    triangles[triangleIndex++] = vertexIndex;
                    triangles[triangleIndex++] = vertexIndex + 2;
                    triangles[triangleIndex++] = vertexIndex + 3;
                    vertexIndex += 4;
                }
            }
        }

        mesh = new Mesh();
        mesh.name = "Webcam Particle Grid";
        if (vertices.Length > 65535)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
    }

    private void UpdateFromWebcam()
    {
        int webcamWidth = webcam.width;
        int webcamHeight = webcam.height;
        if (webcamWidth <= 16 || webcamHeight <= 16)
        {
            return;
        }

        int pixelCount = webcamWidth * webcamHeight;
        if (webcamPixels == null || webcamPixels.Length != pixelCount)
        {
            webcamPixels = new Color32[pixelCount];
        }

        webcam.GetPixels32(webcamPixels);

        Color32[] liveDownsampled = new Color32[columns * rows];
        int sampleIndex = 0;
        float sumX = 0f;
        float sumY = 0f;
        float sumMotion = 0f;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                float u = columns <= 1 ? 0f : x / (float)(columns - 1);
                float v = rows <= 1 ? 0f : y / (float)(rows - 1);
                float sampleU = runtimeMirrorX ? 1f - u : u;
                int sx = Mathf.Clamp(Mathf.RoundToInt(sampleU * (webcamWidth - 1)), 0, webcamWidth - 1);
                int sy = Mathf.Clamp(Mathf.RoundToInt(v * (webcamHeight - 1)), 0, webcamHeight - 1);
                Color32 pixel = webcamPixels[sy * webcamWidth + sx];

                float brightness = Mathf.Clamp01(((pixel.r + pixel.g + pixel.b) / 765f) * brightnessBoost);
                float previous = previousLuma[sampleIndex];
                float motion = Mathf.Clamp01(Mathf.Abs(brightness - previous) * motionInfluence);
                float motionMask = Mathf.Clamp01((motion - motionThreshold) / Mathf.Max(0.0001f, 1f - motionThreshold));

                if (motionMask > 0f)
                {
                    sumX += u * motionMask;
                    sumY += v * motionMask;
                    sumMotion += motionMask;
                }

                float silhouette = Mathf.Clamp01((brightness - silhouetteThreshold) * silhouetteMultiplier);
                float cellAlpha = Mathf.Clamp01(Mathf.Max(silhouette, motionMask));
                byte alphaByte = (byte)(cellAlpha * 255f);

                liveDownsampled[sampleIndex] = new Color32(pixel.r, pixel.g, pixel.b, alphaByte);
                sampleIndex++;
            }
        }

        HistoryFrame newFrame;
        newFrame.pixels = liveDownsampled;
        newFrame.time = Time.time;
        historyBuffer.Add(newFrame);

        if (historyBuffer.Count > maxHistoryFrames)
        {
            historyBuffer.RemoveAt(0);
        }

        if (sumMotion > 0.01f)
        {
            Vector2 currentCentroid = new Vector2(sumX / sumMotion, sumY / sumMotion);
            if (Time.deltaTime > 0f)
            {
                motionVelocity = (currentCentroid - lastCentroid) / Time.deltaTime;
            }
            lastCentroid = currentCentroid;
            motionCentroid = currentCentroid;
            totalMotionAmount = sumMotion / (columns * rows);
        }
        else
        {
            totalMotionAmount = 0f;
            motionVelocity = Vector2.Lerp(motionVelocity, Vector2.zero, Time.deltaTime * 5f);
        }

        int vertexIndex = 0;
        for (int c = cloneCount - 1; c >= 0; c--)
        {
            float offsetX = 0f;
            float offsetY = 0f;
            if (c > 0)
            {
                float seed = c * 15.3f;
                offsetX = (Mathf.PerlinNoise(Time.time * driftSpeed, seed) - 0.5f) * driftRadius;
                offsetY = (Mathf.PerlinNoise(seed, Time.time * driftSpeed) - 0.5f) * driftRadius;
            }

            Color32[] historyPixels = null;
            if (c > 0)
            {
                historyPixels = GetHistoryFrame(Time.time - c * delaySeconds);
            }

            sampleIndex = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float u = columns <= 1 ? 0f : x / (float)(columns - 1);
                    float v = rows <= 1 ? 0f : y / (float)(rows - 1);

                    Color32 pixel;
                    float finalAlpha;
                    float depth;
                    float size;
                    Color finalColor;

                    if (c == 0)
                    {
                        pixel = liveDownsampled[sampleIndex];
                        float brightness = Mathf.Clamp01(((pixel.r + pixel.g + pixel.b) / 765f) * brightnessBoost);
                        float previous = previousLuma[sampleIndex];
                        float motion = Mathf.Clamp01(Mathf.Abs(brightness - previous) * motionInfluence);
                        float motionMask = Mathf.Clamp01((motion - motionThreshold) / Mathf.Max(0.0001f, 1f - motionThreshold));
                        float silhouette = Mathf.Clamp01((brightness - silhouetteThreshold) * silhouetteMultiplier);
                        float cellAlpha = Mathf.Clamp01(Mathf.Max(silhouette, motionMask));
                        finalAlpha = cellAlpha;

                        depth = brightness * displacement + motionMask * displacement * 0.85f;
                        size = particleSize * Mathf.Lerp(1f, motionSizeBoost, motionMask);

                        Color32 boosted = Boost(pixel, brightness, motionMask);
                        finalColor = new Color(boosted.r / 255f, boosted.g / 255f, boosted.b / 255f, finalAlpha);

                        previousLuma[sampleIndex] = brightness;
                    }
                    else
                    {
                        if (historyPixels != null)
                        {
                            pixel = historyPixels[sampleIndex];
                        }
                        else
                        {
                            pixel = liveDownsampled[sampleIndex];
                        }

                        float histBrightness = Mathf.Clamp01(((pixel.r + pixel.g + pixel.b) / 765f) * brightnessBoost);
                        float histAlpha = pixel.a / 255f;

                        if (historyPixels == null)
                        {
                            histAlpha = 0f;
                        }

                        float ageFactor = Mathf.Clamp01(0.9f - 0.25f * (c - 1));
                        finalAlpha = histAlpha * ageFactor;

                        depth = histBrightness * displacement;
                        size = particleSize * Mathf.Lerp(1.2f, motionSizeBoost * 1.2f, histAlpha);

                        Color32 boosted = Boost(pixel, histBrightness, 0f);
                        Color cosmicColor = (c % 2 == 1) ? new Color(0f, 0.8f, 1f, 1f) : new Color(0.6f, 0.1f, 0.9f, 1f);
                        float lerpFactor = Mathf.Clamp01(0.7f + 0.15f * (c - 1));
                        Color blended = Color.Lerp(boosted, cosmicColor, lerpFactor);
                        finalColor = new Color(blended.r, blended.g, blended.b, finalAlpha);
                    }

                    WriteQuad(vertexIndex, u, v, finalColor, depth, size, offsetX, offsetY);
                    vertexIndex += 4;
                    sampleIndex++;
                }
            }
        }

        ApplyMeshChanges();
    }

    private void UpdateFallback()
    {
        if (!warnedNoCamera)
        {
            warnedNoCamera = true;
            Debug.LogWarning("No webcam feed available. Showing animated fallback particle field.", this);
        }

        float time = Time.time;
        int vertexIndex = 0;
        for (int c = cloneCount - 1; c >= 0; c--)
        {
            int sampleIndex = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float u = columns <= 1 ? 0f : x / (float)(columns - 1);
                    float v = rows <= 1 ? 0f : y / (float)(rows - 1);

                    if (c == 0)
                    {
                        float wave = Mathf.Sin((u * 9f) + time * 1.7f) * Mathf.Cos((v * 7f) - time * 1.2f);
                        float brightness = Mathf.Clamp01(0.35f + wave * 0.35f + Mathf.Sin(time + u * 3f) * 0.15f);
                        float motionMask = Mathf.Clamp01((Mathf.Sin(time * 2.1f + u * 11f + v * 5f) + 1f) * 0.5f);
                        Color color = Color.Lerp(new Color(0.05f, 0.18f, 0.28f, 1f), new Color(0.95f, 0.82f, 0.42f, 1f), brightness);
                        previousLuma[sampleIndex] = brightness;
                        WriteQuad(vertexIndex, u, v, Color.Lerp(color, Color.white, motionMask * 0.35f), brightness * displacement, particleSize * Mathf.Lerp(1f, 1.4f, motionMask), 0f, 0f);
                        sampleIndex++;
                    }
                    else
                    {
                        WriteQuad(vertexIndex, u, v, new Color(0f, 0f, 0f, 0f), 0f, 0f, 0f, 0f);
                    }
                    vertexIndex += 4;
                }
            }
        }
        totalMotionAmount = 0f;
        motionVelocity = Vector2.zero;

        ApplyMeshChanges();
    }

    private Color32 Boost(Color32 pixel, float brightness, float motionMask)
    {
        float glow = Mathf.Lerp(0.75f, 1.35f, brightness) + motionMask * 0.65f;
        byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.r * glow + motionMask * 48f), 0, 255);
        byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.g * glow + motionMask * 30f), 0, 255);
        byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.b * glow + motionMask * 14f), 0, 255);
        return new Color32(r, g, b, 255);
    }

    private void WriteQuad(int index, float u, float v, Color color, float z, float quadSize, float offsetX, float offsetY)
    {
        float cx = Mathf.Lerp(-displayWidth * 0.5f, displayWidth * 0.5f, u) + offsetX;
        float cy = Mathf.Lerp(-displayHeight * 0.5f, displayHeight * 0.5f, v) + offsetY;
        float half = quadSize * 0.5f;

        vertices[index] = new Vector3(cx - half, cy - half, z);
        vertices[index + 1] = new Vector3(cx + half, cy - half, z);
        vertices[index + 2] = new Vector3(cx + half, cy + half, z);
        vertices[index + 3] = new Vector3(cx - half, cy + half, z);

        Color32 color32 = color;
        colors[index] = color32;
        colors[index + 1] = color32;
        colors[index + 2] = color32;
        colors[index + 3] = color32;

        uvs[index] = new Vector2(0f, 0f);
        uvs[index + 1] = new Vector2(1f, 0f);
        uvs[index + 2] = new Vector2(1f, 1f);
        uvs[index + 3] = new Vector2(0f, 1f);
    }

    private Color32[] GetHistoryFrame(float targetTime)
    {
        if (historyBuffer.Count == 0)
        {
            return null;
        }
        int bestIndex = 0;
        float minDiff = Mathf.Abs(historyBuffer[0].time - targetTime);
        for (int i = 1; i < historyBuffer.Count; i++)
        {
            float diff = Mathf.Abs(historyBuffer[i].time - targetTime);
            if (diff < minDiff)
            {
                minDiff = diff;
                bestIndex = i;
            }
        }
        return historyBuffer[bestIndex].pixels;
    }

    private void ApplyMeshChanges()
    {
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.RecalculateBounds();
    }

    public bool TryGetRandomSilhouetteViewportPoint(out Vector2 uv)
    {
        uv = new Vector2(0.5f, 0.5f);
        if (historyBuffer == null || historyBuffer.Count == 0)
        {
            return false;
        }
        var latestFrame = historyBuffer[historyBuffer.Count - 1].pixels;
        if (latestFrame == null || latestFrame.Length == 0)
        {
            return false;
        }
        var candidates = new System.Collections.Generic.List<int>();
        for (int i = 0; i < latestFrame.Length; i++)
        {
            if (latestFrame[i].a > 50)
            {
                candidates.Add(i);
            }
        }
        if (candidates.Count == 0)
        {
            return false;
        }
        int randomIndex = candidates[Random.Range(0, candidates.Count)];
        int rx = randomIndex % columns;
        int ry = randomIndex / columns;
        float u = columns <= 1 ? 0.5f : rx / (float)(columns - 1);
        float v = rows <= 1 ? 0.5f : ry / (float)(rows - 1);
        uv = new Vector2(u, v);
        return true;
    }
}

