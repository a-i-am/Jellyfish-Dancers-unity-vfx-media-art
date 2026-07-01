using System;
using UnityEngine;
using UnityEngine.VFX;

namespace Project.VFX.TVUniverse
{
    public sealed class CrowdUniverseInteractionBinder : MonoBehaviour
    {
        public static CrowdUniverseInteractionBinder Instance { get; private set; }

        public float MotionScore => webcamRenderer != null ? webcamRenderer.TotalMotionAmount : 0f;
        public bool IsLiveWebcamInteraction => TryGetWebcamReactiveUv(out _);
        public Vector2 MotionVelocity => webcamRenderer != null ? webcamRenderer.MotionVelocity : Vector2.zero;
        public Vector3 AttractorWorldPos
        {
            get
            {
                if (selectedCount > 0 && selectedSources[0] != null) return selectedSources[0].FocusPoint;
                return GetWebcamFocusPoint();
            }
        }

        public Vector3 WebcamFocusPoint
        {
            get
            {
                return GetWebcamFocusPoint();
            }
        }

        private const int MaxCrowdAttractors = 4;
        private const int MaxStreamLines = 4;
        private const int MaxConstellationLines = 2;

    [Header("References")]
    [SerializeField] private VisualEffect targetVfx;
    [SerializeField] private WebcamParticleMeshRenderer webcamRenderer;
    [SerializeField] private Transform lineRoot;

    [Header("Interaction Options")]
    [SerializeField] private bool crowdGravityEnabled = true;
    [SerializeField] private bool particleStreamsEnabled = true;
    [SerializeField] private bool constellationLinesEnabled = true;
    [SerializeField] private bool crowdPulseEnabled = true;
    [SerializeField] private bool crowdHaloEnabled = false;

    [Header("Tuning")]
    [SerializeField] private float crowdStrength = 3.5f;
    [SerializeField] private float streamAlpha = 0.32f;
    [SerializeField] private float lineAlpha = 0.24f;
    [SerializeField] private float pulseStrength = 1.0f;
    [SerializeField] private float haloAlpha = 0.18f;
    [SerializeField] private float pulseSmoothSpeed = 3.0f;
    [SerializeField] private float motionThreshold = 0.004f;

    [Header("Stage Mapping")]
    [SerializeField] private Vector2 attractorExtent = new Vector2(6f, 3.4f);
    [SerializeField] private Vector2 crowdXBounds = new Vector2(-4.5f, 4.5f);
    [SerializeField] private Vector2 crowdZBounds = new Vector2(-3.5f, 1.0f);

    private readonly CrowdGravitySource[] selectedSources = new CrowdGravitySource[MaxCrowdAttractors];
    private readonly float[] selectedWeights = new float[MaxCrowdAttractors];
    private readonly CrowdGravitySource[] sourceBuffer = new CrowdGravitySource[64];
    private LineRenderer[] streamLines;
    private LineRenderer[] constellationLines;
    private LineRenderer[] haloLines;
    private Material lineMaterial;
    private float lastSourceRefreshTime;
    private int sourceCount;
    private int selectedCount;
        private Transform tvFrameTransform;
        private bool useLocalSpace = true;
        private int webcamSampleFrame = -1;
        private bool hasWebcamReactivePoint;
        private Vector2 webcamReactiveUv;

    public bool CrowdGravityEnabled
    {
        get { return crowdGravityEnabled; }
        set { crowdGravityEnabled = value; }
    }

    public bool ParticleStreamsEnabled
    {
        get { return particleStreamsEnabled; }
        set { particleStreamsEnabled = value; }
    }

    public bool ConstellationLinesEnabled
    {
        get { return constellationLinesEnabled; }
        set { constellationLinesEnabled = value; }
    }

    public bool CrowdPulseEnabled
    {
        get { return crowdPulseEnabled; }
        set { crowdPulseEnabled = value; }
    }

    public bool CrowdHaloEnabled
    {
        get { return crowdHaloEnabled; }
        set { crowdHaloEnabled = value; }
    }

    private void Awake()
    {
        Instance = this;
        EnsureReferences();
        EnsureVisualObjects();
    }

