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
	
	TEXTURE2D_X(_MainTex);//Sampler2D
	
	float4x4 _ViewProjM;
	float4x4 _PrevViewProjM;
	float _Intensity;
	float _Clamp;
	float4 _MainTex_TexelSize;
	
	//URP 理论上应该有内置
	struct FullscreenAttributes
	{
		#if _USE_DRAW_PROCEDURAL
			uint vertexID: SV_VertexID;
		#else
			float4 positionOS: POSITION;
			float2 uv: TEXCOORD0;
		#endif
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};
	
	struct VaryingsCMB
	{
		float4 positionCS: SV_POSITION;
		float4 uv: TEXCOORD0;
		UNITY_VERTEX_OUTPUT_STEREO
	};
	
	
	
	VaryingsCMB VertCMB(FullscreenAttributes input)
	{
		VaryingsCMB output;
		
		//INSTANCE 给 VR 用的
		UNITY_SETUP_INSTANCE_ID(input);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
		
		#if _USE_DRAW_PROCEDURAL
			//通过instanceID 来确认用quad哪个顶点
			GetProceduralQuad(input.vertexID, output.positionCS, output.uv.xy);
			
			//[-1,1] -> [0,1]
			float4 projPos = output.positionCS * 0.5;
			projPos.xy = projPos.xy + projPos.w;
			output.uv.zw = projPos.xy;
		#else
			//output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
			output.positionCS = float4(input.positionOS.xyz, 1.0);
			output.uv.xy = input.uv;
			#if UNITY_UV_STARTS_AT_TOP
				output.uv.y = 1 - output.uv.y;
			#endif

			output.uv.zw = output.uv.xy;
		#endif
		
		
		
		return output;
	}
	
	float2 ClampVelocity(float2 velocity, float maxVelocity)
	{
		float len = length(velocity);
		//rcp(x) = 1/x
		return(len > 0.0)?min(len, maxVelocity) * (velocity * rcp(len)): 0.0;
	}
	
	//通过反算worldPos 在算 vp 空间  做出 运动方向
	float2 GetCameraVelocity(float4 uv)
	{
		float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, uv.xy).r;
		
		#if UNITY_REVERSED_Z
			depth = 1.0 - depth;
		#endif
		
		depth = 2.0 * depth - 1.0;
		
		//([0,1],depth) *2-1得NDC 坐标([-1,1],depth) 用 invProj 得出 vpos  vpos.xyz/vpos.w 就是view位置了
		float3 viewPos = ComputeViewSpacePosition(uv.zw, depth, unity_CameraInvProjection);
		//camera(view) -> world
		float4 worldPos = float4(mul(unity_CameraToWorld, float4(viewPos, 1.0)).xyz, 1.0);
		worldPos/=worldPos.w;
		float4 prevPos = worldPos;
		
		// world -> vp
		float4 prevClipPos = mul(_PrevViewProjM, prevPos);
		float4 curClipPos = mul(_ViewProjM, worldPos);
		
		// 齐次除法
		float2 prevPosCS = prevClipPos.xy / prevClipPos.w;
		float2 curPosCS = curClipPos.xy / curClipPos.w;
		
		return ClampVelocity(prevPosCS - curPosCS, _Clamp);
	}
	
	float3 GatherSample(float sampleNumber, float2 velocity, float invSampleCount, float2 centerUV, float randomVal, float velocitySign)
	{
		float offsetLength = (sampleNumber + 0.5) + (velocitySign * (randomVal - 0.5));
		float2 sampleUV = centerUV + (offsetLength * invSampleCount) * velocity * velocitySign;
		return SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, sampleUV).xyz;
	}
	
	//From  Next Generation Post Processing in Call of Duty: Advanced Warfare [Jimenez 2014]
	// http://advances.realtimerendering.com/s2014/index.html
	/*
	float InterleavedGradientNoise(float2 pixCoord, int frameCount)
	{
		const float3 magic = float3(0.06711056f, 0.00583715f, 52.9829189f);
		float2 frameMagicScale = float2(2.083f, 4.867f);
		pixCoord += frameCount * frameMagicScale;
		return frac(magic.z * frac(dot(pixCoord, magic.xy)));
	}
	*/
	
	half4 DoMotionBlur(VaryingsCMB input, int iterations)
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		
		//非VR uv = input.uv
		float2 uv = UnityStereoTransformScreenSpaceTex(input.uv.xy);
		float2 velocity = GetCameraVelocity(float4(uv, input.uv.zw)) * _Intensity;
		//注释在上面了  float2 生成 noise
		float randomVal = InterleavedGradientNoise(uv * _MainTex_TexelSize.zw, 0);
		float invSampleCount = rcp(iterations * 2.0);
		
		half3 color = 0.0;
		
		UNITY_UNROLL
		for (int i = 0; i < iterations; i ++)
		{
			color += GatherSample(i, velocity, invSampleCount, uv, randomVal, -1.0);
			color += GatherSample(i, velocity, invSampleCount, uv, randomVal, 1.0);
		}
		
		return half4(color * invSampleCount, 1.0);
	}
	
	ENDHLSL
	
	SubShader
	{
		Tags { "RenderType" = "Opaque" /*"RenderPipeline" = "UniversalPipeline"*/ }
		LOD 100
		ZTest Always
		ZWrite Off
		Cull Off
		
		Pass
		{
			Name "Camera Motion Blur - Low Quality"
			
			HLSLPROGRAM
			
			#pragma vertex VertCMB
			#pragma fragment Frag
			
			half4 Frag(VaryingsCMB input): SV_Target
			{
				return DoMotionBlur(input, 2);
			}
			
			ENDHLSL
			
		}
		
		Pass
		{
			Name "Camera Motion Blur - Medium Quality"
			
			HLSLPROGRAM
			
			#pragma vertex VertCMB
			#pragma fragment Frag
			
			half4 Frag(VaryingsCMB input): SV_Target
			{
				return DoMotionBlur(input, 3);
			}
			
			ENDHLSL
			
		}
		
		Pass
		{
			Name "Camera Motion Blur - High Quality"
			
			HLSLPROGRAM
			
			#pragma vertex VertCMB
			#pragma fragment Frag
			
			half4 Frag(VaryingsCMB input): SV_Target
			{
				return DoMotionBlur(input, 4);
			}
			
			ENDHLSL
			
		}
	}
}
