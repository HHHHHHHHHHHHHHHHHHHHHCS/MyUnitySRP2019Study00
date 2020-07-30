Shader "MyRP/Grass/GrassBendingInstancing"
{
	Properties
	{
		_BaseColor ("BaseColor", Color) = (1, 1, 1, 1)
		_GroundColor ("GroundColor", Color) = (0.5, 0.5, 0.5, 0.5)
		
		[Header(Grass Shape)]
		_GrassWidth ("GrassWidth", Float) = 1
		_GrassWidth ("GrassHight", Float) = 1
		
		[Header(Wind)]
		_WindAIntensity ("WindIntensity", Float) = 1
		_WindAFrequency ("WindFrequency", Float) = 4
		_WindATiling ("WindTiling", Vector) = (0.1, 0.1, 0)
		_WindAWrap ("WindTiling", Vector) = (0.5, 0.5, 0)
		
		_WindBIntensity ("WindIntensity", Float) = 0.25
		_WindBFrequency ("WindFrequency", Float) = 7.7
		_WindBTiling ("WindTiling", Vector) = (0.37, 3.0, 0)
		_WindBWrap ("WindTiling", Vector) = (0.5, 0.5, 0)
		
		_WindCIntensity ("WindIntensity", Float) = 0.125
		_WindCFrequency ("WindFrequency", Float) = 11.7
		_WindCTiling ("WindTiling", Vector) = (0.77, 3.0, 0)
		_WindCWrap ("WindTiling", Vector) = (0.5, 0.5, 0)
		
		[Header(Lighting)]
		_RandomNormal ("RandomNormal", Float) = 0.15
		
		//make SRP batcher happy
		[HideInInspector]_DrawDistance ("DrawDistance", Float) = 100
		[HideInInspector]_PivotPosWS ("PivotPosWS", Vector) = (0, 0, 0, 0)
		[HideInInspector]_BoundSize ("BoundSize", Vector) = (1, 1, 0)
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" /*"RenderPipeline" = "UniversalRenderPipeline"*/ }
		
		Pass
		{
			Cull Back //这是平面片 用默认的culling 就好了
			ZTest Less
			Tags { "LightMode" = "UniversalForward" }
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			//Universal Render Pipeline keywords
			//---------------------------------
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile _ _SHADOWS_SOFT
			// -------------------------------------
			// Unity defined keywords
			#pragma multi_compile_fog
			
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			
			
			struct a2v
			{
				float4 vertex: POSITION;
			};
			
			struct v2f
			{
				float4 position: SV_POSITION;
				half3 color: COLOR;
			};
			
			
			CBUFFER_START(UnityPerMaterial)
			
			
			half3 _BaseColor;
			half3 _GroundColor;
			
			float _GrassWidth;
			float _GrassHeight;
			
			float _WindAIntensity;
			float _WindAFrequency;
			float2 _WindATiling;
			float2 _WindAWrap;
			
			float _WindBIntensity;
			float _WindBFrequency;
			float2 _WindBTiling;
			float2 _WindBWrap;
			
			float _WindCIntensity;
			float _WindCFrequency;
			float2 _WindCTiling;
			float2 _WindCWrap;
			
			half _RandomNormal;
			
			float _DrawDistance;
			float3 _PivotPosWS;
			float2 _BoundSize;
			
			StructuredBuffer<float4> _TransformBuffer;
			CBUFFER_END
			
			sampler2D _GrassBendingRT;
			
			
			v2f vert(a2v input, uint instanceID: SV_INSTANCEID)
			{
				v2f o;
				
				//bufferData.xyz    local 位置
				//bufferData.w      高度缩放
				float4 bufferData = _TransformBuffer[instanceID];
				float3 perGrassPivotPosWS = bufferData.xyz * float3(_BoundSize.x, 1, _BoundSize.y) + _PivotPosWS; //posOS -> posWS
				float perGrassHeight = bufferData.w * _GrassHeight;
				
				//float2 grassBendingUV = ((perGrassPivotPosWS.xz - _PivotPosWS.xz)/_BoundSize) * 0.5 + 0.5;//计算草在 脚印图的UV
				float2 grassBendingUV = bufferData.xz * 0.5 + 0.5;//计算草在 脚印图的UV
				float stepped = tex2Dlod(_GrassBendingRT, float4(grassBendingUV, 0, 0)).x;
				
				//旋转 让草朝向摄像机 就像 广告板效果 一样
				//=========================================
				float3 cameraTransformRightWS = UNITY_MATRIX_V[0].xyz;//UNITY_MATRIX_V[0].xyz == world space camera Right unit vector
				float3 cameraTransformUpWS = UNITY_MATRIX_V[1].xyz;//UNITY_MATRIX_V[1].xyz == world space camera Up unit vector
				float3 cameraTransformForwardWS = -UNITY_MATRIX_V[2].xyz;//UNITY_MATRIX_V[2].xyz == -1 * world space camera Forward unit vector
			}
			
			half4 frag(Varyings IN): SV_Target
			{
				return half4(IN.color, 1);
			}
			
			
			ENDHLSL
			
		}
		
		//复制 ShadowCaster Pass, 可以让草产生阴影
		//赋值 DepthOnly Pass , 可以让草产生 _CameraDepthTexture 深度图
	}
}
