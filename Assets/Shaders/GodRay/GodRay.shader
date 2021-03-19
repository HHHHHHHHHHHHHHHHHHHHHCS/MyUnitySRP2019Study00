Shader "MyRP/GodRay/GodRay"
{
	Properties
	{
	}

	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

	struct a2v
	{
		// float4 vertex: POSITION;
		// float2 uv: TEXCOORD0;
		uint vertexID: SV_VERTEXID;
	};

	struct v2f
	{
		float4 pos: SV_POSITION;
		float2 uv: TEXCOORD0;
	};

	v2f vert(a2v v)
	{
		v2f o;

		float4 vertex = GetFullScreenTriangleVertexPosition(v.vertexID);
		float2 uv = GetFullScreenTriangleTexCoord(v.vertexID);

		o.pos = vertex; //TransformObjectToHClip(vertex.xyz);
		o.uv = uv;

		return o;
	}
	ENDHLSL

	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			name "Setup"

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile_local _ _EnableCloud

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			float2 _SunUV;
			float4 _GodRayStrength; //xy dir  z strength  w maxDistance
			float3 _GodRayColor;

			#ifdef _EnableCloud
				TEXTURE2D(_CloudRT);
				SAMPLER(sampler_CloudRT);
			#endif


			float4 frag(v2f i): SV_Target
			{
				float depth = SampleSceneDepth(i.uv);
				if (depth > 0.00001)
				{
					return float4(0, 0, 0, 0);
				}

				float alpha = 0;

				#ifdef _EnableCloud
					alpha = _CloudRT.SampleLevel(sampler_CloudRT, i.uv, 0).a;
					if (alpha < 0.01)
					{
						return float4(0, 0, 0, 0);
					}
				#endif

				float2 uv = i.uv - 0.5;

				float2 blurDir = _SunUV - uv;

				blurDir *= _GodRayStrength.xy;

				float t = smoothstep(_GodRayStrength.z, 1, saturate(1 - length(blurDir)));

				float3 godRayColor = _GodRayColor;

				#ifdef _EnableCloud
					godRayColor *= alpha * t * t;
				#endif


				return float4(godRayColor, 1 - alpha);
			}
			ENDHLSL

		}

		Pass
		{
			name "Blur"

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			TEXTURE2D(_GodRayRT);
			SAMPLER(sampler_point_clamp_GodRayRT);

			float2 _SunUV;
			float4 _GodRayStrength; //xy dir  z strength  w maxDistance
			float3 _GodRayColor;


			float4 SampleGodRayBlur(in float2 uv)
			{
				// float alpha = lerp(2, 0, smoothstep(length(_SunUV - uv - 0.5), 0.0, 0.72));

				float4 blurValue =  _GodRayRT.SampleLevel(sampler_point_clamp_GodRayRT, uv, 0);
				// const float4 offset = 8*(float4(_ScreenParams.zw - 1, -_ScreenParams.zw + 1));
				// blurValue += _GodRayRT.SampleLevel(sampler_point_clamp_GodRayRT, uv + offset.xy, 0).rgb * 0.125;
				// blurValue += _GodRayRT.SampleLevel(sampler_point_clamp_GodRayRT, uv + offset.zw, 0).rgb * 0.125;
				// blurValue += _GodRayRT.SampleLevel(sampler_point_clamp_GodRayRT, uv + offset.xw, 0).rgb * 0.125;
				// blurValue += _GodRayRT.SampleLevel(sampler_point_clamp_GodRayRT, uv + offset.zy, 0).rgb * 0.125;

				return blurValue;
			}


			float4 GodRayBlur(float2 iUV)
			{
				float2 uv = iUV - 0.5;
				float2 blurDir = _SunUV - uv;

				float blurDistance = length(blurDir);
				blurDir /= blurDistance;
				blurDistance = min(blurDistance, _GodRayStrength.w);
				blurDir *= blurDistance;

				float4 blurColor = 0;

				const int loopCount = 16;

				float2 uvStep = blurDir / loopCount;

				for (int i = 0; i < loopCount; i ++)
				{
					blurColor += SampleGodRayBlur(iUV + uvStep * i);
				}

				blurColor /= loopCount;

				//0.71 = sqrt(0.5*0.5 + 0.5*0.5)
				float alpha = 0.71 * _GodRayStrength.w - blurDistance;
				alpha *= 1 - blurColor.a;

				return float4(blurColor.rgb, alpha);
			}


			float4 frag(v2f i): SV_Target
			{
				return GodRayBlur(i.uv);
			}
			ENDHLSL

		}

		Pass
		{
			name "Composite"

			Blend One SrcAlpha

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile_local _ _EnableCloud


			TEXTURE2D(_GodRayBlurRT);
			SAMPLER(sampler_GodRayBlurRT);


			#ifdef _EnableCloud
				TEXTURE2D(_CloudRT);
				SAMPLER(sampler_CloudRT);
			#endif


			float4 frag(v2f i): SV_Target
			{
				float4 color = _GodRayBlurRT.SampleLevel(sampler_GodRayBlurRT, i.uv, 0);
				color.rgb *= color.a;
				color.a = 1 - color.a;
				#ifdef _EnableCloud
					float4 cloudColor = _CloudRT.SampleLevel(sampler_CloudRT, i.uv, 0);
					color.rgb += cloudColor.rgb;
					color.a = cloudColor.a;
				#endif
				return color;
			}
			ENDHLSL

		}
	}
}