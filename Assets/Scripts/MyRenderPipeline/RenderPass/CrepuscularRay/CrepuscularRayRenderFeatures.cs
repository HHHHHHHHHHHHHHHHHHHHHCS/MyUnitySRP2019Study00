using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.CrepuscularRay
{
    public class CrepuscularRayRenderFeatures : ScriptableRendererFeature
    {
        public bool enable = true;

        public Shader crepuscularRayShader;

        private Material crepuscularRayMaterial;
        private CrepuscularRayRenderPass crepuscularRayRenderPass;
        
        public override void Create()
        {
            if (crepuscularRayMaterial != null && crepuscularRayMaterial.shader != crepuscularRayShader)
            {
                DestroyImmediate(crepuscularRayMaterial);
            }
            
            if (crepuscularRayShader == null)
            {
                Debug.LogError("Shader is null!");
                return;
            }

            crepuscularRayRenderPass = new CrepuscularRayRenderPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };

            crepuscularRayMaterial = CoreUtils.CreateEngineMaterial(crepuscularRayShader);

            crepuscularRayRenderPass.Init(crepuscularRayMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (enable && renderingData.postProcessingEnabled
                       && crepuscularRayRenderPass != null) //&& Application.isPlaying)
            {
                var raySettings = VolumeManager.instance.stack.GetComponent<CrepuscularRayPostProcess>();
                if (raySettings != null && raySettings.IsActive())
                {
                    crepuscularRayRenderPass.Setup(raySettings);
                    renderer.EnqueuePass(crepuscularRayRenderPass);
                }
            }
        }
    }
}
