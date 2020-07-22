using System;
using System.Collections.Generic;
using MyRenderPipeline.RenderPass;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline
{
	[CreateAssetMenu(fileName = "MyRenderPipeline", menuName = "MyRP/MyRenderPipeline")]
	public class MyRenderPipelineAsset : RenderPipelineAsset
	{
		[Header("Max Shadow Distance"), SerializeField,]
		private float m_MaxShadowDistance;

		[Header("HDR"), SerializeField,] private bool m_HDR;

		//Delayed : 用于在脚本中使float，int或string变量的属性被延迟。只有按了回车或焦点离开字段才会返回新值。
		[Header("Resolution Scale"), Range(0.25f, 2f), Delayed, SerializeField,]
		private float m_ResolutionScale = 1;

		//Lazy 延迟管理对象  只有在第一次使用的时候才会被加载和初始化
		private Lazy<Shader> m_defaultShader = new Lazy<Shader>(() => Shader.Find("MyRP/ForwardDefault"));
		private Material m_defaultMaterial;

		[SerializeField, HideInInspector]
		private List<MyRenderPassAsset> m_RenderPasses = new List<MyRenderPassAsset>();

		public List<MyRenderPassAsset> RenderPasses => m_RenderPasses;


		public float MaxShadowDistance
		{
			get => m_MaxShadowDistance;
			set => m_MaxShadowDistance = value;
		}

		public bool HDR
		{
			get => m_HDR;
			set => m_HDR = value;
		}

		public float ResolutionScale
		{
			get => m_ResolutionScale;
			set => m_ResolutionScale = value;
		}


		public override Material defaultMaterial
		{
			get
			{
				if (!m_defaultMaterial)
				{
					m_defaultMaterial = new Material(defaultShader);
					//https://www.sardinefish.com/blog/?pid=458
					//我们需要禁用 motion vector pass，但是渲染的时候仍然执行该 pass 的渲染
					m_defaultMaterial.SetShaderPassEnabled("MotionVectors", false);
				}

				return m_defaultMaterial;
			}
		}

		public override Shader defaultShader => m_defaultShader.Value;

		protected override UnityEngine.Rendering.RenderPipeline CreatePipeline()
		{
			return new MyRenderPipeline(this);
		}
	}
}