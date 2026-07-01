using UnityEngine;
using Project.VFX.TVUniverse;

public sealed class JellyfishAgent : MonoBehaviour
{
    private enum JellyfishInteractionState
    {
        Idle,
        Repelled,
        Accepted,
        Consumed
    }

    private const int BellRadialSegments = 32;
    private const int BellProfileSegments = 14;
    private const int MarginalTentacleCount = 24;
    private const int MaxOralArmCount = 16;
    private const int MarginalSegments = 16;
    private const int OralArmSegments = 18;
    private const int SplineSamplesPerSegment = 4;
    private const int MarginalRenderPositions = (MarginalSegments - 1) * SplineSamplesPerSegment + 1;
    private const int OralArmRenderPositions = (OralArmSegments - 1) * SplineSamplesPerSegment + 1;

    [Header("Marginal Tentacle Verlet")]
    [SerializeField] private float marginalStiffness = 0.78f;
    [SerializeField] private float marginalDamping = 0.965f;
    [SerializeField] private float marginalGravity = -0.08f;

    [Header("Oral Arm Flow")]
    [SerializeField] private float oralArmNoiseAmplitude = 0.42f;
    [SerializeField] private float oralArmNoiseSpeed = 0.18f;

    [Header("3D Roaming")]
    [SerializeField] private float powerStrokeForce = 42f;
    [SerializeField] private float contractionDrag = 0.16f;
    [SerializeField] private float relaxationDrag = 1.35f;
    [SerializeField] private float oceanCurrentStrength = 0.32f;
    [SerializeField] private float driftFrequency = 0.09f;
    [SerializeField] private float softBoundaryRadius = 3.35f;
    [SerializeField] private float boundarySteeringStrength = 5.25f;
    [SerializeField] private float maxRoamSpeed = 1.65f;
    [SerializeField] private float turnSpeed = 2.3f;
    [SerializeField] private float uprightReturnSpeed = 0.65f;

    [Header("Interaction")]
    [SerializeField] private float repulseRadius = 2.5f;
    [SerializeField] private float repulseForceStrength = 25f;
    [SerializeField] private float attractorSteerStrength = 0.85f;
    [SerializeField] private float strikeMotionThreshold = 0.012f;
    [SerializeField] private float strikeVelocityThreshold = 1.15f;
    [SerializeField] private float acceptHandRadius = 1.25f;
    [SerializeField] private float acceptCenterRadius = 1.15f;
    [SerializeField] private float acceptMotionMax = 0.018f;
    [SerializeField] private float acceptDwellSeconds = 0.85f;

    [Header("Emotion Label")]
    [SerializeField] private bool showEmotionLabel = true;
    [SerializeField] private float labelHeight = 0.28f;
    [SerializeField] private float labelCharacterSize = 0.08f;
    [SerializeField] private Color labelColor = new Color(0.86f, 1f, 1f, 0.9f);

    public static System.Collections.Generic.List<JellyfishAgent> ActiveAgents = new System.Collections.Generic.List<JellyfishAgent>();
    public static float GlobalSpeedMultiplier = 1f;

    private Transform bellTransform;
    private MeshRenderer bellRenderer;
    private TextMesh labelMesh;
    private LineRenderer[] oralArms;
    private LineRenderer[] marginalTentacles;
    private Vector3[][] oralArmPositions;
    private Vector3[][] oralArmPreviousPositions;
    private Vector3[][] marginalPositions;
    private Vector3[][] marginalPreviousPositions;
    private Vector3[] marginalRimRoots;
    private Vector3[] oralArmRoots;
    private Vector3 forwardDirection;
    private Vector3 currentVelocity;
    private Vector3 lastPosition;
    private Vector2 xBounds;
    private Vector2 yBounds;
    private Vector2 zBounds;
    private float swimSpeed;
    private float pulseSpeed;
    private float pulseAmount;
    private float driftPhase;
    private float tentacleLength;
    private float wobbleAmount;
    private float size;
    private float currentPulsePhase;
    private float contractionDuration;
    private float agentIndex;
    private Vector3 stageCenter;
    private ThoughtEssence thoughtEssence;
    private JellyfishEmotionResultLog resultLog;
    private JellyfishInteractionState interactionState;
    private float acceptDwellTimer;
    private float terminalTimer;
    private Vector3 terminalVelocity;
    private Color assignedColor;
    private bool isRemoteMode;
    private float age;
    private float lifespan;
    private const float DespawnDuration = 3.5f;
    private float despawnFloatSpeed = 0.5f;
    private float pulseSpeedMultiplier = 1.0f;
    private MaterialPropertyBlock propertyBlock;
    private UnityEngine.Pool.IObjectPool<JellyfishAgent> pool;
    private bool isSubliming = false;
    private float patternType = 0f;
    private Vector2 patternSpeed = Vector2.zero;
    private bool isImmortal = false;

    public float BellHeightMultiplier = 1.0f;
    public float BellWidthMultiplier = 1.0f;
    public float TentacleLengthMultiplier = 1.0f;
    private int currentOralArmCount = 4;

