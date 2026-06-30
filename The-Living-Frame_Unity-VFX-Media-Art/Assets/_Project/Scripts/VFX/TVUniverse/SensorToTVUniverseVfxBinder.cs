using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;
using Project.Sensors.Events;

namespace Project.VFX.TVUniverse
{
    public sealed class SensorToTVUniverseVfxBinder : MonoBehaviour
    {
        public static SensorToTVUniverseVfxBinder Instance { get; private set; }

        public Vector3 FinalWorldAttractor { get; private set; }
        public float FinalAttractorStrength { get; private set; }
        public Vector3 CurrentFlow => currentFlow;
        public float CurrentBurst => currentBurst;
        public float CurrentEnergy => currentEnergy;


        [Header("References")]
        [SerializeField] private SensorEventBus eventBus;
        [SerializeField] private VisualEffect targetVfx;
        [SerializeField] private WebcamMotionSignal webcamMotion;
        [SerializeField] private Transform tvFrameTransform;
        [SerializeField] private CrowdUniverseInteractionBinder crowdInteraction;

        [Header("Attractor Settings (Touch UV)")]
        [SerializeField] private Vector2 attractorExtent = new(6f, 3.4f);
        [SerializeField] private float touchDamping = 0.1f;
        [SerializeField] private float attractorStrengthActive = 10f;
        [SerializeField] private bool useLocalSpace = true;
        [SerializeField] private string touchDeviceIdFilter = "";

        [Header("Flow Settings (Tilt)")]
        [SerializeField] private Vector2 calibratedNeutral = Vector2.zero;
        [SerializeField] private float tiltDeadZone = 0.05f;
        [SerializeField] private float tiltMaxMagnitude = 1.0f;
        [SerializeField] private float maxFlowSpeed = 3.0f;
        [SerializeField] private float flowSmoothTime = 0.2f;
        [SerializeField] private bool invertTiltX = false;
        [SerializeField] private bool invertTiltY = false;
        [SerializeField] private string tiltDeviceIdFilter = "";

        [Header("Burst Settings (Acceleration)")]
        [SerializeField] private float gravityBaseline = 1.0f;
        [SerializeField] private float burstThreshold = 0.3f;
        [SerializeField] private float burstCooldown = 0.25f;
        [SerializeField] private float burstDecaySeconds = 0.35f;
        [SerializeField] private float burstStrengthMultiplier = 2.0f;
        [SerializeField] private string accelerationDeviceIdFilter = "";

        [Header("Energy Settings (Webcam)")]
        [SerializeField] private float energySmoothTime = 0.3f;
        [SerializeField] private float idleEnergy = 0.2f;

        [Header("Spawn Settings")]
        [SerializeField] private float defaultSpawnRate = 1000f;

        [Header("Signal Loss Fallback")]
        [SerializeField] private float sensorTimeout = 0.5f;
        [SerializeField] private float webcamTimeout = 1.0f;

        [Header("Webcam Spatial Interaction")]
        [SerializeField] private WebcamParticleMeshRenderer webcamRenderer;
        [Header("Tuning")]
        [SerializeField] private bool invertMotionBehavior = true;
        [SerializeField] private bool webcamAttractorEnabled = true;
        [SerializeField] private bool webcamFlowEnabled = false;
        [SerializeField] private bool webcamBurstEnabled = true;
        [SerializeField] private float webcamAttractorStrength = 16f;
        [SerializeField] private float webcamMotionThreshold = 0.005f;
        [SerializeField] private float webcamFlowScale = 2.2f;
        [SerializeField] private float webcamBurstSpeedThreshold = 1.5f;

        [Header("Runtime Tuning GUI")]
        [SerializeField] private bool showRuntimeTuningGui = true;
        [SerializeField] private Rect tuningGuiRect = new(12f, 64f, 300f, 430f);

        private Vector3 targetAttractor;
        private Vector3 currentAttractor;
        private Vector3 attractorVelocity;
        private float targetAttractorStrength;
        private float currentAttractorStrength;
        private float attractorStrengthVelocity;
        private float lastTouchTime;

