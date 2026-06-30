using UnityEngine;
using UnityEngine.UI;
using Mediapipe.SelfieSegmentation;

public class MaskToShaderBinder : MonoBehaviour
{
    [Header("Input References")]
    [SerializeField] private WebcamParticleMeshRenderer webcamRenderer;
    [SerializeField] private SelfieSegmentationResource segmentationResource;

    [Header("Output Settings")]
    [SerializeField] private Material magicMirrorMaterial;
    [SerializeField] private Graphic targetUIElement;
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Texture2D effectTexture;
    [SerializeField, Min(1f)] private float maxInferenceFps = 20f;

    private SelfieSegmentation segmentation;
    private Material runtimeMaterial;
    private float nextInferenceTime;
    private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
    private static readonly int WebcamTexId = Shader.PropertyToID("_WebcamTex");
    private static readonly int EffectTexId = Shader.PropertyToID("_EffectTex");

    public string CurrentModeLabel => webcamRenderer != null && webcamRenderer.IsUsingWebcam
        ? "Magic Mirror Live"
        : "Magic Mirror Standby";

    public string CurrentStatusLabel => webcamRenderer != null && webcamRenderer.IsUsingWebcam
        ? "Segmentation Active"
        : "Effect Background";

    private void Start()
    {
        if (segmentationResource == null)
        {
            Debug.LogError("MaskToShaderBinder: SelfieSegmentationResource is missing!", this);
            enabled = false;
            return;
        }

        if (magicMirrorMaterial == null)
        {
            Debug.LogError("MaskToShaderBinder: Magic Mirror material is missing!", this);
            enabled = false;
            return;
        }

        segmentation = new SelfieSegmentation(segmentationResource);
        runtimeMaterial = new Material(magicMirrorMaterial)
        {
            name = magicMirrorMaterial.name + " (Runtime)"
        };

        if (targetUIElement != null) targetUIElement.material = runtimeMaterial;
        if (targetRenderer != null) targetRenderer.sharedMaterial = runtimeMaterial;
    }

    private void LateUpdate()
    {
        if (webcamRenderer == null || !webcamRenderer.IsUsingWebcam)
        {
            return;
        }

        WebCamTexture webcamTex = webcamRenderer.WebcamTexture;
        if (webcamTex == null || !webcamTex.didUpdateThisFrame || Time.unscaledTime < nextInferenceTime)
        {
            return;
        }

        nextInferenceTime = Time.unscaledTime + (1f / maxInferenceFps);


        segmentation.ProcessImage(webcamTex);


        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetTexture(WebcamTexId, webcamTex);
            runtimeMaterial.SetTexture(MaskTexId, segmentation.texture);

            if (effectTexture != null)
            {
                runtimeMaterial.SetTexture(EffectTexId, effectTexture);
            }
        }
    }

    private void OnDestroy()
    {
        if (segmentation != null)
        {
            segmentation.Dispose();
            segmentation = null;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }
}
