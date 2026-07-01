using Project.VFX.TVUniverse;
using Project.Sensors.Network;
using Project.Sensors.Events;
using UnityEngine;

public sealed class JellyfishCrowdSpawner : MonoBehaviour
{
    private const string SpeedPrefsKey = "LivingFrame.JellyfishMoveSpeed";

    [Header("Crowd Replacement")]
    [SerializeField] private bool disableSilhouetteCrowd = true;

    [Header("Aquarium Bounds")]
    [SerializeField] private Vector2 xBounds = new Vector2(-4.2f, 4.2f);
    [SerializeField] private Vector2 yBounds = new Vector2(0.35f, 3.2f);
    [SerializeField] private Vector2 zBounds = new Vector2(-3.35f, 0.85f);

    [Header("Crowd Settings")]
    [SerializeField] private int jellyfishCount = 1;
    [SerializeField] private float minSize = 0.05f;
    [SerializeField] private float maxSize = 0.09f;
    [SerializeField] private float minSpeed = 0.45f;
    [SerializeField] private float maxSpeed = 0.85f;
    [SerializeField] private float minPulseSpeed = 0.65f;
    [SerializeField] private float maxPulseSpeed = 1.6f;
    [SerializeField] private float driftWobble = 0.18f;

    [Header("Global Adjustments")]
    [Range(0.1f, 5.0f)]
    public float globalSpeedMultiplier = 1f;
    [Range(0.1f, 5.0f)]
    public float globalScaleMultiplier = 0.34f;
    [Range(0.1f, 5.0f)]
    public float globalBellHeightMultiplier = 2.24f;
    [Range(0.1f, 5.0f)]
    public float globalBellWidthMultiplier = 0.83f;
    [Range(0.1f, 5.0f)]
    public float globalTentacleLengthMultiplier = 0.80f;
    [Range(0, 16)]
    public int globalThickTentacleCount = 0;

    private float lastGlobalScale = 0.34f;
    private float lastBellHeight = 2.24f;
    private float lastBellWidth = 0.83f;
    private float lastTentacleLength = 0.80f;
    private int lastThickTentacleCount = 0;

    [Header("Shape (3D Silhouette Particles)")]
    [SerializeField] private float minTentacleLength = 0.45f;
    [SerializeField] private float maxTentacleLength = 1.05f;

    [Header("Hybrid Swarm Integration")]
    [SerializeField] private UnityEngine.VFX.VisualEffect swarmVfx;
    [SerializeField] private float swarmSpawnRate = 4000f;
    [SerializeField] private float swarmAttractorStrength = 8f;
    [SerializeField] private WebcamParticleMeshRenderer webcamRenderer;

    [Header("Interaction")]
    [SerializeField] private float gravityStrength = 0.28f;
    [SerializeField] private float gravityRadius = 1.8f;
    [SerializeField] private JellyfishEmotionResultLog resultLog;

    [Header("Look")]
    [SerializeField] private Color cyan = new Color(0.30f, 0.95f, 1.0f, 0.34f);
    [SerializeField] private Color violet = new Color(0.78f, 0.42f, 1.0f, 0.32f);
    [SerializeField] private Color warmWhite = new Color(1.0f, 0.92f, 0.62f, 0.30f);

    [Header("Dual Mode Settings")]
    [SerializeField] private bool useRemoteSpawnMode = true;
    [SerializeField] private Light directionalLight;
    [SerializeField] private Color remoteAmbientColor = Color.black;

    [SerializeField] private GameObject signalTrailPrefab;
    private Material bellMaterial;
    private Material tentacleMaterial;
    private UnityEngine.Pool.IObjectPool<JellyfishAgent> agentPool;
    private JellyfishTcpServer tcpServer;
    private JellyfishTcpServer.SpawnRequest? currentSurveyData = null;
    private float magicSpawnCooldown = 0.08f;
    private float lastMagicSpawnTime = 0f;
    private float currentSwarmSpawnRate = 0f;
    private float currentSwarmAttractor = 0f;
    private Vector3 currentSwarmPosition = Vector3.zero;
    private float originalLightIntensity;