    private void Update()
    {
        EnsureReferences();

        if (Time.time - lastSourceRefreshTime > 0.5f)
        {
            RefreshSources();
        }

        UpdateSelectedSources();
        UpdateCrowdPulses();
        UpdateVisualLines();
        ApplyCrowdPropertiesToVfx();
    }

#if UNITY_EDITOR
    public void DrawTuningGui()
    {
        GUILayout.Space(6f);
        GUILayout.Label("Crowd Options");

        crowdGravityEnabled = GUILayout.Toggle(crowdGravityEnabled, "Crowd Gravity");
        particleStreamsEnabled = GUILayout.Toggle(particleStreamsEnabled, "Particle Streams");
        constellationLinesEnabled = GUILayout.Toggle(constellationLinesEnabled, "Constellation Lines");
        crowdPulseEnabled = GUILayout.Toggle(crowdPulseEnabled, "Crowd Pulse");
        crowdHaloEnabled = GUILayout.Toggle(crowdHaloEnabled, "Crowd Halo");

        DrawSlider("Crowd Strength", ref crowdStrength, 0f, 10f);
        DrawSlider("Stream Alpha", ref streamAlpha, 0f, 1f);
        DrawSlider("Line Alpha", ref lineAlpha, 0f, 1f);
        DrawSlider("Pulse Strength", ref pulseStrength, 0f, 3f);
        DrawSlider("Halo Alpha", ref haloAlpha, 0f, 1f);

        GUILayout.Label("Active Crowd " + selectedCount);
    }
#endif

    public void ConfigureVfx(VisualEffect visualEffect, Transform tvFrame, bool localSpace)
    {
        targetVfx = visualEffect;
        tvFrameTransform = tvFrame;
        useLocalSpace = localSpace;
    }

    public void AdjustAttractor(ref Vector3 attractorPosition, ref float attractorStrength)
    {
        if (!crowdGravityEnabled || selectedCount == 0)
        {
            return;
        }

        Vector3 crowdAttractor = Vector3.zero;
        float totalWeight = 0f;
        for (int i = 0; i < selectedCount; i++)
        {
            CrowdGravitySource source = selectedSources[i];
            if (source == null)
            {
                continue;
            }

            float weight = Mathf.Max(0.001f, selectedWeights[i]) * source.EffectiveStrength;
            crowdAttractor += MapWorldToAttractor(source.FocusPoint) * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0.0001f)
        {
            return;
        }

        crowdAttractor /= totalWeight;
        float blend = attractorStrength > 0.05f ? 0.35f : 1f;
        attractorPosition = Vector3.Lerp(attractorPosition, crowdAttractor, blend);
        attractorStrength += crowdStrength * Mathf.Clamp01(totalWeight / selectedCount);
    }

#if UNITY_EDITOR
    private void DrawSlider(string label, ref float value, float min, float max)
    {
        GUILayout.Label(label + " " + value.ToString("0.00"));
        value = GUILayout.HorizontalSlider(value, min, max);
    }
#endif

    private void EnsureReferences()
    {
        if (targetVfx == null)
        {
            targetVfx = GetComponent<VisualEffect>();
        }

        if (webcamRenderer == null)
        {
            webcamRenderer = FindFirstObjectByType<WebcamParticleMeshRenderer>();
        }

        if (lineRoot == null)
        {
            lineRoot = transform;
        }
    }

    private void RefreshSources()
    {
        Array.Clear(sourceBuffer, 0, sourceBuffer.Length);
        CrowdGravitySource[] found = FindObjectsByType<CrowdGravitySource>(FindObjectsSortMode.None);
        sourceCount = Mathf.Min(found.Length, sourceBuffer.Length);
        for (int i = 0; i < sourceCount; i++)
        {
            sourceBuffer[i] = found[i];
        }

        lastSourceRefreshTime = Time.time;
    }

    private void UpdateSelectedSources()
    {
        if (!TryGetWebcamFocusPoint(out Vector3 focusWorld))
        {
            Array.Clear(selectedSources, 0, selectedSources.Length);
            Array.Clear(selectedWeights, 0, selectedWeights.Length);
            selectedCount = 0;
            return;
        }

        Vector3 focusLocal = tvFrameTransform != null ? tvFrameTransform.InverseTransformPoint(focusWorld) : focusWorld;

        Array.Clear(selectedSources, 0, selectedSources.Length);
        Array.Clear(selectedWeights, 0, selectedWeights.Length);
        selectedCount = 0;

        for (int i = 0; i < sourceCount; i++)
        {
            CrowdGravitySource source = sourceBuffer[i];
            if (source == null || !source.IsActive)
            {
                continue;
            }

            Vector3 sourceLocal = MapWorldToAttractor(source.FocusPoint);
            float distance = Vector2.Distance(new Vector2(focusLocal.x, focusLocal.y), new Vector2(sourceLocal.x, sourceLocal.y));
            float score = 1f / Mathf.Max(0.1f, distance);
            InsertSource(source, score);
        }
    }

