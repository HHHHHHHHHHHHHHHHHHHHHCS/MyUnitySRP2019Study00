using System;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.GeometryGrass
{
    public class GeometryGrass : MonoBehaviour
    {
        private void Update()
        {
            Shader.SetGlobalVector("_PositionMoving", transform.position);
        }
    }
}