    [Header("Tentacle Limit")]
    private float multiplicationCoefficient = 1.08f;
    private float tensionCoefficient = 1.25f;

    public void UpdateProportions(float bellHeightMul, float bellWidthMul, float tentacleLenMul, int thickTentacleCount = 4)
    {
        BellHeightMultiplier = bellHeightMul;
        BellWidthMultiplier = bellWidthMul;
        TentacleLengthMultiplier = tentacleLenMul;
        currentOralArmCount = Mathf.Clamp(thickTentacleCount, 0, MaxOralArmCount);
        if (bellTransform != null)
        {
            bellTransform.localScale = new Vector3(size * 1.08f * BellWidthMultiplier, size * 0.72f * BellHeightMultiplier, size * 1.08f * BellWidthMultiplier);
        }
    }

    public void ConfigureRemoteMode(bool remoteMode, float customLifespan, bool immortal = false)
    {
        isRemoteMode = remoteMode;
        lifespan = customLifespan;
        age = 0f;
        isImmortal = immortal;
        isSubliming = false;
    }

    public void SetPool(UnityEngine.Pool.IObjectPool<JellyfishAgent> pool)
    {
        this.pool = pool;
    }

    public void SetPattern(float type, Vector2 speed)
    {
        patternType = type;
        patternSpeed = speed;
    }

    public void StartSublimation()
    {
        if (isSubliming) return;
        isSubliming = true;
        StartCoroutine(SublimeCoroutine());
    }

