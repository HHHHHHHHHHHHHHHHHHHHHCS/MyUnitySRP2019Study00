Shader "MyRP/HiZ/Instance"
{
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		_MainTex ("Albedo (RGB)", 2D) = "white" { }
		_BumpMap ("Bumpmap", 2D) = "bump" { }
	}
	
	SubShader
	{
		Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
		
		Pass
		{
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }
			
			Cull Back
			ZWrite On
			ZTest Less
			
			
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile_instancing
			#pragma multi_compile _ INDIRECT_DEBUG_LOD
			
			//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "ShaderInclude_HiZ_Instance.hlsl"
			
			sampler2D _MainTex;
			sampler2D _BumpMap;
			float4 _Color;
			
			float4 _LightColor;
			
			struct a2v
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
				float4 normal: NORMAL;
				float4 tangent: TANGENT;
				//UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 pos: SV_POSITION;
				float2 uv: TEXCOORD0;
				float4 screenPos: TEXCOORD1;
				float3 worldPos: TEXCOORD2;
				float3 t2w0: TEXCOORD3;
				float3 t2w1: TEXCOORD4;
				float3 t2w2: TEXCOORD5;
				float4 color: TEXCOORD6;
			};
			
			v2f vert(a2v input, uint instanceID: SV_INSTANCEID)
			{
				//UNITY_SETUP_INSTANCE_ID(input);
				uint index = SetupMatrix(instanceID);
				v2f o;
				float4 wPos = mul(UNITY_MATRIX_M, input.vertex);
				o.worldPos = wPos.xyz;
				o.pos = mul(UNITY_MATRIX_VP, wPos);
				o.uv = input.uv;
				
				float3 worldNormal = normalize(mul(input.normal.xyz, (float3x3)UNITY_MATRIX_M));
				float3 worldTangent = normalize(mul(input.tangent.xyz, (float3x3)UNITY_MATRIX_M));
				float3 worldBinormal = cross(worldNormal, worldTangent) * input.tangent.w;
				o.t2w0 = float3(worldTangent.x, worldBinormal.x, worldNormal.x);
				o.t2w1 = float3(worldTangent.y, worldBinormal.y, worldNormal.y);
				o.t2w2 = float3(worldTangent.z, worldBinormal.z, worldNormal.z);
				o.screenPos = ComputeScreenPos(o.pos);
				o.color = index / 2048.0;
				return o;
			}
			
			float4 frag(v2f i): SV_TARGET
			{
				float3 col = i.color.rgb;
				#if defined(INDIRECT_DEBUG_LOD)
					uint offset = _ArgsOffset % 15;
					col = (offset == 14)?float3(0.4, 0.7, 1.0): ((offset == 9)?float3(0.0, 1.0, 0.0): float3(1.0, 0.0, 0.0));
				#endif
				return float4(col, 1.0);
			}
			
			ENDHLSL
			
		}
		
		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }
			ZTest LEqual
			ZWrite On
			Cull Back
			
			HLSLPROGRAM
			
			#pragma vertex vert_depth
			#pragma fragment frag_depth
			
			#pragma multi_compile_instancing
			
			#include "ShaderInclude_HiZ_Instance.hlsl"
			
			
			ENDHLSL
			
		}
		
		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			ZTest LEqual
			ZWrite On
			Cull Back
			
			HLSLPROGRAM
			
			#pragma vertex vert_depth
			#pragma fragment frag_depth
			
			#pragma multi_compile_instancing
			
			#include "ShaderInclude_HiZ_Instance.hlsl"
			
			ENDHLSL
			
		}
	}
	
	/*
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200
		Cull back
		
		CGPROGRAM
		
		#pragma surface surf Lambert addshadow halfasview noambient noshadow
		
		
		#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
			#pragma instancing_options procedural:setup
			
			
			
			void setup()
			{
				#if defined(SHADER_API_METAL)
					uint index = unity_InstanceID;
				#else
					uint index = unity_InstanceID + _ArgsBuffer[_ArgsOffset];
				#endif
				
				Indirect2x2Matrix rows01 = _InstancesDrawMatrixRows01[index];
				Indirect2x2Matrix rows23 = _InstancesDrawMatrixRows23[index];
				Indirect2x2Matrix rows45 = _InstancesDrawMatrixRows45[index];
				
				unity_ObjectToWorld = float4x4(rows01.row0, rows01.row1, rows23.row0, float4(0, 0, 0, 1));
				unity_WorldToObject = float4x4(rows23.row1, rows45.row0, rows45.row1, float4(0, 0, 0, 1));
			}
		#endif
		
		struct Input
		{
			float2 uv_MainTex;
		};
		
		
		
		// void surf(Input IN, inout SurfaceOutputStandard o)
		void surf(Input IN, inout SurfaceOutput o)
		{
			float3 color = _Color;
			#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
				#if defined(INDIRECT_DEBUG_LOD)
					uint off = _ArgsOffset % 15;
					color = (off == 14) ? float3(0.4, 0.7, 1.0): ((off == 9) ? float3(0.0, 1.0, 0.0): float3(1.0, 0.0, 0.0));
				#endif
			#endif
			o.Albedo = tex2D(_MainTex, IN.uv_MainTex) * color;
			o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
			o.Alpha = 1.0;
		}
		ENDCG
		
	}
	*/
}