        private Vector3 targetFlow;
        private Vector3 currentFlow;
        private Vector3 flowVelocity;
        private float lastTiltTime;

        private float currentBurst;
        private float lastImpulse;
        private float lastBurstTime;
        private float lastAccelerationTime;

        private float currentEnergy;
        private float energyVelocity;
        private float lastWebcamTime;

        private void Reset()
        {
            targetVfx = GetComponent<VisualEffect>();
        }

        private void Awake()
        {
            Instance = this;
            if (targetVfx == null)
            {
                targetVfx = GetComponent<VisualEffect>();
            }
        }

        private void Start()
        {
            if (eventBus == null)
            {
                eventBus = FindObjectOfType<SensorEventBus>();
            }

            if (webcamMotion == null)
            {
                webcamMotion = FindObjectOfType<WebcamMotionSignal>();
            }

            if (webcamRenderer == null)
            {
                webcamRenderer = FindObjectOfType<WebcamParticleMeshRenderer>();
            }

            if (crowdInteraction == null)
            {
                crowdInteraction = FindFirstObjectByType<CrowdUniverseInteractionBinder>();
            }

            if (crowdInteraction != null)
            {
                crowdInteraction.ConfigureVfx(targetVfx, tvFrameTransform, useLocalSpace);
            }

            ResetState();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (eventBus == null) return;

            eventBus.TouchUvReceived += OnTouch;
            eventBus.TiltReceived += OnTilt;
            eventBus.AccelerationReceived += OnAcceleration;
            eventBus.ConnectionChanged += OnConnectionChanged;
        }

        private void Unsubscribe()
        {
            if (eventBus == null) return;

            eventBus.TouchUvReceived -= OnTouch;
            eventBus.TiltReceived -= OnTilt;
            eventBus.AccelerationReceived -= OnAcceleration;
            eventBus.ConnectionChanged -= OnConnectionChanged;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                keyboard.f8Key.wasPressedThisFrame)
            {
                showRuntimeTuningGui = !showRuntimeTuningGui;
            }

            float now = Time.time;

            UpdateTouch(now);
            UpdateTilt(now);
            UpdateAcceleration(now);
            UpdateWebcam(now);

            ApplyToVfx();
        }

        private void OnTouch(TouchUvEvent evt)
        {
            if (!string.IsNullOrEmpty(touchDeviceIdFilter) && evt.DeviceId != touchDeviceIdFilter)
            {
                return;
            }

            Vector2 uv = new(Mathf.Clamp01(evt.Uv.x), Mathf.Clamp01(evt.Uv.y));
            targetAttractor = new Vector3(
                Mathf.Lerp(-attractorExtent.x, attractorExtent.x, uv.x),
                Mathf.Lerp(-attractorExtent.y, attractorExtent.y, uv.y),
                0f
            );

            targetAttractorStrength = attractorStrengthActive;
            lastTouchTime = Time.time;
        }

        private void OnTilt(VectorSensorEvent evt)
        {
            if (!string.IsNullOrEmpty(tiltDeviceIdFilter) && evt.DeviceId != tiltDeviceIdFilter)
            {
                return;
            }

            Vector2 tilt = new(evt.Value.x, evt.Value.y);
            tilt -= calibratedNeutral;

            if (tilt.magnitude < tiltDeadZone)
            {
                tilt = Vector2.zero;
            }

            tilt = Vector2.ClampMagnitude(tilt / tiltMaxMagnitude, 1f);

            if (invertTiltX) tilt.x = -tilt.x;
            if (invertTiltY) tilt.y = -tilt.y;

            targetFlow = new Vector3(tilt.x, tilt.y, 0f) * maxFlowSpeed;
            lastTiltTime = Time.time;
        }

