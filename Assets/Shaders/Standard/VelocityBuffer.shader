Shader "MyRP/VelocityBuffer"
{
	Properties { }
	
	HLSLINCLUDE
	
	#include "./Lib.hlsl"
	
	sampler2D _CameraDepthTex;
	
	float4x4 _PreviousGPUViewProjection;
	float2 _PreviousJitterOffset;
	float2 _CurrentJitterOffset;
	
	float2 backgroundVelocityBuffer(v2f_ray i): SV_TARGET
	{
		float3 ray = normalize(i.ray);
		
		float depth = tex2D(_CameraDepthTex, i.uv.xy);
		float4 worldPos;
		if (depth <= 0)
		{
			worldPos = float4(ray.xyz, 0);
		}
		else
		{
			worldPos = float4(DepthToWorldPos(i.uv, depth, ray), 1);
		}
		
		float4 previousP = mul(_PreviousGPUViewProjection, worldPos);
		float4 currentP = mul(UNITY_MATRIX_VP, worldPos);
		previousP /= previousP.w;
		currentP /= currentP.w;
		currentP.y *= _ProjectionParams.x;
		float2 previousScreenPos = previousP.xy * 0.5 + 0.5;
		float2 currentScreenPos = previousP.xy * 0.5 + 0.5;
		previousScreenPos += _PreviousJitterOffset * (_ScreenParams.zw - 1);
		currentScreenPos += _CurrentJitterOffset * (_ScreenParams.zw - 1);
		
		return currentScreenPos - previousScreenPos;
	}
	
	
	ENDHLSL
	
	SubShader
	{
		// #0 Background Velocity Buffer Pass
		Pass
		{
			Name "Skybox Velocity Pass"
			
			Cull Off
			ZWrite Off
			ZTest Off
			
			HLSLPROGRAM
			
			#pragma vertex vert_ray
			#pragma fragment backgroundVelocityBuffer
			
			ENDHLSL
			
		}
	}
}