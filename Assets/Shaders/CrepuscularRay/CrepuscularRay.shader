Shader "MyRP/CrepuscularRay/CrepuscularRay"
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
		uint vertexID:SV_VERTEXID;
	};

	struct v2f
	{
		float4 pos: SV_POSITION;
		float2 uv: TEXCOORD0;
		// float3 viewVector: TEXCOORD1;
	};


	v2f DefaultVert(a2v v)
	{
		v2f o;

		float4 vertex = GetFullScreenTriangleVertexPosition(v.vertexID);
		float2 uv = GetFullScreenTriangleTexCoord(v.vertexID);

		o.pos = vertex; //TransformObjectToHClip(vertex.xyz);
		o.uv = uv;

		// Camera space matches OpenGL convention where cam forward is -z. In unity forward is positive z.
		// (https://docs.unity3d.com/ScriptReference/Camera-cameraToWorldMatrix.html)
		// float3 viewVector = mul(unity_CameraInvProjection, float4(uv * 2 - 1, 0, -1)).xyz;
		// o.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;
		return o;
	}
	ENDHLSL
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Blend One One
			
			HLSLPROGRAM
			#pragma vertex DefaultVert
			#pragma fragment FragRay

			TEXTURE2D(_DownsampleTex);
			SAMPLER(sampler_DownsampleTex);

			float2 _LightViewPos;
			float _RayRange;
			float _OffsetUV;
			int _QualityStep;
			float _LightThreshold;
			float _RayPower;
			float3 _LightColor;
			float _RayIntensity;


			float2 Random(float2 p)
			{
				//给UV 一个噪音
				float a = dot(p, float2(114.5, 141.9));
				float b = dot(p, float2(364.3, 648.8));
				float2 c = sin(float2(a, b)) * 643.1;
				return frac(c);
			}

			float4 FragRay(v2f i):SV_Target
			{
				float2 screenUV = _LightViewPos.xy - i.uv; //模糊向量
				float lightViewDir = length(screenUV);
				float distanceControl = saturate(_RayRange - lightViewDir);

				float3 colorFinal = float3(0, 0, 0);
				float2 originalUV = i.uv;

				float2 scrUV = screenUV * _OffsetUV;
				float2 jitter = Random(i.uv);

				for (int ray = 0; ray < _QualityStep; ray++)
				{
					float3 addcolor = SAMPLE_TEXTURE2D(_DownsampleTex, sampler_DownsampleTex,
					                                   originalUV + jitter * 0.005f).rgb;
					float3 thresholdColor = saturate(addcolor - _LightThreshold) * distanceControl;
					float luminanceColor = dot(thresholdColor, float3(0.3f, 0.59f, 0.11f));
					luminanceColor = pow(luminanceColor, _RayPower);
					colorFinal += luminanceColor;
					originalUV += scrUV;
				}
				colorFinal = (colorFinal / _QualityStep) * _LightColor.rgb * _RayIntensity;

				return float4(colorFinal, 1);
			}
			ENDHLSL
		}
		Pass
		{
			HLSLPROGRAM
			#pragma vertex DefaultVert
			#pragma fragment FragDownsample4

			// #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
			TEXTURE2D_X(_CameraColorTexture);
			SAMPLER(sampler_CameraColorTexture);

			float _CameraOpaqueTexture_TexelSize;
			float _BoxBlur;


			//盒装模糊
			half4 DownsampleBox4Tap(TEXTURE2D_PARAM(tex, samplerTex), float2 uv, float2 texelSize)
			{
				float4 d = texelSize.xyxy * float4(-1.0, -1.0, 1.0, 1.0);

				half4 s = SAMPLE_TEXTURE2D(tex, samplerTex
				                           , UnityStereoTransformScreenSpaceTex(uv + d.xy));
				s += SAMPLE_TEXTURE2D(tex, samplerTex
				                      , UnityStereoTransformScreenSpaceTex(uv+ d.zy));
				s += SAMPLE_TEXTURE2D(tex, samplerTex
				                      , UnityStereoTransformScreenSpaceTex(uv+ d.xw));
				s += SAMPLE_TEXTURE2D(tex, samplerTex
				                      , UnityStereoTransformScreenSpaceTex(uv+ d.zw));


				return s * 0.25;
			}

			half4 FragDownsample4(v2f i) : SV_Target
			{
				half4 color = DownsampleBox4Tap(TEXTURE2D_ARGS(_CameraColorTexture, sampler_CameraColorTexture)
				                                , i.uv, _BoxBlur.xx);
				return color;
			}
			ENDHLSL
		}
	}
}