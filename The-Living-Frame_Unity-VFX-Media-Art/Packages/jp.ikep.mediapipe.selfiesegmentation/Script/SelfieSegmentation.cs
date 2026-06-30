using UnityEngine;
using Unity.Barracuda;

namespace Mediapipe.SelfieSegmentation{
    public class SelfieSegmentation: System.IDisposable
    {

        public RenderTexture texture;

        #region constant number

        const int IMAGE_SIZE = 256;

        const int IN_CH = 3;

        const int OUT_CH = 1;
        #endregion

        #region private variables
        Model model;
        IWorker woker;
        ComputeShader preProcessCS;
        ComputeBuffer networkInputBuffer;
        #endregion

        #region public methods
        public SelfieSegmentation(SelfieSegmentationResource resource){
            preProcessCS = resource.preProcessCS;

            networkInputBuffer = new ComputeBuffer(IMAGE_SIZE * IMAGE_SIZE * IN_CH, sizeof(float));


            texture = new RenderTexture(IMAGE_SIZE, IMAGE_SIZE, 0, RenderTextureFormat.ARGB32);


            model = ModelLoader.Load(resource.model);
            woker = model.CreateWorker();
        }

        public void ProcessImage(Texture inputTexture){

            preProcessCS.SetTexture(0, "_inputTexture", inputTexture);
            preProcessCS.SetBuffer(0, "_output", networkInputBuffer);
            preProcessCS.Dispatch(0, IMAGE_SIZE / 8, IMAGE_SIZE / 8, 1);


            var inputTensor = new Tensor(1, IMAGE_SIZE, IMAGE_SIZE, IN_CH, networkInputBuffer);
            woker.Execute(inputTensor);
            inputTensor.Dispose();


            var segTemp = CopyOutputToTempRT("activation_10", IMAGE_SIZE, IMAGE_SIZE, OUT_CH);

            if(texture.width != inputTexture.width || texture.height != inputTexture.height){

                texture?.Release();
                texture = new RenderTexture(inputTexture.width, inputTexture.height, 0, RenderTextureFormat.ARGB32);
            }


            Graphics.Blit(segTemp, texture);

            RenderTexture.ReleaseTemporary(segTemp);
        }

        public void Dispose(){
            networkInputBuffer?.Dispose();
            woker?.Dispose();
            texture?.Release();
        }
        #endregion

        RenderTexture CopyOutputToTempRT(string name, int w, int h, int ch)
        {
            var rtFormat = RenderTextureFormat.ARGB32;
            var shape = new TensorShape(1, h, w, ch);
            var rt = RenderTexture.GetTemporary(w, h, 0, rtFormat);
            var tensor = woker.PeekOutput(name).Reshape(shape);
            tensor.ToRenderTexture(rt);
            tensor.Dispose();
            return rt;
        }
    }
}
