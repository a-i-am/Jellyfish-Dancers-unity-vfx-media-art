using UnityEngine;
using UnityEngine.UI;
using Mediapipe.SelfieSegmentation;

public class VirtualBackgroundVisuallizer : MonoBehaviour
{
    [SerializeField] WebCamInput webCamInput;
    [SerializeField] RawImage inputImageUI;
    [SerializeField] RawImage compositeImage;
    [SerializeField] SelfieSegmentationResource resource;
    [SerializeField] Shader shader;
    [SerializeField] Texture backGroundTexture;

    SelfieSegmentation segmentation;
    Material material;

    void Start(){
        material = new Material(shader);
        compositeImage.material = material;

        segmentation = new SelfieSegmentation(resource);
    }

    void LateUpdate(){
        inputImageUI.texture = webCamInput.inputImageTexture;


        segmentation.ProcessImage(webCamInput.inputImageTexture);


        compositeImage.texture = segmentation.texture;

        material.SetTexture("_inputImage", webCamInput.inputImageTexture);
        material.SetTexture("_backImage", backGroundTexture);
    }

    void OnApplicationQuit(){
        segmentation.Dispose();
    }
}
