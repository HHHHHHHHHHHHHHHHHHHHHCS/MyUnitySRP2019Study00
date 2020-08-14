Shader "MyRP/SSPRPlane"
{
	Properties
	{
		[MainColor]_BaseColor ("BaseColor", Color) = (1, 1, 1, 1)
		[MainTexture]_BaseMap ("BaseMap", 2D) = "black" { }
		
		_Roughness ("_Roughness", Range(0, 1)) = 0.25
		[NoScaleOffset]_SSPR_UVNoiseTex ("_SSPR_UVNoiseTex", 2D) = "gray" { }
		_SSPR_NoiseIntensity ("_SSPR_NoiseIntensity", Range(-0.2, 0.2)) = 0.0
		
		_UV_MoveSpeed ("_UV_MoveSpeed (xy only)(for things like water flow)", Vector) = (0, 0, 0, 0)
		
		[NoScaleOffset]_ReflectionAreaTex ("_ReflectionArea", 2D) = "white" { }
	}
	
	SubShader
	{
		Pass
		{
			Tags { "LightMode" = "MobileSSPR" }
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile _ _MobileSSPR
			
			#include "SSPRInclude.hlsl"
			
			
			struct a2v
			{
				float4 position: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			struct v2f
			{
				float4 pos: SV_POSITION;
				float2 uv: TEXCOORD0;
				float4 screenPos: TEXCOORD1;
				float3 worldPos: TEXCOORD2;
			};
			
			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);
			
			TEXTURE2D(_SSPR_UVNoiseTex);
			SAMPLER(sampler_SSPR_UVNoiseTex);
			TEXTURE2D(_ReflectionAreaTex);
			SAMPLER(sampler_ReflectionAreaTex);
			
			CBUFFER_START(UnityPerMaterial)
			float4 _BaseMap_ST;
			half4 _BaseColor;
			half _SSPR_NoiseIntensity;
			float2 _UV_MoveSpeed;
			half _Roughness;
			CBUFFER_END
			
			v2f vert(a2v input)
			{
				v2f o;
				o.pos = TransformObjectToHClip(input.position.xyz);
				o.uv = TRANSFORM_TEX(input.uv, _BaseMap) + _Time.y * _UV_MoveSpeed;
				o.screenPos = ComputeScreenPos(o.pos);
				o.worldPos = TransformObjectToWorld(input.position.xyz);
				return o;
			}
			
			half4 frag(v2f input): SV_TARGET
			{
				//base color
				half3 baseColor = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
				
				//noise
				float2 noise = SAMPLE_TEXTURE2D(_SSPR_UVNoiseTex, sampler_SSPR_UVNoiseTex, input.uv).xy;
				noise = noise * 2 - 1;
				noise.y = -abs(noise.y);//朝一个方向移动
				noise.x *= 0.25;
				noise *= _SSPR_NoiseIntensity;
				
				
				//reflection color
				ReflectionInput reflectionData;
				reflectionData.posWS = input.worldPos;
				reflectionData.screenPos = input.screenPos;
				reflectionData.screenSpaceNoise = noise;
				reflectionData.roughness = _Roughness;
				reflectionData.SSPR_Usage = _BaseColor.a;
				
				half3 resultReflection = GetResultReflection(reflectionData);
				
				//reflection area
				half reflectionArea = SAMPLE_TEXTURE2D(_ReflectionAreaTex, sampler_ReflectionAreaTex, input.uv).r;
				half3 finalColor = lerp(baseColor, resultReflection, reflectionArea);
				
				return half4(finalColor, 1);
			}
			
			ENDHLSL
			
		}
	}
}
