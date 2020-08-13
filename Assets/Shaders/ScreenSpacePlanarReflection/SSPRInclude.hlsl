#ifndef SSPRInclude
	#define SSPRInclude
	
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
	
	TEXTURE2D(_MobileSSPR_ColorRT);
	sampler LinearClampSampler;
	
	struct ReflectionInput
	{
		float3 posWS;
		float4 screenPos;
		float2 screenSpaceNoise;
		float roughness;
		float SSPR_Usage;
	};
	
	half3 GetResultReflection(ReflectionInput data)
	{
		half3 viewWS = (data.posWS - _WorldSpaceCameraPos);
		viewWS = normalize(viewWS);
		
		half3 reflectDirWS = viewWS * half3(1, -1, 1);//y翻转  反射朝向横板
		
		//计算反射球的颜色 Lighting.hlsl-> half3 GlossyEnvironmentReflection(half3 reflectVector, half perceptualRoughness, half occlusion)
		half3 reflectionProbeResult = GlossyEnvironmentReflection(reflectDirWS, data.roughness, 1);
		half4 SSPRResult = 0;
		
		#if _MobileSSPR
			half2 screenUV = data.screenPos.xy / data.screenPos.w;
			SSPRResult = SAMPLE_TEXTURE2D(_MobileSSPR_ColorRT, LinearClampSampler, screenUV + data.screenSpaceNoise);
		#endif
		
		//combine reflection probe and SSPR
		half3 finalReflection = lerp(reflectionProbeResult, SSPRResult.rgb, SSPRResult.a * data.SSPR_Usage);
		
		return finalReflection;
	}
	
#endif //SSPRInclude
