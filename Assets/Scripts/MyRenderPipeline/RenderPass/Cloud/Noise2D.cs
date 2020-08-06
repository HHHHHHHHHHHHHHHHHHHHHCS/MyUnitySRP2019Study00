using MyRenderPipeline.Utility;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.Cloud
{
    [ExecuteInEditMode]
    public class Noise2D : MonoBehaviour
    {
        public RenderTexture output;
        public ComputeShader computeShader;
        
        [Delayed]
        public float scale = 64;
        [Delayed]
        public int FBMIteration = 4;
        public bool debug = false;

        private ComputeBuffer PermuteBuffer;
        
        private void Awake()
        {
            RenderNoise();
        }
 
        [EditorButton]
        public void RenderNoise()
        {
            if (output == null)
            {
                return;
            }
            
            if (!output.enableRandomWrite)
            {
                output.Release();
                output.enableRandomWrite = true;
                output.Create();
            }
            computeShader.SetTexture(0, Shader.PropertyToID("OutputTexture"), output);
            computeShader.SetFloat("Scale", scale);
            computeShader.SetVector("OutputSize", new Vector2(output.width, output.height));
            computeShader.SetFloat("Seed", Random.value);
            computeShader.SetInt("Iteration", FBMIteration);
            computeShader.Dispatch(0, output.width / 8, output.height / 8, 1);
        }

        private void OnGUI()
        {
            if(debug)
            {
                GUI.DrawTexture(new Rect(0, 0, 1024, 1024), output, ScaleMode.ScaleToFit, false);
            }
        }
    }
}