        private void OnAcceleration(VectorSensorEvent evt)
        {
            if (!string.IsNullOrEmpty(accelerationDeviceIdFilter) && evt.DeviceId != accelerationDeviceIdFilter)
            {
                return;
            }

            float impulse = Mathf.Abs(evt.Value.magnitude - gravityBaseline);

            if (impulse >= burstThreshold &&
                Time.time - lastBurstTime >= burstCooldown &&
                lastImpulse < burstThreshold)
            {
                currentBurst = 1f;
                lastBurstTime = Time.time;
            }

            lastImpulse = impulse;
            lastAccelerationTime = Time.time;
        }

        private void OnConnectionChanged(DeviceConnectionEvent evt)
        {
            if (!evt.IsConnected)
            {
                if (evt.DeviceId == touchDeviceIdFilter)
                {
                    lastTouchTime = 0f;
                }
                else if (evt.DeviceId == tiltDeviceIdFilter)
                {
                    lastTiltTime = 0f;
                }
                else if (evt.DeviceId == accelerationDeviceIdFilter)
                {
                    lastAccelerationTime = 0f;
                }
            }
        }

        private void UpdateTouch(float now)
        {
            if (now - lastTouchTime <= sensorTimeout)
            {
                currentAttractor = Vector3.SmoothDamp(
                    currentAttractor,
                    targetAttractor,
                    ref attractorVelocity,
                    touchDamping
                );

                currentAttractorStrength = Mathf.SmoothDamp(
                    currentAttractorStrength,
                    targetAttractorStrength,
                    ref attractorStrengthVelocity,
                    touchDamping
                );
            }
            else if (webcamAttractorEnabled &&
                     webcamRenderer != null &&
                     webcamRenderer.TotalMotionAmount > webcamMotionThreshold)
            {
                Vector2 centroid = webcamRenderer.MotionCentroid;
                Vector3 targetWebcamAttractor = new Vector3(
                    Mathf.Lerp(-attractorExtent.x, attractorExtent.x, centroid.x),
                    Mathf.Lerp(-attractorExtent.y, attractorExtent.y, centroid.y),
                    0f
                );
                float motionWeight = Mathf.Clamp01(
                    webcamRenderer.TotalMotionAmount /
                    Mathf.Max(0.0001f, webcamMotionThreshold * 4f)
                );

                currentAttractor = Vector3.SmoothDamp(
                    currentAttractor,
                    targetWebcamAttractor,
                    ref attractorVelocity,
                    touchDamping
                );

                float targetStrength = webcamAttractorStrength * motionWeight;
                if (invertMotionBehavior)
                {
                    targetStrength = Mathf.Lerp(-webcamAttractorStrength * 1.5f, webcamAttractorStrength * 1.5f, motionWeight);
                }

                currentAttractorStrength = Mathf.SmoothDamp(
                    currentAttractorStrength,
                    targetStrength,
                    ref attractorStrengthVelocity,
                    touchDamping
                );
            }
            else
            {
                currentAttractor = Vector3.SmoothDamp(
                    currentAttractor,
                    Vector3.zero,
                    ref attractorVelocity,
                    touchDamping
                );

                float targetStrength = 0f;
                if (invertMotionBehavior && webcamAttractorEnabled)
                {
                    targetStrength = -webcamAttractorStrength * 1.5f;
                }

                currentAttractorStrength = Mathf.SmoothDamp(
                    currentAttractorStrength,
                    targetStrength,
                    ref attractorStrengthVelocity,
                    touchDamping
                );
            }
        }

        private void UpdateTilt(float now)
        {
            if (now - lastTiltTime <= sensorTimeout)
            {
                currentFlow = Vector3.SmoothDamp(
                    currentFlow,
                    targetFlow,
                    ref flowVelocity,
                    flowSmoothTime
                );
            }
            else if (webcamFlowEnabled &&
                     webcamRenderer != null &&
                     webcamRenderer.TotalMotionAmount > webcamMotionThreshold)
            {
                Vector2 vel = webcamRenderer.MotionVelocity;
                Vector3 targetWebcamFlow = new Vector3(vel.x, vel.y, 0f) * webcamFlowScale;

                currentFlow = Vector3.SmoothDamp(
                    currentFlow,
                    targetWebcamFlow,
                    ref flowVelocity,
                    flowSmoothTime
                );
            }
            else
            {
                currentFlow = Vector3.SmoothDamp(
                    currentFlow,
                    Vector3.zero,
                    ref flowVelocity,
                    flowSmoothTime
                );
            }
        }

