Shader "MyRP/HiZ/HiZCopyDepth"
{
	Properties { }
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "" }
		
		Pass
		{
			Name "CopyDepth"
			ZTest Always ZWrite On
			Cull Off
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			//没有这个 会导致少宏定义
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			struct a2v
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			struct v2f
			{
				float4 pos: SV_POSITION;
				float2 uv: TEXCOORD0;
			};
			
			Texture2D _CameraDepthTexture;
			SamplerState sampler_CameraDepthTexture;
			
			v2f vert(a2v input)
			{
				v2f o;
				o.pos = input.vertex;
				o.uv = input.uv;
				#if UNITY_UV_STARTS_AT_TOP
					o.uv.y = 1 - o.uv.y;
				#endif
				return o;
			}
			
			
			float frag(v2f input): SV_Target
			{
				return _CameraDepthTexture.Sample(sampler_CameraDepthTexture, input.uv).r;
			}
			ENDHLSL
			
		}
	}
}
