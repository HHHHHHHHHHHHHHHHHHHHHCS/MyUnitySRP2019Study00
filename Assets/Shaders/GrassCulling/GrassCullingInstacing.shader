Shader "MyRP/GrassCulling/GrassCullingInstancing"
{
	//TODO:Dark Texture
	Properties
	{
		_GrassColorRT ("Grass Color RT", 2D) = "white" { }
		
		[MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
		_GroundColor ("Ground Color", Color) = (0.5, 0.5, 0.5)
		
		[Header(Grass Shape)]
		_GrassWidth ("Grass Width", Float) = 1
		_GrassHeight ("Grass Height", FLoat) = 1
		
		[Header(Wind)]
		_WindAIntensity ("Wind A Intensity", Float) = 1.77
		_WindAFrequency ("Wind A Frequency", Float) = 4
		_WindATiling ("Wind A Tiling", Vector) = (0.1, 0.1, 0)
		_WindAWrap ("Wind A Wrap", Vector) = (0.5, 0.5, 0)
		
		_WindBIntensity ("Wind B Intensity", Float) = 0.25
		_WindBFrequency ("Wind B Frequency", Float) = 7.7
		_WindBTiling ("Wind B Tiling", Vector) = (0.37, 3, 0)
		_WindBWrap ("Wind B Wrap", Vector) = (0.5, 0.5, 0)
		
		_WindCIntensity ("Wind C Intensity", Float) = 0.125
		_WindCFrequency ("Wind C Frequency", Float) = 11.7
		_WindCTiling ("Wind C Tiling", Vector) = (0.77, 3, 0)
		_WindCWrap ("Wind C Wrap", Vector) = (0.5, 0.5, 0)
		
		[Header(Lighting)]
		_RandomNormal ("Random Normal", Float) = 0.15
		
		//make SRP batcher happy
		[HideInInspector]_PivotPosWS ("_PivotPosWS", Vector) = (0, 0, 0, 0)
		[HideInInspector]_BoundSize ("_BoundSize", Vector) = (1, 1, 0)
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }
		
		Pass
		{
			Cull Back //使用默认的culling 因为这个shader 是 面片
			ZTest Less
			Tags { "LightMode" = "UniversalForward" }
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			//URP keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile _ _SHADOWS_SOFT
			// Unity defined keywords
			#pragma multi_compile_fog
			
			#include "Packages/com.unity.render-pipelines/universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines/universal/ShaderLibrary/Lighting.hlsl"
			
			struct a2v
			{
				float4 positionOS: POSITION;
			};
			
			struct v2f
			{
				float4 positionCS: SV_POSITION;
				half3 color: COLOR;
			};
			
			CBUFFER_START(UnityPerMaterial)
			
			sampler2D _GrassColorRT;
			
			float _GrassWidth;
			float _GrassHeight;
			
			half3  _BaseColor;
			half3 _GroundColor;
			
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
			
			float3 _PivotPosWS;
			float2 _BoundSize;
			
			StructuredBuffer<float3> _AllInstancesTrasnformBuffer;
			StructuredBuffer<uint> _VisibleInstanceOnlyTrasnformIDBuffer;
			
			CBUFFER_END
			
			
			sampler2D _GrassBendingRT;
			
			//half3 ApplySingleDirectLight(Light)
			
			v2f vert(a2v IN, uint instanceID: SV_INSTANCEID)
			{
				v2f o ;
				
				float3 perGrassPivotPosWS = _AllInstancesTrasnformBuffer[_VisibleInstanceOnlyTrasnformIDBuffer[instanceID]];
				
				float perGrassHeight = lerp(2, 5, (sin(perGrassPivotPosWS.x * 23.4643 + perGrassPivotPosWS.z) * 0.45 + 0.55)) * _GrassHeight;
				
				float2 grassBendingUV = ((perGrassPivotPosWS.xz - _PivotPosWS.xz) / _BoundSize) * 0.5 + 0.5;
				float stepped = tex2Dlod(_GrassBendingRT, float4(grassBendingUV, 0, 0)).x;
				
				//rotation (make grass LookAt() camera just like a billboard)
				float3 cameraTransformRightWS = UNITY_MATRIX_V[0].xyz;//UNITY_MATRIX_V[0].xyz == world space camera Right unit vector
				float3 cameraTransformUpWS = UNITY_MATRIX_V[1].xyz;//UNITY_MATRIX_V[1].xyz == world space camera Up unit vector
				float3 cameraTransformForwardWS = -UNITY_MATRIX_V[2].xyz;//UNITY_MATRIX_V[2].xyz == -1 * world space camera Forward unit vector
				
				//Expand Billboard (billboard Left+right)  宽度
				float3  positionOS = IN.positionOS.x * cameraTransformRightWS * _GrassWidth * (sin(perGrassPivotPosWS.x * 95.4643 + perGrassPivotPosWS.z) * 0.45 + 0.55);//random width from posXZ, min 0.1
				
				//Expand Billboard (billboard Up)  高度
				positionOS += IN.positionOS.y * cameraTransformUpWS;
				
				//踩弯的时候让草变短小
				float3 bendDir = cameraTransformForwardWS;
				bendDir.xz *= 0.5;
				bendDir.y = min(-0.5, bendDir.y);
				//越高踩弯幅度越大
				positionOS = lerp(positionOS.xyz + bendDir * positionOS.y / - bendDir.y, positionOS.xyz, stepped * 0.95 + 0.05);
				
				positionOS.y *= perGrassHeight;

				//TODO:
			}
			
			ENDHLSL
			
		}
	}
}