        private void UpdateAcceleration(float now)
        {
            currentBurst = Mathf.MoveTowards(
                currentBurst,
                0f,
                Time.deltaTime / burstDecaySeconds
            );

            if (now - lastAccelerationTime <= sensorTimeout)
            {
            }
            else
            {
                lastImpulse = 0f;

                if (webcamBurstEnabled &&
                    webcamRenderer != null &&
                    webcamRenderer.TotalMotionAmount > webcamMotionThreshold)
                {
                    float speed = webcamRenderer.MotionVelocity.magnitude;
                    if (!invertMotionBehavior && speed >= webcamBurstSpeedThreshold && now - lastBurstTime >= burstCooldown)
                    {
                        currentBurst = 1f;
                        lastBurstTime = now;
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showRuntimeTuningGui)
            {
                return;
            }

            tuningGuiRect.width = Mathf.Max(tuningGuiRect.width, 300f);
            tuningGuiRect.height = Mathf.Max(tuningGuiRect.height, 430f);

            tuningGuiRect = GUI.Window(
                GetInstanceID(),
                tuningGuiRect,
                DrawTuningGui,
                "Webcam Universe"
            );
        }

        private void DrawTuningGui(int windowId)
        {
            GUILayout.BeginVertical();

            invertMotionBehavior = GUILayout.Toggle(invertMotionBehavior, "Invert Motion (Gather when Fast)");
            webcamAttractorEnabled = GUILayout.Toggle(webcamAttractorEnabled, "Attractor");
            webcamFlowEnabled = GUILayout.Toggle(webcamFlowEnabled, "Flow");
            webcamBurstEnabled = GUILayout.Toggle(webcamBurstEnabled, "Burst");

            GUILayout.Label("Attractor " + webcamAttractorStrength.ToString("0.0"));
            webcamAttractorStrength = GUILayout.HorizontalSlider(webcamAttractorStrength, 0f, 32f);

            GUILayout.Label("Flow " + webcamFlowScale.ToString("0.0"));
            webcamFlowScale = GUILayout.HorizontalSlider(webcamFlowScale, 0f, 8f);

            var spawner = UnityEngine.Object.FindFirstObjectByType<JellyfishCrowdSpawner>();
            if (spawner != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Move Speed " + spawner.globalSpeedMultiplier.ToString("0.00"));
                float newSpeed = GUILayout.HorizontalSlider(spawner.globalSpeedMultiplier, 0.1f, 5.0f);
                if (!Mathf.Approximately(newSpeed, spawner.globalSpeedMultiplier))
                {
                    spawner.SetGlobalSpeedMultiplier(newSpeed, true);
                }
                GUILayout.Label("Jellyfish Scale " + spawner.globalScaleMultiplier.ToString("0.00"));
                spawner.globalScaleMultiplier = GUILayout.HorizontalSlider(spawner.globalScaleMultiplier, 0.1f, 5.0f);
                GUILayout.Label("Bell Width " + spawner.globalBellWidthMultiplier.ToString("0.00"));
                spawner.globalBellWidthMultiplier = GUILayout.HorizontalSlider(spawner.globalBellWidthMultiplier, 0.1f, 5.0f);
                GUILayout.Label("Bell Height " + spawner.globalBellHeightMultiplier.ToString("0.00"));
                spawner.globalBellHeightMultiplier = GUILayout.HorizontalSlider(spawner.globalBellHeightMultiplier, 0.1f, 5.0f);
                GUILayout.Label("Leg Length " + spawner.globalTentacleLengthMultiplier.ToString("0.00"));
                spawner.globalTentacleLengthMultiplier = GUILayout.HorizontalSlider(spawner.globalTentacleLengthMultiplier, 0.1f, 5.0f);
                GUILayout.Label("Thick Tentacles " + spawner.globalThickTentacleCount.ToString());
                spawner.globalThickTentacleCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(spawner.globalThickTentacleCount, 0f, 16f));
                GUILayout.Space(10);
            }

            GUILayout.Label("Motion " + GetWebcamMotionLabel());

            if (crowdInteraction != null)
            {
                crowdInteraction.DrawTuningGui();
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, tuningGuiRect.width, 22f));
        }