    private void Awake()
    {
        if (PlayerPrefs.HasKey(SpeedPrefsKey))
        {
            globalSpeedMultiplier = PlayerPrefs.GetFloat(SpeedPrefsKey, globalSpeedMultiplier);
        }

        SetGlobalSpeedMultiplier(globalSpeedMultiplier, false);

        if (disableSilhouetteCrowd)
        {
            DisablePeopleSpawner();
        }

        EnsureMaterials();
        EnsureResultLog();
    }
    private void Start()
    {
        InitializePool();
        bool remoteMode = true;
        if (StageModeManager.Instance != null)
        {
            remoteMode = StageModeManager.Instance.UseRemoteSpawnMode;
        }
        else
        {
            remoteMode = useRemoteSpawnMode;
        }
        if (remoteMode)
        {
            tcpServer = GetComponent<JellyfishTcpServer>();
            if (tcpServer == null)
            {
                tcpServer = FindFirstObjectByType<JellyfishTcpServer>();
            }
            if (InputDispatcher.Instance != null)
            {
                InputDispatcher.Instance.OnPointerPressed += OnPointerPressed;
            }
            SensorEventBus eventBus = FindFirstObjectByType<SensorEventBus>();
            if (eventBus != null)
            {
                eventBus.TouchUvReceived += OnTouchUvReceived;
            }
        }


        for (int i = 0; i < jellyfishCount; i++)
        {
            SpawnLocalJellyfish(i);
        }
    }

    private void OnDisable()
    {
    }

