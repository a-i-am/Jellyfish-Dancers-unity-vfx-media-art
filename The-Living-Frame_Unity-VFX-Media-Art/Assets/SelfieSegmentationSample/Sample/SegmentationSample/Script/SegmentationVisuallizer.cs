using UnityEngine;
using UnityEngine.UI;
using Mediapipe.SelfieSegmentation;

public class SegmentationVisuallizer : MonoBehaviour
{
    [SerializeField] WebCamInput webCamInput;
    [SerializeField] RawImage inputImageUI;
    [SerializeField] RawImage segmentationImage;
    [SerializeField] SelfieSegmentationResource resource;

    SelfieSegmentation segmentation;

    void Start(){
        segmentation = new SelfieSegmentation(resource);
    }

    void LateUpdate(){
        inputImageUI.texture = webCamInput.inputImageTexture;

        segmentation.ProcessImage(webCamInput.inputImageTexture);

        segmentationImage.texture = segmentation.texture;
    }

    void OnApplicationQuit(){
        segmentation.Dispose();
    }
}