    private void InsertSource(CrowdGravitySource source, float score)
    {
        for (int i = 0; i < MaxCrowdAttractors; i++)
        {
            if (selectedSources[i] != null && selectedWeights[i] >= score)
            {
                continue;
            }

            for (int j = MaxCrowdAttractors - 1; j > i; j--)
            {
                selectedSources[j] = selectedSources[j - 1];
                selectedWeights[j] = selectedWeights[j - 1];
            }

            selectedSources[i] = source;
            selectedWeights[i] = score;
            selectedCount = Mathf.Min(MaxCrowdAttractors, selectedCount + 1);
            return;
        }
    }

    private void UpdateCrowdPulses()
    {
        float motion = MotionScore;
        Vector3 focus = GetWebcamFocusPoint();

        for (int i = 0; i < sourceCount; i++)
        {
            CrowdGravitySource source = sourceBuffer[i];
            if (source == null)
            {
                continue;
            }

            float distance = Vector3.Distance(focus, source.FocusPoint);
            float distancePulse = Mathf.Clamp01(1f - distance / Mathf.Max(0.1f, source.Radius * 2.5f));
            float targetPulse = Mathf.Clamp01((motion / Mathf.Max(0.0001f, motionThreshold * 8f)) * distancePulse * pulseStrength);
            source.SetPulse(targetPulse, pulseSmoothSpeed, crowdPulseEnabled);
        }
    }

    private void UpdateVisualLines()
    {
        EnsureVisualObjects();

        bool hasFocus = TryGetWebcamFocusPoint(out Vector3 focus);
        float motionWeight = Mathf.Clamp01(MotionScore / Mathf.Max(0.0001f, motionThreshold * 8f));

        for (int i = 0; i < streamLines.Length; i++)
        {
            bool visible = hasFocus && particleStreamsEnabled && i < selectedCount && selectedSources[i] != null;
            DrawLine(streamLines[i], visible, focus, visible ? selectedSources[i].FocusPoint : focus, streamAlpha * Mathf.Max(0.25f, motionWeight), new Color(0.25f, 0.95f, 1f, 1f));
        }

        for (int i = 0; i < constellationLines.Length; i++)
        {
            bool visible = hasFocus && constellationLinesEnabled && selectedCount > i + 1 && selectedSources[i] != null && selectedSources[i + 1] != null;
            Vector3 start = visible ? selectedSources[i].FocusPoint : focus;
            Vector3 end = visible ? selectedSources[i + 1].FocusPoint : focus;
            DrawLine(constellationLines[i], visible, start, end, lineAlpha * Mathf.Max(0.35f, motionWeight), new Color(0.55f, 0.35f, 1f, 1f));
        }

        for (int i = 0; i < haloLines.Length; i++)
        {
            bool visible = crowdHaloEnabled && i < selectedCount && selectedSources[i] != null;
            DrawHalo(haloLines[i], visible, visible ? selectedSources[i] : null, haloAlpha * Mathf.Max(0.25f, motionWeight));
        }
    }

    private void ApplyCrowdPropertiesToVfx()
    {
        if (targetVfx == null)
        {
            return;
        }

        SetVfxBool(TVUniverseVfxContract.CrowdGravityEnabled, crowdGravityEnabled);
        SetVfxFloat(TVUniverseVfxContract.CrowdAttractorRadius, 2.0f);

        for (int i = 0; i < MaxCrowdAttractors; i++)
        {
            CrowdGravitySource source = i < selectedCount ? selectedSources[i] : null;
            Vector3 position = source != null ? MapWorldToAttractor(source.FocusPoint) : Vector3.zero;
            float strength = source != null && crowdGravityEnabled ? crowdStrength * source.EffectiveStrength : 0f;

            if (!useLocalSpace && source != null)
            {
                Transform origin = tvFrameTransform != null ? tvFrameTransform : targetVfx.transform;
                position = origin.TransformPoint(position);
            }

            SetVfxVector3(TVUniverseVfxContract.CrowdAttractorPosition(i), position);
            SetVfxFloat(TVUniverseVfxContract.CrowdAttractorStrength(i), strength);
        }
    }

        private bool TryGetWebcamReactiveUv(out Vector2 uv)
        {
            if (webcamSampleFrame != Time.frameCount)
            {
                webcamSampleFrame = Time.frameCount;
                hasWebcamReactivePoint = webcamRenderer != null &&
                    webcamRenderer.IsUsingWebcam &&
                    webcamRenderer.TryGetRandomSilhouetteViewportPoint(out webcamReactiveUv);
            }

            uv = webcamReactiveUv;
            return hasWebcamReactivePoint;
        }