    private void DisablePeopleSpawner()
    {
        SilhouetteCrowdSpawner[] peopleSpawners = FindObjectsByType<SilhouetteCrowdSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < peopleSpawners.Length; i++)
        {
            SilhouetteCrowdSpawner spawner = peopleSpawners[i];
            if (spawner != null && spawner != this)
            {
                spawner.enabled = false;
            }
        }
    }

    private void EnsureMaterials()
    {
        bellMaterial = CreateBellMaterial();
        tentacleMaterial = CreateTransparentMaterial("Runtime_Jellyfish_Tentacle", 0.48f);
    }

    private Material CreateBellMaterial()
    {
        Shader shader = Shader.Find("TheLivingFrame/JellyfishBell");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader);
        material.name = "Runtime_Jellyfish_Bell_Dome";
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetColor("_BaseColor", new Color(0.35f, 0.95f, 1f, 0.055f));
        material.SetColor("_RimColor", new Color(0.85f, 0.45f, 1f, 0.58f));
        material.SetFloat("_EmissionIntensity", 1.9f);
        material.SetFloat("_CenterAlpha", 0.01f);
        material.SetFloat("_RimAlpha", 0.44f);
        material.SetFloat("_FresnelPower", 2.2f);
        material.SetFloat("_PulseFrequency", 1.15f);
        material.SetFloat("_PulseAmplitude", 0.045f);
        material.SetFloat("_WaveSpeed", 7.0f);
        material.SetFloat("_ImpulsePhase", 0.0f);
        material.SetFloat("_GonadOffset", 0.23f);
        material.SetFloat("_GonadRadius", 0.145f);
        material.SetFloat("_GonadThickness", 0.028f);
        material.SetColor("_GonadEmissionColor", new Color(1.0f, 0.32f, 0.95f, 1.0f));
        return material;
    }

    private Material CreateTransparentMaterial(string materialName, float alpha)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetColor("_BaseColor", new Color(0.55f, 0.9f, 1f, alpha));
        material.SetColor("_Color", new Color(0.55f, 0.9f, 1f, alpha));

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);

        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        return material;
    }

    private void InitializePool()
    {
        agentPool = new UnityEngine.Pool.ObjectPool<JellyfishAgent>(
            createFunc: CreateJellyfishInstance,
            actionOnGet: OnGetJellyfish,
            actionOnRelease: OnReleaseJellyfish,
            actionOnDestroy: OnDestroyJellyfish,
            collectionCheck: false,
            defaultCapacity: 24,
            maxSize: 100
        );
    }

    private JellyfishAgent CreateJellyfishInstance()
    {
        GameObject go = new GameObject("Jellyfish_Agent");
        go.transform.SetParent(transform, false);
        JellyfishAgent agent = go.AddComponent<JellyfishAgent>();
        agent.SetPool(agentPool);
        CrowdGravitySource source = go.AddComponent<CrowdGravitySource>();
        source.Configure(gravityStrength, gravityRadius);
        return agent;
    }

    private void OnGetJellyfish(JellyfishAgent agent)
    {
        agent.gameObject.SetActive(true);
    }

    private void OnReleaseJellyfish(JellyfishAgent agent)
    {
        agent.gameObject.SetActive(false);
    }

    private void OnDestroyJellyfish(JellyfishAgent agent)
    {
        Destroy(agent.gameObject);
    }

    private void EvaluatePayload(float h, out Color color, out float speed, out int pattern)
    {
        h = Mathf.Clamp01(h);
        if (h < 0.5f)
        {
            float t = h / 0.5f;
            color = Color.Lerp(Color.cyan, Color.yellow, t);
            speed = Mathf.Lerp(0.7f, 1.1f, t);
        }
        else
        {
            float t = (h - 0.5f) / 0.5f;
            color = Color.Lerp(Color.yellow, Color.magenta, t);
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

    private Vector3 GetRandomViewportEdgePosition()
    {
        float x = Random.value < 0.5f ? xBounds.x - 2f : xBounds.y + 2f;
        float y = Random.Range(yBounds.x, yBounds.y);
        float z = Random.Range(zBounds.x, zBounds.y);
        return new Vector3(x, y, z);
    }

    private void Update()
    {
        JellyfishAgent.GlobalSpeedMultiplier = globalSpeedMultiplier;

        if (!Mathf.Approximately(globalScaleMultiplier, lastGlobalScale) ||
            !Mathf.Approximately(globalBellHeightMultiplier, lastBellHeight) ||
            !Mathf.Approximately(globalBellWidthMultiplier, lastBellWidth) ||
            !Mathf.Approximately(globalTentacleLengthMultiplier, lastTentacleLength) ||
            globalThickTentacleCount != lastThickTentacleCount)
        {
            lastGlobalScale = globalScaleMultiplier;
            lastBellHeight = globalBellHeightMultiplier;
            lastBellWidth = globalBellWidthMultiplier;
            lastTentacleLength = globalTentacleLengthMultiplier;
            lastThickTentacleCount = globalThickTentacleCount;
            foreach (var agent in JellyfishAgent.ActiveAgents)
            {
                if (agent != null && agent.gameObject.activeSelf)
                {
                    agent.transform.localScale = Vector3.one * globalScaleMultiplier;
                    agent.UpdateProportions(globalBellHeightMultiplier, globalBellWidthMultiplier, globalTentacleLengthMultiplier, globalThickTentacleCount);
                }
            }
        }

        bool remoteMode = true;
        if (StageModeManager.Instance != null)
        {
            remoteMode = StageModeManager.Instance.UseRemoteSpawnMode;
        }
        else
        {
            remoteMode = useRemoteSpawnMode;
        }
        if (remoteMode && tcpServer != null)
        {
            while (tcpServer.TryDequeueRequest(out var request))
            {
                currentSurveyData = request;
            }
        }

        if (remoteMode)
        {
            bool isInteracting = false;
            Vector3 spawnPoint = Vector3.zero;

            if (InputDispatcher.Instance != null && InputDispatcher.Instance.IsPressed)
            {
                isInteracting = true;
                Camera cam = Camera.main;
                if (cam != null)
                {
                    float depth = Mathf.Abs(cam.transform.position.z - ((zBounds.x + zBounds.y) * 0.5f));
                    spawnPoint = cam.ScreenToWorldPoint(new Vector3(InputDispatcher.Instance.Position.x, InputDispatcher.Instance.Position.y, depth));
                }
            }
            else if (CrowdUniverseInteractionBinder.Instance != null && CrowdUniverseInteractionBinder.Instance.IsLiveWebcamInteraction)
            {
                isInteracting = true;

                if (webcamRenderer != null && webcamRenderer.TryGetRandomSilhouetteViewportPoint(out Vector2 uv))
                {
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        float depth = Mathf.Abs(cam.transform.position.z - ((zBounds.x + zBounds.y) * 0.5f));
                        spawnPoint = cam.ViewportToWorldPoint(new Vector3(uv.x, uv.y, depth));

                        spawnPoint.z += Random.Range(-0.8f, 0.8f);
                    }
                }
                else
                {
                    spawnPoint = CrowdUniverseInteractionBinder.Instance.WebcamFocusPoint;
                }
            }

            Vector3 swarmCenter = CrowdUniverseInteractionBinder.Instance != null && CrowdUniverseInteractionBinder.Instance.IsLiveWebcamInteraction
                ? CrowdUniverseInteractionBinder.Instance.WebcamFocusPoint
                : spawnPoint;

            UpdateSwarmVfx(isInteracting, swarmCenter);
        }
    }

    public void SetGlobalSpeedMultiplier(float value, bool save)
    {
        globalSpeedMultiplier = Mathf.Clamp(value, 0.1f, 5.0f);
        JellyfishAgent.GlobalSpeedMultiplier = globalSpeedMultiplier;

        if (save)
        {
            PlayerPrefs.SetFloat(SpeedPrefsKey, globalSpeedMultiplier);
            PlayerPrefs.Save();
        }
    }

    private void UpdateSwarmVfx(bool isInteracting, Vector3 spawnPoint)
    {
        if (swarmVfx == null) return;

        float targetRate = isInteracting ? swarmSpawnRate : 0f;
        float targetAttractor = isInteracting ? swarmAttractorStrength : 0f;

        currentSwarmSpawnRate = Mathf.MoveTowards(currentSwarmSpawnRate, targetRate, Time.deltaTime * swarmSpawnRate * 2f);
        currentSwarmAttractor = Mathf.MoveTowards(currentSwarmAttractor, targetAttractor, Time.deltaTime * swarmAttractorStrength * 2f);

        if (isInteracting)
        {
            currentSwarmPosition = Vector3.Lerp(currentSwarmPosition, spawnPoint, Time.deltaTime * 15f);
        }

        if (swarmVfx.HasFloat("SpawnRate"))
            swarmVfx.SetFloat("SpawnRate", currentSwarmSpawnRate);

        if (swarmVfx.HasVector3("AttractorPosition_position"))
            swarmVfx.SetVector3("AttractorPosition_position", currentSwarmPosition);

        if (swarmVfx.HasFloat("AttractorStrength"))
            swarmVfx.SetFloat("AttractorStrength", currentSwarmAttractor);

        if (swarmVfx.HasFloat("Energy"))
            swarmVfx.SetFloat("Energy", isInteracting ? 0.8f : 0.2f);

        if (swarmVfx.HasFloat("BurstStrength"))
            swarmVfx.SetFloat("BurstStrength", isInteracting ? 0.2f : 0f);

        if (swarmVfx.HasVector3("FlowVector"))
            swarmVfx.SetVector3("FlowVector", Vector3.up * 0.5f);

        if (currentSurveyData.HasValue)
        {
            EvaluatePayload(currentSurveyData.Value.h, out Color color, out float speed, out int pattern);
            if (swarmVfx.HasVector4("BaseColor"))
                swarmVfx.SetVector4("BaseColor", color);
            if (swarmVfx.HasVector4("Color"))
                swarmVfx.SetVector4("Color", color);
        }
    }

    private void TriggerSpawnTransition(JellyfishTcpServer.SpawnRequest request)
    {
        EvaluatePayload(request.h, out Color color, out float speed, out int pattern);
        Vector3 spawnTarget = new Vector3(
            (xBounds.x + xBounds.y) * 0.5f,
            (yBounds.x + yBounds.y) * 0.5f,
            (zBounds.x + zBounds.y) * 0.5f
        );
        WebcamParticleMeshRenderer webcamRenderer = FindFirstObjectByType<WebcamParticleMeshRenderer>();
        if (webcamRenderer != null)
        {
            Vector2 uv;
            if (webcamRenderer.TryGetRandomSilhouetteViewportPoint(out uv))
            {
                spawnTarget = new Vector3(
                    Mathf.Lerp(xBounds.x, xBounds.y, uv.x),
                    Mathf.Lerp(yBounds.x, yBounds.y, uv.y),
                    Random.Range(zBounds.x, zBounds.y)
                );
            }
        }
        Vector3 startPosition = GetRandomViewportEdgePosition();
        if (signalTrailPrefab != null)
        {
            GameObject trailGo = Instantiate(signalTrailPrefab, startPosition, Quaternion.identity);
            SignalTrailEffect effect = trailGo.GetComponent<SignalTrailEffect>();
            if (effect != null)
            {
                effect.Configure(color, spawnTarget, () => {
                    SpawnJellyfishFromPool(spawnTarget, color, speed, pattern);
                });
            }
            else
            {
                SpawnJellyfishFromPool(spawnTarget, color, speed, pattern);
            }
        }
        else
        {
            GameObject trailGo = new GameObject("Dynamic_SignalTrail");
            trailGo.transform.position = startPosition;
            ParticleSystem ps = trailGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = color;
            main.startSize = 0.1f;
            main.startLifetime = 1.0f;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 50f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.High;
            noise.frequency = 1.2f;
            noise.strength = 0.5f;
            var renderer = trailGo.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            SignalTrailEffect effect = trailGo.AddComponent<SignalTrailEffect>();
            effect.Configure(color, spawnTarget, () => {
                SpawnJellyfishFromPool(spawnTarget, color, speed, pattern);
            });
        }
    }

    private void SpawnJellyfishFromPool(Vector3 position, Color color, float speed, int pattern)
    {
        if (agentPool == null)
        {
            InitializePool();
        }
        JellyfishAgent agent = agentPool.Get();
        agent.transform.position = position;
        agent.transform.localScale = Vector3.zero;
        Vector3 velocity = new Vector3(
            Random.Range(-0.2f, 0.2f),
            Random.Range(-0.5f, 0.5f),
            Random.Range(-0.2f, 0.2f)
        ).normalized;
        agent.Initialize(
            bellMaterial,
            tentacleMaterial,
            color,
            velocity,
            xBounds,
            yBounds,
            zBounds,
            speed * 0.15f,
            Random.Range(minSize, maxSize),
            Random.Range(0.65f, 1.6f),
            0.2f,
            Random.Range(minTentacleLength, maxTentacleLength),
            0.18f
        );
        agent.ConfigureRemoteMode(true, 99999f, true);
        agent.SetPattern(pattern, new Vector2(speed * 0.5f, speed * 0.5f));
        agent.UpdateProportions(globalBellHeightMultiplier, globalBellWidthMultiplier, globalTentacleLengthMultiplier, globalThickTentacleCount);
        StartCoroutine(SpawnScaleCoroutine(agent));
    }

    private System.Collections.IEnumerator SpawnScaleCoroutine(JellyfishAgent agent)
    {
        float timer = 0f;
        float duration = 1.0f;
        while (timer < duration && agent != null && agent.gameObject.activeSelf)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float scale = EaseOutBack(progress);
            agent.transform.localScale = Vector3.one * scale * globalScaleMultiplier;
            yield return null;
        }
        if (agent != null)
        {
            agent.transform.localScale = Vector3.one * globalScaleMultiplier;
        }
    }

    private float EaseOutBack(float x)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private void SpawnLocalJellyfish(int index)
    {
        Vector3 position = new Vector3(
            Random.Range(xBounds.x, xBounds.y),
            Random.Range(yBounds.x, yBounds.y),
            Random.Range(zBounds.x, zBounds.y)
        );
        JellyfishAgent agent = agentPool.Get();
        agent.transform.position = position;
        agent.transform.localScale = Vector3.one * globalScaleMultiplier;
        Vector3 velocity = new Vector3(
            Random.Range(-0.45f, 0.45f),
            Random.Range(-0.5f, 0.5f),
            Random.Range(-0.25f, 0.25f)
        ).normalized;
        float colorPick = Random.value;
        Color color = colorPick < 0.45f ? cyan : colorPick < 0.82f ? violet : warmWhite;
        color.a *= Random.Range(0.8f, 1.15f);
        float speed = Random.Range(minSpeed, maxSpeed);
        agent.Initialize(
            bellMaterial,
            tentacleMaterial,
            color,
            velocity,
            xBounds,
            yBounds,
            zBounds,
            speed,
            Random.Range(minSize, maxSize),
            Random.Range(minPulseSpeed, maxPulseSpeed),
            Random.Range(0.12f, 0.26f),
            Random.Range(minTentacleLength, maxTentacleLength),
            driftWobble
        );
        agent.ConfigureRemoteMode(false, 0f, true);
        agent.UpdateProportions(globalBellHeightMultiplier, globalBellWidthMultiplier, globalTentacleLengthMultiplier, globalThickTentacleCount);
    }

    private void EnsureResultLog()
    {
        if (resultLog != null)
        {
            return;
        }
        resultLog = FindFirstObjectByType<JellyfishEmotionResultLog>();
        if (resultLog != null)
        {
            return;
        }
        GameObject go = new GameObject("EmotionResultLog");
        resultLog = go.AddComponent<JellyfishEmotionResultLog>();
    }

    public void TriggerTestSpawn()
    {
        Vector3 spawnTarget = new Vector3(
            (xBounds.x + xBounds.y) * 0.5f,
            (yBounds.x + yBounds.y) * 0.5f,
            (zBounds.x + zBounds.y) * 0.5f
        );
        SpawnFromInteraction(spawnTarget);
    }

    public void SimulateSurveyData(float h)
    {
        currentSurveyData = new JellyfishTcpServer.SpawnRequest { h = h, spd = 1.0f, pat = 1 };
    }

    private void SpawnMagicJellyfish(Vector3 spawnTarget, JellyfishTcpServer.SpawnRequest request)
    {
        EvaluatePayload(request.h, out Color color, out float speed, out int pattern);

        Vector3 offset = Random.insideUnitSphere * 0.35f;
        offset.z = 0f;
        Vector3 spawnPos = spawnTarget + offset;

        spawnPos.x = Mathf.Clamp(spawnPos.x, xBounds.x, xBounds.y);
        spawnPos.y = Mathf.Clamp(spawnPos.y, yBounds.x, yBounds.y);
        spawnPos.z = Mathf.Clamp(spawnPos.z, zBounds.x, zBounds.y);

        SpawnJellyfishFromPool(spawnPos, color, speed, pattern);

        UniverseMockController mock = FindFirstObjectByType<UniverseMockController>();
        if (mock != null)
        {


        }
    }

    private Light FindDirectionalLight()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                return l;
            }
        }
        return null;
    }

    private void OnDestroy()
    {
        if (InputDispatcher.Instance != null)
        {
            InputDispatcher.Instance.OnPointerPressed -= OnPointerPressed;
        }
        SensorEventBus eventBus = FindFirstObjectByType<SensorEventBus>();
        if (eventBus != null)
        {
            eventBus.TouchUvReceived -= OnTouchUvReceived;
        }
    }

    private void OnPointerPressed(Vector2 screenPos)
    {
        if (currentSurveyData.HasValue) return;

        Camera cam = Camera.main;
        if (cam == null) return;
        float depth = Mathf.Abs(cam.transform.position.z - ((zBounds.x + zBounds.y) * 0.5f));
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        SpawnFromInteraction(worldPos);
    }

    private void OnTouchUvReceived(TouchUvEvent evt)
    {
        if (currentSurveyData.HasValue) return;

        Vector3 worldPos = new Vector3(
            Mathf.Lerp(xBounds.x, xBounds.y, evt.Uv.x),
            Mathf.Lerp(yBounds.x, yBounds.y, evt.Uv.y),
            Random.Range(zBounds.x, zBounds.y)
        );
        SpawnFromInteraction(worldPos);
    }

    private void SpawnFromInteraction(Vector3 spawnTarget)
    {
    }