    private System.Collections.IEnumerator SublimeCoroutine()
    {
        float timer = 0f;
        float duration = 3.5f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            transform.position += Vector3.up * (0.5f * Time.deltaTime);
            SetRenderAlpha(1f - progress);
            yield return null;
        }
        isSubliming = false;
        gameObject.SetActive(false);
        if (pool != null)
        {
            pool.Release(this);
        }
    }

    private void OnEnable()
    {
        ActiveAgents.Add(this);
    }

    private void OnDisable()
    {
        ActiveAgents.Remove(this);
    }

    public void Initialize(
        Material bellMaterial,
        Material tentacleMaterial,
        Color color,
        Vector3 initialVelocity,
        Vector2 xLimits,
        Vector2 yLimits,
        Vector2 zLimits,
        float speed,
        float bellSize,
        float pulseRate,
        float pulseDepth,
        float tentacleScale,
        float wobble)
    {
        forwardDirection = initialVelocity.sqrMagnitude > 0.0001f ? initialVelocity.normalized : Vector3.up;
        currentVelocity = forwardDirection * speed * 0.16f;
        xBounds = xLimits;
        yBounds = yLimits;
        zBounds = zLimits;
        swimSpeed = speed;
        size = bellSize;
        pulseSpeed = pulseRate;
        pulseAmount = pulseDepth;
        driftPhase = Random.value * Mathf.PI * 2f;
        currentPulsePhase = Random.value;
        agentIndex = Random.Range(0f, 1000f);
        contractionDuration = Random.Range(0.15f, 0.20f);
        tentacleLength = tentacleScale;
        wobbleAmount = wobble;
        lastPosition = transform.position;
        stageCenter = new Vector3(
            (xBounds.x + xBounds.y) * 0.5f,
            (yBounds.x + yBounds.y) * 0.5f,
            (zBounds.x + zBounds.y) * 0.5f
        );
        transform.rotation = Quaternion.LookRotation(Vector3.forward, forwardDirection);

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
        BuildBell(bellMaterial, color);
        BuildOralArms(tentacleMaterial, color);
        BuildMarginalTentacles(tentacleMaterial, color);
    }

    public void AssignThoughtEssence(ThoughtEssence essence, JellyfishEmotionResultLog log)
    {
        thoughtEssence = essence;
        resultLog = log;
        assignedColor = essence != null ? essence.Color : Color.white;

        if (thoughtEssence != null)
        {
            ApplyEmotionColor(assignedColor);
            EnsureEmotionLabel();
        }
    }

    private void Update()
    {
        if (oralArmPositions == null) return;

        if (isRemoteMode && !isImmortal)
        {
            if (isSubliming)
            {
                return;
            }
            age += Time.deltaTime;
            if (age >= lifespan || CheckCameraViewportExit())
            {
                StartSublimation();
                return;
            }
        }

        float time = Time.time + driftPhase;
        float motionScore = CrowdUniverseInteractionBinder.Instance != null ? CrowdUniverseInteractionBinder.Instance.MotionScore : 0f;
        float agitation = 1f + motionScore * 2f;
        EvaluateSwimCycle(agitation, out float contraction, out float propulsion);

        float drag = Mathf.Lerp(relaxationDrag, contractionDrag, propulsion);
        Vector3 propulsionForce = transform.up * propulsion * (powerStrokeForce * agitation) * Mathf.Lerp(0.85f, 1.35f, pulseAmount);

        bool scatterOutward = Project.VFX.MusicTrigger.Instance != null && !Project.VFX.MusicTrigger.IsPlaying;
        Vector3 boundaryForce = scatterOutward ? Vector3.zero : GetBoundarySteeringForce();

        bool hasAttractor = false;
        Vector3 attractorWorldPos = stageCenter;
        float currentAttractorStrength = attractorSteerStrength * 8f;
        Vector3 tvFlow = Vector3.zero;
        Vector3 tvBurstForce = Vector3.zero;

        var tvBinder = SensorToTVUniverseVfxBinder.Instance;
        if (tvBinder != null && tvBinder.isActiveAndEnabled)
        {
            if (tvBinder.FinalAttractorStrength > 0.01f)
            {
                hasAttractor = true;
                attractorWorldPos = tvBinder.FinalWorldAttractor;
                currentAttractorStrength = Mathf.Clamp(tvBinder.FinalAttractorStrength * 0.5f, -20f, 20f);
                if (Mathf.Abs(currentAttractorStrength) < 0.1f) hasAttractor = false;
            }
            tvFlow = tvBinder.CurrentFlow;
            if (tvBinder.CurrentBurst > 0.01f)
            {
                Vector3 away = (transform.position - attractorWorldPos).normalized;
                tvBurstForce = away * (tvBinder.CurrentBurst * 60f);
            }
            agitation += tvBinder.CurrentEnergy * 4f;
        }
        else if (InputDispatcher.Instance != null && InputDispatcher.Instance.IsPressed)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                float depth = Mathf.Abs(cam.transform.position.z - ((zBounds.x + zBounds.y) * 0.5f));
                Vector3 screenPos = new Vector3(InputDispatcher.Instance.Position.x, InputDispatcher.Instance.Position.y, depth);
                attractorWorldPos = cam.ScreenToWorldPoint(screenPos);
                hasAttractor = true;
            }
        }
        else if (CrowdUniverseInteractionBinder.Instance != null && CrowdUniverseInteractionBinder.Instance.IsLiveWebcamInteraction)
        {
            attractorWorldPos = CrowdUniverseInteractionBinder.Instance.WebcamFocusPoint;
            hasAttractor = true;
        }

        Vector3 interactionForce = Vector3.zero;
        Vector3 repulsionStream = Vector3.zero;

        if (scatterOutward)
        {
            Vector3 outward = transform.position - stageCenter;
            interactionForce = (outward.sqrMagnitude > 0.0001f ? outward.normalized : forwardDirection) * repulseForceStrength;
            hasAttractor = false;
            pulseSpeedMultiplier = 1.0f;
        }
        else if (hasAttractor)
        {
            Vector3 offset = transform.position - attractorWorldPos;
            float distToAttractor = offset.magnitude;

            Vector3 separationForce = Vector3.zero;
            int neighbors = 0;
            float separationRadius = 0.85f * size * 10f;
            foreach (var agent in ActiveAgents)
            {
                if (agent == this || agent == null) continue;
                Vector3 toAgent = transform.position - agent.transform.position;
                float dist = toAgent.magnitude;
                if (dist > 0.001f && dist < separationRadius)
                {
                    separationForce += toAgent.normalized * (1.0f - dist / separationRadius);
                    neighbors++;
                }
            }
            if (neighbors > 0)
            {
                separationForce = separationForce / neighbors * 8f;
            }

            if (distToAttractor > 0.0001f)
            {
                float orbitRadius = 0.8f;
                Vector3 dirToAttractor = -offset.normalized;


                Vector3 radialForce = dirToAttractor * Mathf.Clamp(distToAttractor, 0f, 2f);


                interactionForce = (radialForce * 1.5f + separationForce).normalized * currentAttractorStrength;
            }

            if (distToAttractor < 2.5f)
            {
                pulseSpeedMultiplier = 2.0f;
            }
            else
            {
                pulseSpeedMultiplier = 1.0f;
            }
        }
        else
        {
            pulseSpeedMultiplier = 1.0f;
        }

        Vector3 oceanCurrent = hasAttractor ? Vector3.zero : GetOceanCurrentForce(Time.time);

        float currentDrag = hasAttractor ? drag * 3.5f : drag;

        float speedMultiplier = Mathf.Max(0.05f, GlobalSpeedMultiplier);
        currentVelocity += (propulsionForce + oceanCurrent + boundaryForce + interactionForce + tvFlow + tvBurstForce) * Time.deltaTime * speedMultiplier;
        currentVelocity *= Mathf.Exp(-currentDrag * Time.deltaTime);
        currentVelocity = Vector3.ClampMagnitude(currentVelocity, maxRoamSpeed * agitation * (hasAttractor || scatterOutward ? 4f : 1f) * speedMultiplier);

        Vector3 motion = currentVelocity * Time.deltaTime;
        transform.position += motion;
        AlignToVelocity();

        UpdateBellMaterial(contraction);
        UpdateOralArms(time, contraction, repulsionStream);
        UpdateMarginalTentacles(time, contraction, repulsionStream);
        UpdateEmotionLabelFacingCamera();
        lastPosition = transform.position;
    }



    private Vector3 GetOceanCurrentForce(float time)
    {
        float sample = time * driftFrequency;
        Vector3 current = new Vector3(
            Mathf.PerlinNoise(sample, agentIndex * 0.013f) - 0.5f,
            Mathf.PerlinNoise(agentIndex * 0.017f, sample + 19.37f) - 0.5f,
            Mathf.PerlinNoise(sample + 41.91f, agentIndex * 0.021f) - 0.5f
        );
        return current * oceanCurrentStrength;
    }

    private Vector3 GetBoundarySteeringForce()
    {
        Vector3 offset = transform.position - stageCenter;
        float distance = offset.magnitude;
        if (distance <= softBoundaryRadius || distance <= 0.0001f)
        {
            return Vector3.zero;
        }

        float excess = distance - softBoundaryRadius;
        Vector3 centerDirection = -offset / distance;
        forwardDirection = Vector3.Slerp(forwardDirection, centerDirection, Time.deltaTime * 0.75f).normalized;
        return centerDirection * (excess * boundarySteeringStrength);
    }

    private void AlignToVelocity()
    {
        if (currentVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, currentVelocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
        }
        else
        {
            Quaternion upright = Quaternion.FromToRotation(transform.up, Vector3.up) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, upright, Time.deltaTime * uprightReturnSpeed);
        }
    }

    private void EvaluateSwimCycle(float agitation, out float contraction, out float propulsion)
    {
        currentPulsePhase += Time.deltaTime * pulseSpeed * agitation;
        float phase = Mathf.Repeat(currentPulsePhase, 1f);
        if (phase < contractionDuration)
        {
            float t = phase / contractionDuration;
            contraction = Smooth01(Mathf.Pow(t, 0.36f));
            propulsion = Mathf.Pow(t, 3.35f);
        }
        else
        {
            float t = (phase - contractionDuration) / (1f - contractionDuration);
            contraction = Mathf.Pow(1f - t, 1.85f);
            propulsion = 0f;
        }
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void BuildBell(Material bellMaterial, Color color)
    {
        if (bellTransform == null)
        {
            GameObject bellObject = new GameObject("Procedural_Hollow_Flared_Bell");
            bellObject.transform.SetParent(transform, false);
            bellTransform = bellObject.transform;
            MeshFilter meshFilter = bellObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateHollowFlaredBellMesh();
            bellRenderer = bellObject.AddComponent<MeshRenderer>();
        }
        bellTransform.localScale = new Vector3(size * 1.08f * BellWidthMultiplier, size * 0.72f * BellHeightMultiplier, size * 1.08f * BellWidthMultiplier);
        bellRenderer.sharedMaterial = bellMaterial;

        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        bellRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 0.09f)));
        propertyBlock.SetColor("_RimColor", new Color(
            Mathf.Lerp(color.r, 1f, 0.35f),
            Mathf.Lerp(color.g, 0.75f, 0.15f),
            Mathf.Lerp(color.b, 1f, 0.35f),
            Mathf.Clamp01(color.a)));
        propertyBlock.SetFloat("_PulseFrequency", pulseSpeed);
        propertyBlock.SetFloat("_PulseAmplitude", Mathf.Lerp(0.012f, 0.038f, pulseAmount));
        propertyBlock.SetFloat("_ImpulsePhase", 0f);
        propertyBlock.SetColor("_EmissionColor", new Color(color.r * 3f, color.g * 3f, color.b * 3f, 1f));
        propertyBlock.SetFloat("_PatternType", patternType);
        propertyBlock.SetVector("_PatternSpeed", new Vector4(patternSpeed.x, patternSpeed.y, 0f, 0f));
        bellRenderer.SetPropertyBlock(propertyBlock);
    }

    private void EnsureEmotionLabel()
    {
        if (!showEmotionLabel || thoughtEssence == null)
        {
            return;
        }

        if (labelMesh != null)
        {
            labelMesh.text = thoughtEssence.Label;
            return;
        }

        GameObject labelObject = new GameObject("ThoughtEssence_Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = Vector3.up * (size + labelHeight);

        labelMesh = labelObject.AddComponent<TextMesh>();
        labelMesh.text = thoughtEssence.Label;
        labelMesh.anchor = TextAnchor.MiddleCenter;
        labelMesh.alignment = TextAlignment.Center;
        labelMesh.characterSize = labelCharacterSize;
        labelMesh.fontSize = 42;
        labelMesh.color = labelColor;
    }

    private void UpdateEmotionLabelFacingCamera()
    {
        if (labelMesh == null)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        Transform labelTransform = labelMesh.transform;
        Vector3 toCamera = labelTransform.position - targetCamera.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            labelTransform.rotation = Quaternion.LookRotation(toCamera.normalized, targetCamera.transform.up);
        }
    }

    private void ApplyEmotionColor(Color color)
    {
        assignedColor = color;

        if (bellRenderer != null)
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            bellRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 0.11f)));
            propertyBlock.SetColor("_RimColor", new Color(
                Mathf.Lerp(color.r, 1f, 0.25f),
                Mathf.Lerp(color.g, 0.9f, 0.18f),
                Mathf.Lerp(color.b, 1f, 0.25f),
                Mathf.Clamp01(color.a)));
            propertyBlock.SetColor("_EmissionColor", new Color(color.r * 3f, color.g * 3f, color.b * 3f, 1f));
            propertyBlock.SetFloat("_PatternType", patternType);
            propertyBlock.SetVector("_PatternSpeed", new Vector4(patternSpeed.x, patternSpeed.y, 0f, 0f));
            bellRenderer.SetPropertyBlock(propertyBlock);
        }

        if (labelMesh != null)
        {
            labelMesh.color = new Color(
                Mathf.Lerp(color.r, 1f, 0.45f),
                Mathf.Lerp(color.g, 1f, 0.45f),
                Mathf.Lerp(color.b, 1f, 0.45f),
                labelColor.a);
        }

        ApplyLineColor(oralArms, color, 0.82f, 0.38f, 0f);
        ApplyLineColor(marginalTentacles, color, 0.7f, 0.25f, 0f);
    }

    private void SetRenderAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (bellRenderer != null)
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            bellRenderer.GetPropertyBlock(propertyBlock);
            Color baseColor = assignedColor;
            Color rimColor = assignedColor;
            baseColor.a = Mathf.Clamp01(assignedColor.a * 0.11f * alpha);
            rimColor.a = Mathf.Clamp01(assignedColor.a * alpha);
            propertyBlock.SetColor("_BaseColor", baseColor);
            propertyBlock.SetColor("_RimColor", rimColor);
            propertyBlock.SetColor("_EmissionColor", new Color(assignedColor.r * 3f * alpha, assignedColor.g * 3f * alpha, assignedColor.b * 3f * alpha, 1f));
            propertyBlock.SetFloat("_PatternType", patternType);
            propertyBlock.SetVector("_PatternSpeed", new Vector4(patternSpeed.x, patternSpeed.y, 0f, 0f));
            if (isRemoteMode)
            {
                propertyBlock.SetFloat("_EmissionIntensity", 4.5f * alpha);
            }
            bellRenderer.SetPropertyBlock(propertyBlock);
        }

        if (labelMesh != null)
        {
            Color c = labelMesh.color;
            c.a = labelColor.a * alpha;
            labelMesh.color = c;
        }

        ApplyLineColor(oralArms, assignedColor, 0.82f * alpha, 0.38f * alpha, 0f);
        ApplyLineColor(marginalTentacles, assignedColor, 0.7f * alpha, 0.25f * alpha, 0f);
    }

    private static void ApplyLineColor(LineRenderer[] lines, Color color, float rootAlpha, float midAlpha, float tipAlpha)
    {
        if (lines == null)
        {
            return;
        }

        Gradient gradient = BuildGradient(color, rootAlpha, midAlpha, tipAlpha);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] != null)
            {
                lines[i].colorGradient = gradient;
            }
        }
    }

    private Mesh CreateHollowFlaredBellMesh()
    {
        int rings = (BellProfileSegments + 1) * 2;
        Vector3[] vertices = new Vector3[(BellRadialSegments + 1) * rings];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[BellRadialSegments * (rings - 1) * 6];

        int vertex = 0;
        for (int side = 0; side < 2; side++)
        {
            for (int profile = 0; profile <= BellProfileSegments; profile++)
            {
                float t = profile / (float)BellProfileSegments;
                Vector2 p = side == 0 ? OuterProfile(t) : InnerProfile(1f - t);
                for (int slice = 0; slice <= BellRadialSegments; slice++)
                {
                    float u = slice / (float)BellRadialSegments;
                    float angle = u * Mathf.PI * 2f;
                    Vector3 point = new Vector3(Mathf.Cos(angle) * p.x, p.y, Mathf.Sin(angle) * p.x);
                    vertices[vertex] = point;
                    uv[vertex] = new Vector2(u, side == 0 ? t * 0.5f : 0.5f + t * 0.5f);
                    vertex++;
                }
            }
        }

        int stride = BellRadialSegments + 1;
        int tri = 0;
        for (int ring = 0; ring < rings - 1; ring++)
        {
            bool inner = ring >= BellProfileSegments + 1;
            for (int slice = 0; slice < BellRadialSegments; slice++)
            {
                int a = ring * stride + slice;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;

                if (!inner)
                {
                    triangles[tri++] = a;
                    triangles[tri++] = c;
                    triangles[tri++] = b;
                    triangles[tri++] = b;
                    triangles[tri++] = c;
                    triangles[tri++] = d;
                }
                else
                {
                    triangles[tri++] = a;
                    triangles[tri++] = b;
                    triangles[tri++] = c;
                    triangles[tri++] = b;
                    triangles[tri++] = d;
                    triangles[tri++] = c;
                }
            }
        }

        var mesh = new Mesh();
        mesh.name = "Runtime_Aurelia_Hollow_Flared_Bell";
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = normals[i].normalized;
        }
        mesh.normals = normals;
        return mesh;
    }

    private static Vector2 OuterProfile(float t)
    {
        float r = Mathf.Sin(t * Mathf.PI * 0.5f);
        r = Mathf.Pow(r, 0.68f);
        r += 0.13f * Smooth01(Mathf.InverseLerp(0.68f, 1f, t));
        float y = Mathf.Lerp(0.58f, -0.04f, Mathf.Pow(t, 1.18f));
        y += Mathf.Sin(t * Mathf.PI) * 0.16f;
        return new Vector2(r, y);
    }

    private static Vector2 InnerProfile(float t)
    {
        float r = Mathf.Lerp(0.16f, 0.96f, Mathf.Pow(t, 0.72f));
        float y = Mathf.Lerp(0.19f, -0.04f, t);
        y += Mathf.Sin(t * Mathf.PI) * 0.08f;
        return new Vector2(r, y);
    }

    private void BuildOralArms(Material material, Color color)
    {
        if (oralArms == null || oralArms.Length != MaxOralArmCount)
        {
            oralArms = new LineRenderer[MaxOralArmCount];
            oralArmPositions = new Vector3[MaxOralArmCount][];
            oralArmPreviousPositions = new Vector3[MaxOralArmCount][];
            oralArmRoots = new Vector3[MaxOralArmCount];

            for (int i = 0; i < MaxOralArmCount; i++)
            {
                float angle = i * Mathf.PI * 2f / MaxOralArmCount + Mathf.PI * 0.125f;
                oralArmRoots[i] = new Vector3(Mathf.Cos(angle) * 0.18f, 0.12f, Mathf.Sin(angle) * 0.18f);

                GameObject go = new GameObject("OralArm_Curtain_" + i);
                go.transform.SetParent(transform, false);
                LineRenderer line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.positionCount = OralArmRenderPositions;
                line.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0.25f),
                    new Keyframe(0.45f, 0.18f),
                    new Keyframe(1f, 0.02f));
                line.numCapVertices = 6;
                line.numCornerVertices = 8;
                line.textureMode = LineTextureMode.Stretch;
                oralArms[i] = line;
                oralArmPositions[i] = new Vector3[OralArmSegments];
                oralArmPreviousPositions[i] = new Vector3[OralArmSegments];
            }
        }

        for (int i = 0; i < MaxOralArmCount; i++)
        {
            LineRenderer line = oralArms[i];
            line.widthMultiplier = size;
            line.colorGradient = BuildGradient(color, 0.82f, 0.38f, 0f);

            Vector3 root = transform.TransformPoint(oralArmRoots[i] * size);
            for (int segment = 0; segment < OralArmSegments; segment++)
            {
                float t = segment / (float)(OralArmSegments - 1);
                Vector3 position = root + Vector3.down * (tentacleLength * TentacleLengthMultiplier * 0.95f * t);
                oralArmPositions[i][segment] = position;
                oralArmPreviousPositions[i][segment] = position;
            }
        }
    }

    private void BuildMarginalTentacles(Material material, Color color)
    {
        if (marginalTentacles == null || marginalTentacles.Length != MarginalTentacleCount)
        {
            marginalTentacles = new LineRenderer[MarginalTentacleCount];
            marginalPositions = new Vector3[MarginalTentacleCount][];
            marginalPreviousPositions = new Vector3[MarginalTentacleCount][];
            marginalRimRoots = new Vector3[MarginalTentacleCount];

            for (int i = 0; i < MarginalTentacleCount; i++)
            {
                float angle = i / (float)MarginalTentacleCount * Mathf.PI * 2f;
                marginalRimRoots[i] = new Vector3(Mathf.Cos(angle) * 1.08f, -0.04f, Mathf.Sin(angle) * 1.08f);
                GameObject go = new GameObject("Marginal_RimThread_" + i);
                go.transform.SetParent(transform, false);

                LineRenderer line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.positionCount = MarginalRenderPositions;
                line.numCapVertices = 4;
                line.numCornerVertices = 6;
                line.textureMode = LineTextureMode.Stretch;

                marginalTentacles[i] = line;
                marginalPositions[i] = new Vector3[MarginalSegments];
                marginalPreviousPositions[i] = new Vector3[MarginalSegments];
            }
        }

        for (int i = 0; i < MarginalTentacleCount; i++)
        {
            LineRenderer line = marginalTentacles[i];
            line.widthMultiplier = 0.015f * size;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            line.colorGradient = BuildGradient(color, 0.7f, 0.25f, 0f);

            Vector3 root = transform.TransformPoint(marginalRimRoots[i] * size);
            for (int segment = 0; segment < MarginalSegments; segment++)
            {
                float t = segment / (float)(MarginalSegments - 1);
                Vector3 position = root + Vector3.down * (tentacleLength * TentacleLengthMultiplier * 1.18f * t);
                marginalPositions[i][segment] = position;
                marginalPreviousPositions[i][segment] = position;
            }
        }
    }

    private static Gradient BuildGradient(Color color, float rootAlpha, float midAlpha, float tipAlpha)
    {
        Color root = color;
        root.a = Mathf.Clamp01(color.a * rootAlpha);
        Color mid = color;
        mid.a = Mathf.Clamp01(color.a * midAlpha);
        Color tip = color;
        tip.a = Mathf.Clamp01(color.a * tipAlpha);

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(root, 0f), new GradientColorKey(mid, 0.55f), new GradientColorKey(tip, 1f) },
            new[] { new GradientAlphaKey(root.a, 0f), new GradientAlphaKey(mid.a, 0.55f), new GradientAlphaKey(tip.a, 1f) });
        return gradient;
    }

    private void UpdateBellMaterial(float contraction)
    {
        if (bellRenderer == null)
        {
            return;
        }

        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        bellRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat("_PulseFrequency", pulseSpeed * pulseSpeedMultiplier);
        propertyBlock.SetFloat("_PulseAmplitude", Mathf.Lerp(0.008f, 0.032f, contraction * pulseAmount));
        propertyBlock.SetFloat("_ImpulsePhase", contraction);
        if (isRemoteMode)
        {
            float targetEmission = 4.5f;
            propertyBlock.SetFloat("_EmissionIntensity", targetEmission);
        }
        bellRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateOralArms(float time, float contraction, Vector3 repulsionStream)
    {
        for (int i = 0; i < MaxOralArmCount; i++)
        {
            if (i >= currentOralArmCount)
            {
                if (oralArms[i].enabled) oralArms[i].enabled = false;
                continue;
            }
            if (!oralArms[i].enabled) oralArms[i].enabled = true;

            Vector3 root = transform.TransformPoint(oralArmRoots[i] * size);
            for (int segment = 0; segment < OralArmSegments; segment++)
            {
                float t = segment / (float)(OralArmSegments - 1);
                float seed = driftPhase + i * 17.13f + segment * 0.77f;
                Vector3 local = oralArmRoots[i] * size;
                local.y -= t * tentacleLength * TentacleLengthMultiplier * 0.95f;
                local.x += (Mathf.PerlinNoise(seed, time * oralArmNoiseSpeed + t * 1.7f) - 0.5f) * oralArmNoiseAmplitude * size * t;
                local.z += (Mathf.PerlinNoise(time * oralArmNoiseSpeed + seed, t * 2.1f) - 0.5f) * oralArmNoiseAmplitude * size * t;
                local += oralArmRoots[i].normalized * Mathf.Sin(time * 0.65f + t * 4f + i) * 0.10f * size * t;
                Vector3 worldPos = segment == 0 ? root : transform.TransformPoint(local);
                if (segment > 0) worldPos += repulsionStream * (t * 0.05f);
                oralArmPositions[i][segment] = worldPos;
            }

            ApplySpline(oralArms[i], oralArmPositions[i], OralArmRenderPositions);
        }
    }

    private void UpdateMarginalTentacles(float time, float contraction, Vector3 repulsionStream)
    {
        float dt = Time.deltaTime;
        if (dt < 0.0001f) return;
        Vector3 rawHeadVelocity = (transform.position - lastPosition) / dt;
        Vector3 headVelocity = Vector3.ClampMagnitude(rawHeadVelocity, maxRoamSpeed * 2f);
        float rimRadius = Mathf.Lerp(1.14f, 0.82f, contraction);
        float segmentLength = tentacleLength * TentacleLengthMultiplier * 1.18f / (MarginalSegments - 1);
        float fixedDt = 1f / 60f;
        float dtRatio = dt / fixedDt;
        float dt2 = fixedDt * fixedDt * dtRatio;
        float phaseStiffness = Mathf.Lerp(marginalStiffness * 0.48f, marginalStiffness * 1.45f, contraction);
        float effectiveDamping = Mathf.Pow(marginalDamping, dtRatio);
        float noiseScale = Mathf.Lerp(0.5f, 0.08f, contraction);

        Vector3 bellCenter = transform.position;
        Vector3 bellUpAxis = transform.up;
        float baseMaxRadius = size * multiplicationCoefficient * BellWidthMultiplier * tensionCoefficient;

        for (int i = 0; i < marginalTentacles.Length; i++)
        {
            Vector3 rimDirection = marginalRimRoots[i].normalized;
            Vector3 localRoot = new Vector3(rimDirection.x * rimRadius, -0.04f - contraction * 0.09f, rimDirection.z * rimRadius) * size;
            Vector3 root = transform.TransformPoint(localRoot);
            Vector3 inward = transform.TransformDirection(-rimDirection);
            Vector3 backwardStream = -transform.up;
            marginalPositions[i][0] = root;
            marginalPreviousPositions[i][0] = root - headVelocity * dt;

            for (int segment = 1; segment < MarginalSegments; segment++)
            {
                float t = segment / (float)(MarginalSegments - 1);
                Vector3 current = marginalPositions[i][segment];
                Vector3 previous = marginalPreviousPositions[i][segment];
                Vector3 verletVelocity = (current - previous) * effectiveDamping;
                Vector3 noise = LocalizedCurrent(time, i, segment, t, contraction) * noiseScale;
                Vector3 wake = -headVelocity * Mathf.Lerp(0.01f, 0.06f, t) * Mathf.Lerp(0.1f, 0.4f, contraction);
                wake = Vector3.ClampMagnitude(wake, 0.5f);
                Vector3 sleekStream = (backwardStream + inward * 0.35f).normalized * contraction * Mathf.Lerp(0.2f, 0.9f, t);
                Vector3 gravity = Vector3.up * marginalGravity * t;
                Vector3 acceleration = noise + gravity + wake + sleekStream + repulsionStream * t;

                Vector3 toSegment = current - bellCenter;
                float h = Vector3.Dot(toSegment, bellUpAxis);
                Vector3 pointOnAxis = bellCenter + bellUpAxis * h;
                Vector3 radialOffset = current - pointOnAxis;
                float currentDistSq = radialOffset.sqrMagnitude;
                float allowedRadius = baseMaxRadius * Mathf.Lerp(1.0f, 1.35f, t);

                if (currentDistSq > allowedRadius * allowedRadius)
                {
                    float actualDist = Mathf.Sqrt(currentDistSq);
                    float penetration = actualDist - allowedRadius;
                    Vector3 inwardNormal = -radialOffset / actualDist;
                    float springStiffness = 45.0f;
                    acceleration += inwardNormal * (penetration * springStiffness);
                }

                marginalPreviousPositions[i][segment] = current;
                marginalPositions[i][segment] = current + verletVelocity + acceleration * dt2;
            }

            SatisfyConstraints(marginalPositions[i], segmentLength, phaseStiffness);
            ApplySpline(marginalTentacles[i], marginalPositions[i], MarginalRenderPositions);
        }
    }

    private Vector3 LocalizedCurrent(float time, int tentacleIndex, int segment, float t, float contraction)
    {
        float seed = driftPhase + tentacleIndex * 13.37f + segment * 2.11f;
        float noiseX = Mathf.PerlinNoise(seed, time * 0.18f + t * 1.7f) - 0.5f;
        float noiseY = Mathf.PerlinNoise(time * 0.12f + seed, t * 2.3f) - 0.5f;
        float noiseZ = Mathf.PerlinNoise(t * 1.9f + seed, time * 0.15f) - 0.5f;
        float amplitude = Mathf.Lerp(0.32f, 0.08f, contraction) * wobbleAmount * (0.25f + t * 0.75f);
        return new Vector3(noiseX, noiseY * 0.3f, noiseZ) * amplitude;
    }

    private static void SatisfyConstraints(Vector3[] chain, float segmentLength, float stiffness)
    {
        const int iterations = 5;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int segment = 1; segment < chain.Length; segment++)
            {
                Vector3 parent = chain[segment - 1];
                Vector3 child = chain[segment];
                Vector3 delta = child - parent;
                float distance = delta.magnitude;
                if (distance < 0.0001f)
                {
                    chain[segment] = parent + Vector3.down * segmentLength;
                    continue;
                }
                chain[segment] -= delta.normalized * (distance - segmentLength) * stiffness;
            }
        }
    }

    private static void ApplySpline(LineRenderer line, Vector3[] controlPoints, int renderPositions)
    {
        if (line == null || controlPoints == null || controlPoints.Length < 2)
        {
            return;
        }

        if (line.positionCount != renderPositions)
        {
            line.positionCount = renderPositions;
        }

        int outputIndex = 0;
        for (int segment = 0; segment < controlPoints.Length - 1; segment++)
        {
            Vector3 p0 = controlPoints[Mathf.Max(segment - 1, 0)];
            Vector3 p1 = controlPoints[segment];
            Vector3 p2 = controlPoints[segment + 1];
            Vector3 p3 = controlPoints[Mathf.Min(segment + 2, controlPoints.Length - 1)];

            for (int sample = 0; sample < SplineSamplesPerSegment; sample++)
            {
                float t = sample / (float)SplineSamplesPerSegment;
                line.SetPosition(outputIndex, CatmullRom(p0, p1, p2, p3, t));
                outputIndex++;
            }
        }
        line.SetPosition(outputIndex, controlPoints[controlPoints.Length - 1]);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }



    private bool CheckCameraViewportExit()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }
        Vector3 viewportPos = cam.WorldToViewportPoint(transform.position);
        return viewportPos.z < 0f || viewportPos.x < -0.3f || viewportPos.x > 1.3f || viewportPos.y < -0.3f || viewportPos.y > 1.3f;
    }

}