        private Vector3 GetWebcamFocusPoint()
        {
            if (TryGetWebcamFocusPoint(out Vector3 focus))
            {
                return focus;
            }

            return transform.position + Vector3.up;
        }

        private bool TryGetWebcamFocusPoint(out Vector3 focus)
        {
            if (!TryGetWebcamReactiveUv(out Vector2 uv))
            {
                focus = transform.position + Vector3.up;
                return false;
            }

            if (tvFrameTransform != null)
            {
                Vector3 local = new Vector3(
                    Mathf.Lerp(-attractorExtent.x, attractorExtent.x, uv.x),
                    Mathf.Lerp(-attractorExtent.y, attractorExtent.y, uv.y),
                    0f
                );
                focus = tvFrameTransform.TransformPoint(local);
                return true;
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                float depth = Mathf.Abs(camera.transform.position.z - transform.position.z);
                focus = camera.ViewportToWorldPoint(new Vector3(uv.x, uv.y, depth));
                return true;
            }

            focus = transform.position + Vector3.up;
            return false;
        }

    private Vector3 MapWorldToAttractor(Vector3 worldPoint)
    {
        float x = Mathf.InverseLerp(crowdXBounds.x, crowdXBounds.y, worldPoint.x);
        float y = Mathf.InverseLerp(crowdZBounds.x, crowdZBounds.y, worldPoint.z);
        return new Vector3(
            Mathf.Lerp(-attractorExtent.x, attractorExtent.x, x),
            Mathf.Lerp(-attractorExtent.y, attractorExtent.y, y),
            0f
        );
    }

    private void EnsureVisualObjects()
    {
        if (streamLines != null && constellationLines != null && haloLines != null)
        {
            return;
        }

        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            lineMaterial = new Material(shader);
            lineMaterial.name = "Runtime_CrowdUniverse_Line";
        }

        streamLines = CreateLineSet("ParticleStream", MaxStreamLines, 0.025f);
        constellationLines = CreateLineSet("ConstellationLine", MaxConstellationLines, 0.014f);
        haloLines = CreateLineSet("CrowdHalo", MaxCrowdAttractors, 0.018f);
    }

    private LineRenderer[] CreateLineSet(string label, int count, float width)
    {
        LineRenderer[] lines = new LineRenderer[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject(label + "_" + i);
            go.transform.SetParent(lineRoot, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.textureMode = LineTextureMode.Stretch;
            line.enabled = false;
            lines[i] = line;
        }

        return lines;
    }

    private void DrawLine(LineRenderer line, bool visible, Vector3 start, Vector3 end, float alpha, Color color)
    {
        if (line == null)
        {
            return;
        }

        line.enabled = visible && alpha > 0.001f;
        if (!line.enabled)
        {
            line.positionCount = 0;
            return;
        }

        Vector3 mid = Vector3.Lerp(start, end, 0.5f) + Vector3.up * 0.22f;
        float wobble = Mathf.Sin(Time.time * 2.3f + start.x + end.z) * 0.12f;
        mid += Vector3.right * wobble;

        line.positionCount = 3;
        line.SetPosition(0, start);
        line.SetPosition(1, mid);
        line.SetPosition(2, end);

        color.a = Mathf.Clamp01(alpha);
        line.startColor = color;
        line.endColor = color;
        line.material.color = color;
    }

    private void DrawHalo(LineRenderer line, bool visible, CrowdGravitySource source, float alpha)
    {
        if (line == null)
        {
            return;
        }

        line.enabled = visible && source != null && alpha > 0.001f;
        if (!line.enabled)
        {
            line.positionCount = 0;
            return;
        }

        const int segments = 36;
        Vector3 center = source.FocusPoint;
        float radius = Mathf.Lerp(0.28f, 0.48f, source.Pulse);
        line.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0.04f, Mathf.Sin(angle) * radius);
            line.SetPosition(i, point);
        }

        Color color = new Color(0.35f, 0.95f, 1f, Mathf.Clamp01(alpha * (0.35f + source.Pulse)));
        line.startColor = color;
        line.endColor = color;
        line.material.color = color;
    }

    private void SetVfxVector3(string property, Vector3 value)
    {
        if (targetVfx.HasVector3(property))
        {
            targetVfx.SetVector3(property, value);
        }
    }

    private void SetVfxFloat(string property, float value)
    {
        if (targetVfx.HasFloat(property))
        {
            targetVfx.SetFloat(property, value);
        }
    }

    private void SetVfxBool(string property, bool value)
    {
        if (targetVfx.HasBool(property))
        {
            targetVfx.SetBool(property, value);
        }
    }
    }
}