        private string GetWebcamMotionLabel()
        {
            if (webcamRenderer == null)
            {
                return "none";
            }

            return webcamRenderer.TotalMotionAmount.ToString("0.000");
        }
#endif

        private void UpdateWebcam(float now)
        {
            float targetEnergy = idleEnergy;

            if (webcamMotion != null)
            {
                targetEnergy = webcamMotion.NormalizedMotion;
                lastWebcamTime = now;
            }

            if (now - lastWebcamTime > webcamTimeout)
            {
                targetEnergy = idleEnergy;
            }

            currentEnergy = Mathf.SmoothDamp(
                currentEnergy,
                targetEnergy,
                ref energyVelocity,
                energySmoothTime
            );
        }

        private void ApplyToVfx()
        {
            Vector3 finalPos = currentAttractor;
            float finalStrength = currentAttractorStrength;

            if (crowdInteraction != null)
            {
                crowdInteraction.AdjustAttractor(ref finalPos, ref finalStrength);
            }

            if (!useLocalSpace)
            {
                Transform origin = tvFrameTransform != null ? tvFrameTransform : (targetVfx != null ? targetVfx.transform : transform);
                finalPos = origin.TransformPoint(finalPos);
            }

            FinalWorldAttractor = finalPos;
            FinalAttractorStrength = finalStrength;

            if (targetVfx == null) return;

            if (targetVfx.HasVector3(TVUniverseVfxContract.AttractorPosition))
            {
                targetVfx.SetVector3(TVUniverseVfxContract.AttractorPosition, finalPos);
                if (targetVfx.HasFloat(TVUniverseVfxContract.AttractorStrength))
                {
                    targetVfx.SetFloat(TVUniverseVfxContract.AttractorStrength, finalStrength);
                }
            }

            if (targetVfx.HasVector3(TVUniverseVfxContract.FlowVector))
            {
                targetVfx.SetVector3(TVUniverseVfxContract.FlowVector, currentFlow);
            }

            if (targetVfx.HasFloat(TVUniverseVfxContract.BurstStrength))
            {
                targetVfx.SetFloat(TVUniverseVfxContract.BurstStrength, currentBurst * burstStrengthMultiplier);
            }

            if (targetVfx.HasFloat(TVUniverseVfxContract.Energy))
            {
                targetVfx.SetFloat(TVUniverseVfxContract.Energy, currentEnergy);
            }

            if (targetVfx.HasFloat(TVUniverseVfxContract.SpawnRate))
            {
                targetVfx.SetFloat(TVUniverseVfxContract.SpawnRate, defaultSpawnRate);
            }
        }

        private void ResetState()
        {
            targetAttractor = Vector3.zero;
            currentAttractor = Vector3.zero;
            attractorVelocity = Vector3.zero;
            targetAttractorStrength = 0f;
            currentAttractorStrength = 0f;
            attractorStrengthVelocity = 0f;
            lastTouchTime = -sensorTimeout;

            targetFlow = Vector3.zero;
            currentFlow = Vector3.zero;
            flowVelocity = Vector3.zero;
            lastTiltTime = -sensorTimeout;

            currentBurst = 0f;
            lastImpulse = 0f;
            lastBurstTime = -burstCooldown;
            lastAccelerationTime = -sensorTimeout;

            currentEnergy = idleEnergy;
            energyVelocity = 0f;
            lastWebcamTime = -webcamTimeout;
        }
    }
}
