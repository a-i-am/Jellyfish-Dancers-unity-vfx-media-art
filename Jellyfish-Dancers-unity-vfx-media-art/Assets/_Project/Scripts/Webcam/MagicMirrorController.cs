using UnityEngine;
using Mediapipe.SelfieSegmentation;

public class MagicMirrorController : MonoBehaviour
{
    [Header("AI Resource")]
    public SelfieSegmentationResource resource;

    [Header("Display Target")]
    public Renderer targetRenderer;
    public Material magicMirrorMaterial;

    [Header("Webcam Source")]
    public WebcamParticleMeshRenderer webcamRenderer;

    private SelfieSegmentation segmentation;
    private Material runtimeMaterial;
    private bool fallbackApplied;

    private static readonly int MaskTexId =
        Shader.PropertyToID("_MaskTex");
    private static readonly int MirrorXId =
        Shader.PropertyToID("_MirrorX");

    private void Start()
    {
        if (resource == null)
        {
            Debug.LogError(
                "MagicMirrorController: SelfieSegmentationResource가 없습니다!",
                this
            );
            enabled = false;
            return;
        }

        if (targetRenderer == null || magicMirrorMaterial == null)
        {
            Debug.LogError(
                "MagicMirrorController: Renderer 또는 Material이 없습니다!",
                this
            );
            enabled = false;
            return;
        }

        segmentation = new SelfieSegmentation(resource);



        targetRenderer.material = magicMirrorMaterial;
        runtimeMaterial = targetRenderer.material;

        ShowFallback();
    }

    private void LateUpdate()
    {
        if (webcamRenderer == null ||
            !webcamRenderer.IsUsingWebcam)
        {
            ShowFallback();
            return;
        }

        Texture webcamTexture = webcamRenderer.WebcamTexture;

        if (webcamTexture == null ||
            segmentation == null ||
            runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(
            MirrorXId,
            webcamRenderer.MirrorX ? 1f : 0f
        );

        runtimeMaterial.SetTexture("_WebcamTex", webcamTexture);

        fallbackApplied = false;

        segmentation.ProcessImage(webcamTexture);
        runtimeMaterial.SetTexture(
            MaskTexId,
            segmentation.texture
        );
    }

    public void ShowFallback()
    {
        if (fallbackApplied || runtimeMaterial == null)
        {
            return;
        }


        runtimeMaterial.SetTexture(
            MaskTexId,
            Texture2D.whiteTexture
        );

        fallbackApplied = true;
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
