Shader "MyRP/MotionBlur/MotionBlur"
{
	HLSLINCLUDE
	
	#pragma exclude_renderers gles
	
	//VR 用的  绘制在Quad上
	#pragma multi_compile _ _USE_DRAW_PROCEDURAL
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
	
	TEXTURE2D_X(_SourceTex);//Sampler2D
	
	float4x4 _ViewProjM;
	float4x4 _PrevViewProjM;
	float _Intensity;
	float _Clamp;
	float4 _SourceTex_TexelSize;
	
	sturct VaryingsCMB
	{
		float4 positionCS: SV_POSITION;
		float4 uv: TEXCOORD0;
		UNITY_VERTEX_OUTPUT_STEREO
	};
	
	VaryingCMB VertCMB(FullscreenAttributes input)
	{
		VaringsCMB output;
		
		//INSTANCE 给 VR 用的
		UNITY_SETUP_INSTANCE_ID(input);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
		
		#if _USE_DRAW_PROCEDURAL
			//通过instanceID 来确认用quad哪个顶点
			GetProceduralQuad(input.vertexID, output.positionCS, output.uv.xy);
		#else
			output.positionCS = TrasnformObjectToHClip(input.positionOS.xyz);
			output.uv.xy = input.uv;
		#endif
		
		//[-1,1] -> [0,1]
		float4 projPos = output.PositionCS * 0.5;
		projPos.xy = projPos.xy + projPos.w;
		output.uv.zw = projPos.xy;
		
		return output;
	}
	
	float2 ClampVelocity(float2 velocity, , float maxVelocity)
	{
		float len = length(velocity);
		//rcp(x) = 1/x
		return(len > 0.0)?min(len, maxVelocity) * (velocity * rcp(len)): 0.0;
	}
	
	//通过反算worldPos 在算 vp 空间  做出 运动方向
	float2 GetCameraVelocity(float4 uv)
	{
		float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler2D_PointClamp, uv.xy).r;
		
		#if UNITY_REVERSED_Z
			depth = 1.0 - depth;
		#endif
		
		depth = 2.0 * depth - 1.0;
		
		//([0,1],depth) *2-1得NDC 坐标([-1,1],depth) 用 invProj 得出 vpos  vpos.xyz/vpos.w 就是view位置了
		float3 viewPos = ComputeViewSpacePosition(uv.zw, depth, unity_CameraInvProjection);
		//camera(view) -> world
		float4 worldPos = float4(mul(unity_CameraToWorld, float4(viewPos, 1.0)).xyz, 1.0);
		float4 prevPos = worldPos;
		
		// world -> vp
		float4 prevClipPos = mul(_PrevViewProjM, prevPos);
		float4 curClipPos = mul(_ViewProjM, worldPos);
		
		// 齐次除法
		float2 prevPosCS = prevClipPos.xy / prevClipPos.w;
		float2 curPosCS = curClipPos.xy / curClipPos.w;
		
		return ClampVelocity(prevPosCS - curPosCS, _Clamp);
	}
	
	ENDHLSL
	
}
