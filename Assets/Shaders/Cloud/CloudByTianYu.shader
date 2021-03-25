Shader "MyRP/Cloud/CloudByTianYu"
{
	Properties
	{
	}
	SubShader
	{


		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct a2v
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float3 normal: NORMAL0;
				float4 uv0: TEXCOORD0;
				float4 uv1: TEXCOORD1;
			};


			v2f Vert(a2v v)
			{
				v2f o = (v2f)0;

				float4 mvpPos = float4(mul((float3x3)UNITY_MATRIX_M, v.vertex.xyz), 0.0);
				float4 wPos = mvpPos + UNITY_MATRIX_M._14_24_34_44;
				o.uv1.xyz = wPos;
				mvpPos = mul(UNITY_MATRIX_VP, wPos);
				o.pos = mvpPos;
				wPos.xyz = mul(v.normal.xyz, (float3x3)GetWorldToObjectMatrix());
				float len = dot(wPos.xyz, wPos.xyz);
				len = 1 / sqrt(len);
				o.normal.xyz = len * wPos.xyz;
				mvpPos.y = mvpPos.y * _ProjectionParams.x;
				wPos.xzw = mvpPos.xwy * 0.5;
				o.uv0.zw = mvpPos.zw;
				o.uv0.xy = wPos.zz + wPos.xw;

				o.uv1.w = UNITY_MATRIX_M;
				
				return o;
			}

			half4 frag(v2f i) : SV_Target
			{
				return i.uv1.w;
			}
			ENDHLSL
		}
	}
}