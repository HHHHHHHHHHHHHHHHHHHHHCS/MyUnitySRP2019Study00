using System.Collections.Generic;
using MyRenderPipeline.RenderPass.Shadow;
using MyRenderPipeline.Shadow;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline
{
	public struct MyRenderingData
	{
		public Camera camera;
		public CullingResults cullResults;
		public NativeArray<VisibleLight> lights;
		public RenderTargetIdentifier colorTarget;
		public RenderTargetIdentifier depthTarget;
		public RenderTextureFormat colorBufferFormat;
		public Dictionary<Light, MyShadowMapData> shadowMapData;
		public RenderTargetIdentifier defaultShadowMap;
		public RenderTargetIdentifier velocityBuffer;
		public int frameID;
		public bool discardFrameBuffer;
		public Vector2 projectionJitter;
		public Vector2 nextProjectionJitter;
		public Matrix4x4 jitteredProjectionMatrix;
		public Matrix4x4 projectionMatrix;
		public Matrix4x4 viewMatrix;
		public float resolutionScale;

		public int resolutionX => Mathf.FloorToInt(camera.pixelWidth * resolutionScale);
		public int resolutionY => Mathf.FloorToInt(camera.pixelHeight * resolutionScale);

	}
}