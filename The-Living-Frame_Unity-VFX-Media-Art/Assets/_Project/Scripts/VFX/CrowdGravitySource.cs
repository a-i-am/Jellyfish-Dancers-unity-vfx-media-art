using UnityEngine;

namespace Project.VFX.TVUniverse
{
    public sealed class CrowdGravitySource : MonoBehaviour
    {
        [SerializeField] private bool activeSource = true;
        [SerializeField] private float focusHeight = 1.05f;
        [SerializeField] private float baseStrength = 0.35f;
        [SerializeField] private float radius = 2.0f;
        [SerializeField] private float pulseBoost = 1.75f;
        [SerializeField] private Color idleBottomColor = new Color(0.02f, 0.04f, 0.10f, 0.12f);
        [SerializeField] private Color idleTopColor = new Color(0.14f, 0.42f, 0.58f, 0.22f);
        [SerializeField] private Color pulseBottomColor = new Color(0.05f, 0.12f, 0.20f, 0.20f);
        [SerializeField] private Color pulseTopColor = new Color(0.42f, 0.95f, 1.0f, 0.42f);

        private readonly Renderer[] emptyRenderers = new Renderer[0];
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private float currentPulse;

        public bool IsActive
        {
            get { return activeSource && isActiveAndEnabled; }
            set { activeSource = value; }
        }

        public Vector3 FocusPoint
        {
            get { return transform.position + Vector3.up * focusHeight; }
        }

        public float Radius
        {
            get { return Mathf.Max(0.05f, radius); }
        }

        public float Pulse
        {
            get { return currentPulse; }
        }

        public float EffectiveStrength
        {
            get { return baseStrength * (1f + currentPulse * pulseBoost); }
        }

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnEnable()
        {
            CacheRenderers();
            ApplyVisuals();
        }

        public void SetPulse(float targetPulse, float smoothSpeed, bool visualPulseEnabled)
        {
            float target = visualPulseEnabled ? Mathf.Clamp01(targetPulse) : 0f;
            currentPulse = Mathf.MoveTowards(
                currentPulse,
                target,
                Mathf.Max(0.1f, smoothSpeed) * Time.deltaTime
            );

            ApplyVisuals();
        }

        public void Configure(float strength, float sourceRadius)
        {
            baseStrength = Mathf.Max(0f, strength);
            radius = Mathf.Max(0.05f, sourceRadius);
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null)
            {
                renderers = emptyRenderers;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        private void ApplyVisuals()
        {
            if (renderers == null || propertyBlock == null)
            {
                return;
            }

            Color bottom = Color.Lerp(idleBottomColor, pulseBottomColor, currentPulse);
            Color top = Color.Lerp(idleTopColor, pulseTopColor, currentPulse);
            Color baseColor = Color.Lerp(idleTopColor, pulseTopColor, currentPulse);
            float alpha = Mathf.Lerp(idleTopColor.a, pulseTopColor.a, currentPulse);
            float intensity = Mathf.Lerp(0.55f, 1.35f, currentPulse);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BottomColor", bottom);
                propertyBlock.SetColor("_TopColor", top);
                propertyBlock.SetColor("_BaseColor", baseColor);
                propertyBlock.SetColor("_Color2", top);
                propertyBlock.SetFloat("_Alpha", alpha);
                propertyBlock.SetFloat("_Intensity", intensity);
                target.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
