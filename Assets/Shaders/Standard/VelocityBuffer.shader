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
		Tags { /*"RenderPipeline" = "MyRenderPipeline"*/ }
		
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

//1.首先在DrawSettings中把PerObjectData.Motion打开
//-------------------------
//2.接着在 UnityPerFrame 中添加且自己传入
// float4x4 Matrix_PrevViewProj
// float4x4 Matrix_ViewJitterProj
// 接着在UnityPerDraw中添加 下面三个属性
// float4x4 unity_MatrixPreviousM;
// float4x4 unity_MatrixPreviousMI;
// float4 unity_MotionVectorsParams;
//-------------------------
//3.TEXCOORD4 储存了上一帧的ObjectPos
// unity_MotionVectorsParams.x > 0 是 skinMesh
// unity_MotionVectorsParams.y > 0 强制没有motionVector
/*
struct a2v
{
	float4 vertex: POSITION;
	float3 vertex_old: TEXCOORD4;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
	float4 vertex: SV_POSITION;
	float4 clipPos: TEXCOORD0;
	float3 clipPos_Old: TEXCOORD1;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

v2f vert(a2v IN)
{
	v2f o = (v2f) 0;
	UNITY_SETUP_INSTANCE_ID(IN);
	UNITY_TRANSFORM_INSTANCE_ID(IN, o);
	
	float4 worldPos = mul(UNITY_MATRIX_M, float4(IN.vertex.xyz, 1.0));
	
	o.clipPos = TransformWorldToHClip(worldPos.xyz);
	o.clipPos_Old = mul(Matrix_PrevViewProj, mul(unity_MatrixPreviousM, unity_MotionVectorsParams.x > 0?float4(IN.vertex_old.xyz, 1.0): IN.vertex));
	
	o.vertex = mul(Matrix_ViewJitterProj, worldPos);//UNITY_MATRIX_VP
	return o;
}

float2 frag(v2f IN): SV_TARGET
{
	float2 NDC_PixelPos = (IN.clipPos.xy / IN.clipPos.w);
	float2 NDC_PixelPos_Old = (IN.clipPos_Old.xy / IN.clipPos_Old.w);
	float2 ObjectMotion = (NDC_PixelPos - NDC_PixelPos_Old) * 0.5;
	return lerp(ObjectMotion, 0, unity_MotionVectorsParams.y > 0);
}
*/