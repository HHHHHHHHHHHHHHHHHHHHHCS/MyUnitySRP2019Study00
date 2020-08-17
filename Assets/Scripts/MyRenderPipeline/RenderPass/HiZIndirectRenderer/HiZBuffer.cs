using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer
{
	[RequireComponent(typeof(Camera))]
	public class HiZBuffer:MonoBehaviour
	{
		// Enums
		private enum Pass
		{
			Blit,
			Reduce
		}
		
		#region Variables
		
		// Consts
		private const int MAXIMUM_BUFFER_SIZE = 1024;

		[Header("References")]
		public RenderTexture topDownView = null;
		public HiZIndirectRenderer m_indirectRenderer;
		public Camera mainCamera = null;
		public Light light = null;
		public Shader generateBufferShader = null;
		public Shader debugShader = null;

		// Private 
		private int m_LODCount = 0;
		private int[] m_Temporaries = null;
		private CameraEvent m_CameraEvent = CameraEvent.AfterReflections;
		private Vector2 m_textureSize;
		private Material m_generateBufferMaterial = null;
		private Material m_debugMaterial = null;
		private RenderTexture m_HiZDepthTexture = null;
		private CommandBuffer m_CommandBuffer = null;
		private CameraEvent m_lastCameraEvent = CameraEvent.AfterReflections;
		private RenderTexture m_ShadowmapCopy;
		private RenderTargetIdentifier m_shadowmap;
		private CommandBuffer m_lightShadowCommandBuffer;
    
		// Public Properties
		public int DebugLodLevel { get; set; }
		public Vector2 TextureSize { get { return m_textureSize; } }
		public RenderTexture Texture { get { return m_HiZDepthTexture; } }



		#endregion
	}
}