#if UNITY_EDITOR
    private int guiJellyfishCount = 1;
    private System.Collections.Generic.List<JellyfishAgent> localAgents = new System.Collections.Generic.List<JellyfishAgent>();

    private void OnGUI()
    {
        float x = 10f;
        float y = Screen.height - 112f;
        GUI.Label(new Rect(x, y, 200f, 20f), "Jellyfish Count: " + guiJellyfishCount);
        float newVal = GUI.HorizontalSlider(new Rect(x, y + 24f, 200f, 20f), guiJellyfishCount, 0, 40);
        int desired = Mathf.RoundToInt(newVal);
        if (desired != guiJellyfishCount)
        {
            guiJellyfishCount = desired;
            AdjustLocalJellyfishCount(guiJellyfishCount);
        }

        GUI.Label(new Rect(x, y + 48f, 200f, 20f), "Move Speed: " + globalSpeedMultiplier.ToString("0.00"));
        float newSpeed = GUI.HorizontalSlider(new Rect(x, y + 72f, 200f, 20f), globalSpeedMultiplier, 0.1f, 5.0f);
        if (!Mathf.Approximately(newSpeed, globalSpeedMultiplier))
        {
            SetGlobalSpeedMultiplier(newSpeed, true);
        }
    }

    private void AdjustLocalJellyfishCount(int target)
    {
        while (localAgents.Count > target)
        {
            JellyfishAgent agent = localAgents[localAgents.Count - 1];
            localAgents.RemoveAt(localAgents.Count - 1);
            if (agent != null && agent.gameObject.activeSelf)
            {
                agent.StartSublimation();
            }
        }
        while (localAgents.Count < target)
        {
            SpawnAndTrackLocal(localAgents.Count);
        }
    }

    private void SpawnAndTrackLocal(int index)
    {
        Vector3 position = new Vector3(
            Random.Range(xBounds.x, xBounds.y),
            Random.Range(yBounds.x, yBounds.y),
            Random.Range(zBounds.x, zBounds.y)
        );
        JellyfishAgent agent = agentPool.Get();
        agent.transform.position = position;
        agent.transform.localScale = Vector3.one * globalScaleMultiplier;
        Vector3 velocity = new Vector3(
            Random.Range(-0.45f, 0.45f),
            Random.Range(-0.5f, 0.5f),
            Random.Range(-0.25f, 0.25f)
        ).normalized;
        float colorPick = Random.value;
        Color color = colorPick < 0.45f ? cyan : colorPick < 0.82f ? violet : warmWhite;
        color.a *= Random.Range(0.8f, 1.15f);
        float speed = Random.Range(minSpeed, maxSpeed);
        agent.Initialize(
            bellMaterial,
            tentacleMaterial,
            color,
            velocity,
            xBounds,
            yBounds,
            zBounds,
            speed,
            Random.Range(minSize, maxSize),
            Random.Range(minPulseSpeed, maxPulseSpeed),
            Random.Range(0.12f, 0.26f),
            Random.Range(minTentacleLength, maxTentacleLength),
            driftWobble
        );
        agent.ConfigureRemoteMode(false, 0f, true);
        agent.UpdateProportions(globalBellHeightMultiplier, globalBellWidthMultiplier, globalTentacleLengthMultiplier, globalThickTentacleCount);
        localAgents.Add(agent);
    }
#endif
}
