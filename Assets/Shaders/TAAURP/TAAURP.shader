Shader "MyRP/TAAURP/TAAURP"
{
	Properties
	{
	}
	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

	struct a2v
	{
		uint vertexID: SV_VERTEXID;
	};

	struct v2f
	{
		float4 pos: SV_POSITION;
		float2 uv: TEXCOORD0;
	};

	TEXTURE2D(_SrcTex);
	SAMPLER(sampler_LinearClamp);

	v2f Vert(a2v v)
	{
		v2f o = (v2f)0;
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		o.uv = GetFullScreenTriangleTexCoord(v.vertexID);
		return o;
	}

	inline half4 SampleColor(float2 uv)
	{
		return SAMPLE_TEXTURE2D(_SrcTex, sampler_LinearClamp, uv);
	}
	ENDHLSL

	SubShader
	{
		Blend One Zero
		ZTest Always
		ZWrite Off
		Cull Off

		//0. TAA
		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#pragma multi_compile _TAA_LOW _TAA_MEDIUM _TAA_HIGH

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


			float4 _SrcTex_TexelSize;

			float4 _TAA_Params;
			TEXTURE2D(_TAA_PreTexture);
			float4x4 _TAA_PrevViewProj, _TAA_Current_I_V_Jittered, _TAA_Current_I_P_Jittered;

			//其实 有时候对颜色做clamp 会用 ycocg
			half4 MinMaxColor(float2 uv, out half4 color_min, out half4 color_max, out half4 color_avg)
			{
				//left right top bottom
				const float4 offset = float4(+_SrcTex_TexelSize.xy, -_SrcTex_TexelSize.xy);
				half4 cc = SampleColor(uv);

				#if _TAA_LOW

				half4 crt = SampleColor(uv + offset.xy);
				half4 clb = SampleColor(uv + offset.zw);

				color_min = min(cc, min(crt, clb));
				color_max = max(cc, max(crt, clb));
				color_avg = 0.333333333 * (cc + crt + clb);

				#elif _TAA_MEDIUM
					
					half4 clt = SampleColor(_MainTex, uv + offset.zy);
					half4 clb = SampleColor(_MainTex, uv + offset.zw);
					half4 crt = SampleColor(_MainTex, uv + offset.xy);
					half4 crb = SampleColor(_MainTex, uv + offset.xw);
					
					
					color_min = min(cc, min(crt, min(clb, min(crb, clt))));
					color_max = max(cc, max(crt, max(clb, max(crb, clt))));
					color_avg = 0.2 * (cc + crt + clb + crb + clt);
					
				#else //if _TAA_HIGH
					
					half4 cl = SampleColor(uv + float2(offset.z, 0));
					half4 cr = SampleColor(uv + float2(offset.x, 0));
					half4 ct = SampleColor(uv + float2(0, offset.y));
					half4 cb = SampleColor(uv + float2(0, offset.w));
					half4 clt = SampleColor(uv + float2(offset.zy));
					half4 clb = SampleColor(uv + float2(offset.zw));
					half4 crt = SampleColor(uv + float2(offset.xy));
					half4 crb = SampleColor(uv + float2(offset.xw));
					
					color_min = min(cc, min(cl, min(cr, min(ct, min(cb, min(clt, min(clb, min(crt, crb))))))));
					color_max = max(cc, max(cl, max(cr, max(ct, max(cb, max(clt, max(clb, max(crt, crb))))))));
					color_avg = 0.1111111 * (cc + cl + cr + ct + cb + clt + clb + crt + crb);
					
				#endif

				return cc;
			}

			float2 HistoryPosition(float2 uv)
			{
				float depth = SampleSceneDepth(uv);

				#if UNITY_REVERSED_Z
				depth = 1.0 - depth;
				#endif
				depth = 2.0 * depth - 1.0;

				float3 viewPos = ComputeViewSpacePosition(uv, depth, _TAA_Current_I_P_Jittered);
				float4 worldPos = float4(mul(_TAA_Current_I_V_Jittered, float4(viewPos, 1.0)).xyz, 1.0);

				float4 historyNDC = mul(_TAA_PrevViewProj, worldPos);
				historyNDC /= historyNDC.w;
				historyNDC.xy = historyNDC.xy * 0.5f + 0.5f;


				return historyNDC.xy;
			}

			half4 ClipAABB(half3 color_min, half3 color_max, half4 color_avg, half4 color_prev)
			{
				half3 p_clip = 0.5 * (color_max + color_min);
				half3 e_clip = 0.5 * (color_max - color_min) + FLT_EPS;
				half4 v_clip = color_prev - float4(p_clip, color_avg.w);
				float3 v_unit = v_clip.xyz / e_clip;
				float3 a_unit = abs(v_unit);
				float ma_unit = max(a_unit.x, max(a_unit.y, a_unit.z)) + FLT_EPS;

				if (ma_unit > 1.0)
					return float4(p_clip, color_avg.w) + v_clip / ma_unit;
				else
					return color_prev;
			}

			//todo:taa抖动
			half4 Frag(v2f i): SV_Target
			{
				float2 uv = i.uv;
				// float2 jittered_uv = jittered_uv + _TAA_Params.xy;
				half4 color_min, color_max, color_avg;
				half4 color = MinMaxColor(uv, color_min, color_max, color_avg);
				float2 previousTC = HistoryPosition(uv);
				float4 prev_color = SAMPLE_TEXTURE2D_X(_TAA_PreTexture, sampler_LinearClamp, previousTC);
				prev_color = ClipAABB(color_min, color_max, color_avg, prev_color);
				float4 result_color = lerp(color, prev_color, _TAA_Params.z);
				return result_color;
			}
			ENDHLSL

		}

		//1.Blit
		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			half4 Frag(v2f i): SV_Target
			{
				return SampleColor(i.uv);
			}
			ENDHLSL

		}
	}
}