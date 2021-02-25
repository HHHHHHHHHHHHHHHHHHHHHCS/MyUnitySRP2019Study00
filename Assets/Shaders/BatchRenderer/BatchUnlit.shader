Shader "MyRP/BatchRenderer/BatchUnlit"
{
	Properties
	{
		//  _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
	}
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
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			
			// CBUFFER_START(UnityPerMaterial)
			// // float4 _BaseColor;
			// CBUFFER_END
			
			// // SRP Batcher 与 instance 不兼容
			// // 需要关闭SRP Batcher
			// UNITY_INSTANCING_BUFFER_START(Props)
			// // UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
			// UNITY_INSTANCING_BUFFER_END(Props)
			
			struct a2v
			{
				float4 positionOS: POSITION;
				float2 uv: TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 hclipPos: SV_Position;
				float2 uv: TEXCOORD0;
				float4 color: TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			
			v2f vert(a2v v)
			{
				v2f o = (v2f)0;
				
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				
				float4x4 worldMatrix = GetObjectToWorldMatrix();
				
				o.color = worldMatrix._41_42_43_44;
				
				worldMatrix._41_42_43_44 = float4(0, 0, 0, 1);
				
				float3 wPos = mul(worldMatrix, v.positionOS).xyz;
				
				
				// float3 wPos = TransformObjectToWorld(v.positionOS.xyz).xyz;
				float4 hclipPos = TransformWorldToHClip(wPos);
				
				o.hclipPos = hclipPos;
				o.uv = v.uv;
				
				return o;
			}
			
			half4 frag(v2f IN): SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
				
				float2 uv = IN.uv;
				// half4 col = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
				
				// #ifdef UNITY_INSTANCING_ENABLED
				// 	col = unity_InstanceID / 50.0;
				// #endif
				
				half4 col = IN.color;
				
				
				return col;
			}
			
			ENDHLSL
			
		}
		
		Pass
		{
			Tags { "LightMode" = "DepthOnly" }
			
			ZWrite On
			ColorMask 0
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			// -------------------------------------
			
			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			struct a2v
			{
				float4 positionOS: POSITION;
				float2 uv: TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 hclipPos: SV_Position;
				float2 uv: TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			
			v2f vert(a2v v)
			{
				v2f o = (v2f)0;
				
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				float4x4 worldMatrix = GetObjectToWorldMatrix();
				
				worldMatrix._41_42_43_44 = float4(0, 0, 0, 1);
				
				float3 wPos = mul(worldMatrix, v.positionOS).xyz;
				
				// float3 wPos = TransformObjectToWorld(v.positionOS.xyz).xyz;
				float4 hclipPos = TransformWorldToHClip(wPos);
				
				o.hclipPos = hclipPos;
				o.uv = v.uv;
				
				return o;
			}
			
			half4 frag(v2f IN): SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
				
				return 0;
			}
			
			
			ENDHLSL
			
		}
	}
}