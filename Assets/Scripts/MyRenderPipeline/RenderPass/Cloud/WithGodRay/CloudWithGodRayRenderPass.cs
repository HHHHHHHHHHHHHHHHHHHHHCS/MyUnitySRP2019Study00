using MyRenderPipeline.RenderPass.Cloud.ImageEffect;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.WithGodRay
{
    public class CloudWithGodRayRenderPass : ScriptableRenderPass
    {
        private Texture3D shapeTexture;
        private Texture3D detailTexture;
        private Texture2D weatherMap;
        private Texture2D blueNoise;
        private Texture2D maskNoise;

        private Material cloudMaterial;
        
        private ContainerVis containerVis;
        private CloudWithGodRayPostProcess cloudSettings;

        
        public void Init(Material cloudMaterial, Texture3D shapeTexture, Texture3D detailTexture, Texture2D weatherMap, Texture2D blueNoise, Texture2D maskNoise)
        {
            this.cloudMaterial = cloudMaterial;
            this.shapeTexture = shapeTexture;
            this.detailTexture = detailTexture;
            this.weatherMap = weatherMap;
            this.blueNoise = blueNoise;
            this.maskNoise = maskNoise;
        }
        
        public void Setup(CloudWithGodRayPostProcess cloudSettings)
        {
            if (containerVis == null)
            {
                containerVis = Object.FindObjectOfType<ContainerVis>();
            }

            this.cloudSettings = cloudSettings;
        }

        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
        }


    }
}
