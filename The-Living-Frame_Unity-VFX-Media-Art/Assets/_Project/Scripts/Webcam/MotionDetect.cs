using UnityEngine;
using UnityEngine.VFX;

public class MotionDetect : MonoBehaviour
{
    [SerializeField] private VisualEffect vfx;
    [SerializeField] private Material diffMaterial;
    [SerializeField] private int width = 1280;
    [SerializeField] private int height = 720;
    [SerializeField] private int fps = 30;
    [SerializeField] private int motionWidth = 640;
    [SerializeField] private int motionHeight = 360;

    private WebCamTexture webcam;
    private RenderTexture prevFrame;
    private RenderTexture motionMap;

    public RenderTexture MotionMap => motionMap;

    private void Reset()
    {
        vfx = GetComponent<VisualEffect>();
    }

    private void Start()
    {
        if (vfx == null)
        {
            vfx = GetComponent<VisualEffect>();
        }

        if (vfx == null)
        {
            Debug.LogError("MotionDetect requires a VisualEffect component.", this);
            return;
        }

        if (diffMaterial == null)
        {
            Debug.LogError("MotionDetect requires a frame difference material.", this);
            return;
        }

        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("No webcam device found.", this);
            return;
        }

        webcam = new WebCamTexture(WebCamTexture.devices[0].name, width, height, fps);
        webcam.Play();

        prevFrame = new RenderTexture(motionWidth, motionHeight, 0, RenderTextureFormat.ARGB32);
        prevFrame.name = "RT_RuntimePrevFrame";
        motionMap = new RenderTexture(motionWidth, motionHeight, 0, RenderTextureFormat.ARGB32);
        motionMap.name = "RT_RuntimeMotionMap";

        vfx.SetTexture("WebcamTex", webcam);
        vfx.SetTexture("MotionTex", motionMap);
    }

    private void Update()
    {
        if (webcam == null || !webcam.didUpdateThisFrame || diffMaterial == null)
        {
            return;
        }

        diffMaterial.SetTexture("_PrevTex", prevFrame);
        Graphics.Blit(webcam, motionMap, diffMaterial);
        Graphics.Blit(webcam, prevFrame);
    }

    private void OnDestroy()
    {
        if (webcam != null)
        {
            webcam.Stop();
        }

        if (prevFrame != null)
        {
            prevFrame.Release();
        }

        if (motionMap != null)
        {
            motionMap.Release();
        }
    }
}
