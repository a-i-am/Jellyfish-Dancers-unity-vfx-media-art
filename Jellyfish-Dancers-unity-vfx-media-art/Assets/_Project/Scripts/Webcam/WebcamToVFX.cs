using UnityEngine;
using UnityEngine.VFX;

public class WebcamToVFX : MonoBehaviour
{
    [SerializeField] private VisualEffect vfx;
    [SerializeField] private bool useRequestedFormat;
    [SerializeField] private int width = 1280;
    [SerializeField] private int height = 720;
    [SerializeField] private int fps = 30;

    private WebCamTexture webcam;
    private bool sizeSent;

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
            Debug.LogError("WebcamToVFX requires a VisualEffect component.", this);
            return;
        }

        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogWarning("No webcam device found. WebcamToVFX will stay idle.", this);
            return;
        }

        webcam = CreateWebcam();
        webcam.Play();

        if (vfx.HasTexture("WebcamTex"))
        {
            vfx.SetTexture("WebcamTex", webcam);
        }
    }

    private void Update()
    {
        if (webcam == null || !webcam.didUpdateThisFrame)
        {
            return;
        }

        if (!sizeSent && webcam.width > 16 && webcam.height > 16)
        {
            if (vfx.HasVector2("TexSize"))
            {
                vfx.SetVector2("TexSize", new Vector2(webcam.width, webcam.height));
            }

            sizeSent = true;
        }
    }

    private void OnDestroy()
    {
        if (webcam != null)
        {
            webcam.Stop();
        }
    }

    private WebCamTexture CreateWebcam()
    {
        var deviceName = WebCamTexture.devices[0].name;
        if (!useRequestedFormat)
        {
            return new WebCamTexture(deviceName);
        }

        var texture = new WebCamTexture(deviceName);
        texture.requestedWidth = width;
        texture.requestedHeight = height;
        texture.requestedFPS = fps;
        return texture;
    }
}
