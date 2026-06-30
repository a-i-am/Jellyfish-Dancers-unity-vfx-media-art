using UnityEngine;

public class FrameScreenReference : MonoBehaviour
{
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private RenderTexture sourceTexture;

    private void Reset()
    {
        screenRenderer = GetComponent<Renderer>();
    }

    private void Awake()
    {
        if (screenRenderer != null && sourceTexture != null)
        {
            screenRenderer.material.mainTexture = sourceTexture;
        }
    }
}