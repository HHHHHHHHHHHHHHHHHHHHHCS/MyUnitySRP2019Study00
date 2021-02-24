Shader "MyRP/Grass/BatchUnlit"
{
	Properties { }
	SubShader
	{
		Tags { "RenderType" = "Opaque" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
		
		Blend One Zero
		ZWrite On
		Cull Back
		
		Pass
		{
			Name "Unlit"
			Tags { "LightMode" = "UniversalForward" }
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile_instancing
			
			#include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl"
			
			// SRP Batcher 与 instance 不兼容 , instance>bathcer
			// CBUFFER_START(UnityPerMaterial)
			// float4 _Color;
			// CBUFFER_END
			
			UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
			UNITY_INSTANCING_BUFFER_END(Props)
			
			struct a2v
			{
				float4 positionOS: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			struct v2f
			{
				float2 uv: TEXCOORD0;
			};
			
			ENDHLSL
			
		}
	}
}