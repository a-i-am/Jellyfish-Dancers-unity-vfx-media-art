using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class VFXParameterDefaults : MonoBehaviour
{
    [SerializeField] private float displacement = 1.5f;
    [SerializeField] private float particleSize = 0.02f;

        private void Awake()
    {
        VisualEffect visualEffect;
        if (!TryGetComponent(out visualEffect))
        {
            Debug.LogError("VFXParameterDefaults requires a VisualEffect component.", this);
            return;
        }

        if (visualEffect.HasFloat("Displacement"))
        {
            visualEffect.SetFloat("Displacement", displacement);
        }

        if (visualEffect.HasFloat("ParticleSize"))
        {
            visualEffect.SetFloat("ParticleSize", particleSize);
        }
    }
}
