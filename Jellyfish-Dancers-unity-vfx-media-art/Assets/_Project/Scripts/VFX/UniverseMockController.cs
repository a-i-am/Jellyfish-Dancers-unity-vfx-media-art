using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public sealed class UniverseMockController : MonoBehaviour
{
    public enum UniverseState
    {
        Idle,
        TouchAttractor,
        TiltFlow,
        ShakeBurst,
        CalmDown
    }

    [Header("Spatial Mapping")]
    [SerializeField] private Vector2 attractorExtent = new(6f, 3.4f);

    [Header("Spawn")]
    [SerializeField, Min(0f)] private float idleSpawnRate = 800f;
    [SerializeField, Min(0f)] private float touchSpawnBoost = 1600f;
    [SerializeField, Min(0f)] private float flowSpawnBoost = 500f;
    [SerializeField, Min(0f)] private float burstSpawnBoost = 8000f;

    [Header("Forces")]
    [SerializeField, Min(0f)] private float touchForce = 8f;
    [SerializeField, Min(0f)] private float flowSpeed = 3f;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float engageTime = 0.08f;
    [SerializeField, Min(0.01f)] private float calmDownTime = 1.5f;
    [SerializeField, Min(0.01f)] private float burstDuration = 0.35f;

    [Header("Runtime - Play Mode 확인용")]
    [SerializeField] private UniverseState currentState;
    [SerializeField] private Vector3 currentAttractorPosition;
    [SerializeField, Range(0f, 1f)] private float currentTouch;
    [SerializeField] private Vector3 currentFlow;
    [SerializeField, Range(0f, 1f)] private float currentBurst;
    [SerializeField, Range(0f, 1f)] private float currentEnergy;

    private VisualEffect visualEffect;

    private Vector3 attractorSmoothVelocity;
    private float touchSmoothVelocity;
    private Vector3 flowSmoothVelocity;

    private void Awake()
    {
        visualEffect = GetComponent<VisualEffect>();
    }

    private void Start()
    {
        if (!ValidateVfxContract())
        {
            enabled = false;
            return;
        }

        ResetMock();
        visualEffect.Play();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            ResetMock();
        }

        bool touchActive =
            mouse != null && mouse.leftButton.isPressed;

        Vector3 attractorTarget = currentAttractorPosition;

        if (touchActive)
        {
            Vector2 pointer = mouse.position.ReadValue();

            Vector2 uv = new(
                Mathf.Clamp01(pointer.x / Mathf.Max(1f, Screen.width)),
                Mathf.Clamp01(pointer.y / Mathf.Max(1f, Screen.height))
            );

            attractorTarget = new Vector3(
                (uv.x * 2f - 1f) * attractorExtent.x,
                (uv.y * 2f - 1f) * attractorExtent.y,
                0f
            );
        }

        currentAttractorPosition = Vector3.SmoothDamp(
            currentAttractorPosition,
            attractorTarget,
            ref attractorSmoothVelocity,
            engageTime
        );

        currentTouch = Mathf.SmoothDamp(
            currentTouch,
            touchActive ? 1f : 0f,
            ref touchSmoothVelocity,
            touchActive ? engageTime : calmDownTime
        );

        Vector2 flowInput = ReadFlowInput(keyboard);
        Vector3 flowTarget =
            new Vector3(flowInput.x, flowInput.y, 0f) * flowSpeed;

        currentFlow = Vector3.SmoothDamp(
            currentFlow,
            flowTarget,
            ref flowSmoothVelocity,
            flowInput.sqrMagnitude > 0f
                ? engageTime
                : calmDownTime
        );

        if (keyboard != null &&
            keyboard.spaceKey.wasPressedThisFrame)
        {
            currentBurst = 1f;
        }
        else
        {
            currentBurst = Mathf.MoveTowards(
                currentBurst,
                0f,
                Time.deltaTime / burstDuration
            );
        }

        UpdateState(touchActive, flowInput);
        ApplyToVfx();
    }

    private static Vector2 ReadFlowInput(Keyboard keyboard)
    {
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;

        if (keyboard.aKey.isPressed ||
            keyboard.leftArrowKey.isPressed)
        {
            input.x -= 1f;
        }

        if (keyboard.dKey.isPressed ||
            keyboard.rightArrowKey.isPressed)
        {
            input.x += 1f;
        }

        if (keyboard.sKey.isPressed ||
            keyboard.downArrowKey.isPressed)
        {
            input.y -= 1f;
        }

        if (keyboard.wKey.isPressed ||
            keyboard.upArrowKey.isPressed)
        {
            input.y += 1f;
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    private void UpdateState(
        bool touchActive,
        Vector2 flowInput)
    {
        if (currentBurst > 0.05f)
        {
            currentState = UniverseState.ShakeBurst;
        }
        else if (touchActive)
        {
            currentState = UniverseState.TouchAttractor;
        }
        else if (flowInput.sqrMagnitude > 0.001f)
        {
            currentState = UniverseState.TiltFlow;
        }
        else if (currentTouch > 0.02f ||
                 currentFlow.sqrMagnitude > 0.002f)
        {
            currentState = UniverseState.CalmDown;
        }
        else
        {
            currentState = UniverseState.Idle;
        }
    }

    private void ApplyToVfx()
    {
        float normalizedFlow = Mathf.Clamp01(
            currentFlow.magnitude / Mathf.Max(0.001f, flowSpeed)
        );

        currentEnergy = Mathf.Clamp01(
            currentTouch * 0.45f +
            normalizedFlow * 0.35f +
            currentBurst * 0.8f
        );

        float spawnRate =
            idleSpawnRate +
            currentTouch * touchSpawnBoost +
            normalizedFlow * flowSpawnBoost +
            currentBurst * burstSpawnBoost;

        visualEffect.SetFloat("SpawnRate", spawnRate);
        visualEffect.SetVector3(
            "AttractorPosition_position",
            currentAttractorPosition
        );
        visualEffect.SetFloat(
            "AttractorStrength",
            currentTouch * touchForce
        );
        visualEffect.SetVector3("FlowVector", currentFlow);
        visualEffect.SetFloat("BurstStrength", currentBurst);
        visualEffect.SetFloat("Energy", currentEnergy);
    }

    private bool ValidateVfxContract()
    {
        bool valid =
            visualEffect.HasFloat("SpawnRate") &&
            visualEffect.HasVector3("AttractorPosition_position") &&
            visualEffect.HasFloat("AttractorStrength") &&
            visualEffect.HasVector3("FlowVector") &&
            visualEffect.HasFloat("BurstStrength") &&
            visualEffect.HasFloat("Energy");

        if (!valid)
        {
            Debug.LogError(
                "VFX Blackboard의 Exposed Property 이름이나 타입이 " +
                "UniverseMockController 계약과 일치하지 않습니다.",
                this
            );
        }

        return valid;
    }

    private void ResetMock()
    {
        currentState = UniverseState.Idle;
        currentAttractorPosition = Vector3.zero;
        attractorSmoothVelocity = Vector3.zero;
        currentTouch = 0f;
        touchSmoothVelocity = 0f;
        currentFlow = Vector3.zero;
        flowSmoothVelocity = Vector3.zero;
        currentBurst = 0f;
        currentEnergy = 0f;

        ApplyToVfx();
        visualEffect.Reinit();
    }
}