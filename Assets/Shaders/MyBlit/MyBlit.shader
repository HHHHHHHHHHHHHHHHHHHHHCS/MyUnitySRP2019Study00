Shader "MyRP/MyBlit/MyBlit"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

	struct a2v
	{
		float4 positionOS:POSITION;
		float2 texcoord:TEXCOORD0;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};

	struct v2f
	{
		float4 positionCS:SV_POSITION;
		float2 uv:TEXCOORD0;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};


	v2f vert(a2v input)
	{
		v2f output;
		UNITY_SETUP_INSTANCE_ID(input);
		UNITY_TRANSFER_INSTANCE_ID(input, output);

		output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
		output.uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
		return output;
	}


	half4 DownsampleBox4Tap(TEXTURE2D_PARAM(tex, samplerTex), float2 uv, float2 texelSize, float amount)
	{
		float4 d = texelSize.xyxy * float4(-amount, -amount, amount, amount);

		half4 s;
		s = (SAMPLE_TEXTURE2D(tex, samplerTex, uv + d.xy));
		s += (SAMPLE_TEXTURE2D(tex, samplerTex, uv + d.zy));
		s += (SAMPLE_TEXTURE2D(tex, samplerTex, uv + d.xw));
		s += (SAMPLE_TEXTURE2D(tex, samplerTex, uv + d.zw));

		return s * 0.25h;
	}
	ENDHLSL

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
		}
		LOD 100

		// 0 - Downsample - Box filtering
		Pass
		{
			Name "Default"
			ZTest Always
			ZWrite Off

			HLSLPROGRAM
			// 跳过gles2.0
			#pragma prefer_hlslcc gles
			#pragma vertex vert
			#pragma fragment frag

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			float4 _MainTex_TexelSize;

			float _SampleOffset;

			half4 frag(v2f input) : SV_Target
			{
				half4 col = DownsampleBox4Tap(
					TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.uv, _MainTex_TexelSize.xy, _SampleOffset);
				return half4(col.rgb, 1);
			}
			ENDHLSL
		}
	}
}