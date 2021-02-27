Shader "MyRP/Cullings/Cullings_Instance"
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
			
			#pragma multi_compile_instancing
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			struct appdata
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float2 uv: TEXCOORD0;
				float4 hclipPos: SV_POSITION;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			
			// CBUFFER_START(UnityPerMaterial)
			// float4 _MainTex_ST;
			// CBUFFER_END
			
			//UNITY_ACCESS_INSTANCED_PROP
			UNITY_INSTANCING_BUFFER_START(Prop)
			UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
			UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
			UNITY_INSTANCING_BUFFER_END(Prop)
			
			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			// float4 _MainTex_ST;
			
			
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				
				o.hclipPos = TransformObjectToHClip(v.vertex.xyz);
				float4 mainTexST = UNITY_ACCESS_INSTANCED_PROP(Prop, _MainTex_ST);
				o.uv = v.uv * mainTexST.xy + mainTexST.zw;
				
				return o;
			}
			
			half4 frag(v2f i): SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				half4 col = UNITY_ACCESS_INSTANCED_PROP(Prop, _BaseColor) ;
				col *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				return col;
			}
			
			ENDHLSL
			
		}
	}
}
