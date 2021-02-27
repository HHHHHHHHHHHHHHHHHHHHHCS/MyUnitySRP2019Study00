Shader "MyRP/Cullings/Cullings_Indirect"
{
	Properties
	{
		_BaseColor ("Base Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_MainTex ("Texture", 2D) = "white" { }
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		
		Pass
		{
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile _ _PROCEDURAL
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			struct appdata
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			struct v2f
			{
				float2 uv: TEXCOORD0;
				float4 hclipPos: SV_POSITION;
			};
			
			
			// CBUFFER_START(UnityPerMaterial)
			// float4 _MainTex_ST;
			// CBUFFER_END
			
			//UNITY_ACCESS_INSTANCED_PROP
			// UNITY_INSTANCING_BUFFER_START(Prop)
			// UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
			// UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
			// UNITY_INSTANCING_BUFFER_END(Prop)
			
			//https://docs.unity3d.com/ScriptReference/Graphics.DrawMeshInstancedIndirect.html
			//UNITY_PROCEDURAL_INSTANCING_ENABLED 在 surface中起效果
			#define _INDIRECT_ENABLE (SHADER_TARGET >= 45)
			#ifdef _INDIRECT_ENABLE
				StructuredBuffer<float4x4> _MatrixsBuffer;
			#endif
			
			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			half4 _BaseColor;
			float4 _MainTex_ST;
			// float4 _MainTex_ST;
			
			
			v2f vert(appdata v, uint instanceID: SV_InstanceID)
			{
				v2f o;
				
				#ifdef _INDIRECT_ENABLE
					UNITY_MATRIX_M = _MatrixsBuffer[instanceID];
					
					UNITY_MATRIX_I_M = UNITY_MATRIX_M;
					UNITY_MATRIX_I_M._14_24_34 *= -1;
					UNITY_MATRIX_I_M._11_22_33 = 1.0f / UNITY_MATRIX_M._11_22_33;
				#endif
				
				o.hclipPos = TransformObjectToHClip(v.vertex.xyz);
				float4 mainTexST = _MainTex_ST;
				o.uv = v.uv * mainTexST.xy + mainTexST.zw;
				
				return o;
			}
			
			half4 frag(v2f i): SV_Target
			{
				half4 col = _BaseColor;
				col *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				return col;
			}
			
			ENDHLSL
			
		}
	}
}
