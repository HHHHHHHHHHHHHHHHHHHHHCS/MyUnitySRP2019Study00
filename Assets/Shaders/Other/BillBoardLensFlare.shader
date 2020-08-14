//copy by github.com/ColinLeung-NiloCat/UnityURP-BillboardLensFlareShader
Shader "MyRP/Other/BillBoardLensFlare"
{
	Properties
	{
		[MainColor] _BaseColor ("BaseColor (can use alpha to do fadeout)", Color) = (1, 1, 1, 1)
		_BaseColorRGBIntensity ("BaseColorRGBIntensity", Float) = 1
		[MainTexture] _BaseMap ("BaseMap (regular LDR texture)", 2D) = "white" { }
		_RemoveTextureArtifact ("RemoveTextureArtifact", Range(0, 0.5)) = 0
		
		[Header(PreMultiply Alpha. Turn it ON only if your texture has correct alpha)]
		[Toggle]_UsePreMultiplyAlpha ("UsePreMultiplyAlpha (recommend _BaseMap's alpha = 'From Gray Scale')", Float) = 0
		
		[Header(Depth Occlusion)]
		_LightSourceViewSpaceRadius ("LightSourceViewSpaceRadius", range(0, 1)) = 0.05
		_DepthOcclusionTestZBias ("DepthOcclusionTestZBias", range(-1, 1)) = -0.001
		
		[Header(If camera too close Auto fadeout)]
		_StartFadeinDistanceWorldUnit ("StartFadeinDistanceWorldUnit", Float) = 0.05
		_EndFadeinDistanceWorldUnit ("EndFadeinDistanceWorldUnit", Float) = 0.5
		
		[Header(Optional Flicker animation)]
		[Toggle]_ShouldDoFlicker ("ShouldDoFlicker", FLoat) = 1
		_FlickerAnimSpeed ("FlickerAnimSpeed", Float) = 5
		_FlickResultIntensityLowestPoint ("FlickResultIntensityLowestPoint", range(0, 1)) = 0.5
	}
	SubShader
	{
		//因为光斑是最后覆盖到屏幕上  所以尽量最晚绘制
		Tags { "RenderType" = "Overlay" "Queue" = "Overlay" "DisableBatching" = "True" "IgnoreProjector" = "True" }
		
		//不需要覆盖深度
		ZWrite Off
		//永远通过深度测试
		ZTest Always//ZTest Off == ZTest Always
		
		//Blend One One
		Blend One OneMinusSrcAlpha
		
		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		
		TEXTURE2D(_BaseMap);
		SAMPLER(sampler_BaseMap);
		
		SAMPLER(_CameraDepthTexture);
		
		CBUFFER_START(unityPerMaterial)
		
		float4 _BaseMap_ST;
		
		half4 _BaseColor;
		half _BaseColorRGBIntensity;
		half _RemoveTextureArtifact;
		
		float _UsePreMultiplyAlpha;
		
		float _LightSourceViewSpaceRadius;
		float _DepthOcclusionTestZBias;
		
		float _StartFadeinDistanceWorldUnit;
		float _EndFadeinDistanceWorldUnit;
		
		CBUFFER_END
		
		
		ENDHLSL
		
		Pass
		{
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			struct a2v
			{
				float4 positionOS: POSITION;
				float2 uv: TEXCOORD0;
				half4 color: COLOR;
			};
			
			struct v2f
			{
				float4 position: SV_POSITION;
				float2 uv: TEXCOORD0;
				half4 color: TEXCOORD1;
			};
			
			#define COUNT 8 // 你可以编辑任何数字(1~32) 越低速度越快  常量可以实现编译优化
			
			v2f vert(v2f input)
			{
				v2f o;
				o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
				o.color = input.color * _BaseColor;
				o.color.rgb *= _BaseColorRGBIntensity;
				
				//让quad 朝向 摄像机的view 空间
				float3 quadPivotPosOS = float3(0, 0, 0);
				float3 quadPivotPosWS = TransformObjectToWorld(quadPivotPosOS);
				float3 quadPivotPosVS = TransformWorldToView(quadPivotPosWS);
				
				float4x4 o2w = GetObjectToWorldMatrix();
				//get transform.lossyScale using:
				//https://forum.unity.com/threads/can-i-get-the-scale-in-the-transform-of-the-object-i-attach-a-shader-to-if-so-how.418345/
				//GetObjectToWorldMatrix 返回Object->World 的矩阵
				//得到XY 尺寸
				float2 scaleXY_WS = float2(
					length(o2w._m00_m10_m20), // scale x axis
					length(o2w._m01_m11_m21) // scale y axis
				);
				//也可以这样 o2w[0].x  o2w[0][0]
				//TODO:

				o.position = float4(quadPivotPosVS,1.0);

				return o;
			}
			
			half4 frag(v2f IN): SV_Target
			{
				return 0;
			}
			
			ENDHLSL
			
		}
	}
}
