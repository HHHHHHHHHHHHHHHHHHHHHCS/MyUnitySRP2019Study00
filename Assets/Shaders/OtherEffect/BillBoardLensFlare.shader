//copy by github.com/ColinLeung-NiloCat/UnityURP-BillboardLensFlareShader
Shader "MyRP/OtherEffect/BillBoardLensFlare"
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
		
		CBUFFER_START(UnityPerMaterial)
		
		float4 _BaseMap_ST;
		
		half4 _BaseColor;
		half _BaseColorRGBIntensity;
		half _RemoveTextureArtifact;
		
		float _UsePreMultiplyAlpha;
		
		float _LightSourceViewSpaceRadius;
		float _DepthOcclusionTestZBias;
		
		float _StartFadeinDistanceWorldUnit;
		float _EndFadeinDistanceWorldUnit;
		
		float _FlickerAnimSpeed;
		float _FlickResultIntensityLowestPoint;
		float _ShouldDoFlicker;
		
		CBUFFER_END
		
		
		ENDHLSL
		
		Pass
		{
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			struct a2v
			{
				float4 vertex: POSITION;
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
			
			v2f vert(a2v input)
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
				//得到XY 尺寸  因为是广告牌 Z 是0
				float2 scaleXY_WS = float2(
					length(o2w._m00_m10_m20), // scale x axis
					length(o2w._m01_m11_m21) // scale y axis
				);
				//也可以这样 o2w[0].x  o2w[0][0]
				
				float3 posVS = quadPivotPosVS + float3(input.vertex.xy * scaleXY_WS, 0);
				
				o.position = mul(GetViewToHClipMatrix(), float4(posVS, 1));
				
				float visibilityTestPassedCount = 0;
				float linearEyeDepthOfFlarePivot = -quadPivotPosVS.z;//view space 前是 -z , 这里要翻转它成 +z
				float testLoopSingleAxisWidth = COUNT * 2 + 1;
				float totalTestCount = testLoopSingleAxisWidth * testLoopSingleAxisWidth;
				float divider = 1.0 / totalTestCount;
				float maxSinglexisOffset = _LightSourceViewSpaceRadius / testLoopSingleAxisWidth;
				
				//计算被遮挡的count
				for (int x = -COUNT; x <= COUNT; x ++)
				{
					for (int y = -COUNT; y <= COUNT; y ++)
					{
						float3 testPosVS = quadPivotPosVS;
						testPosVS.xy += float2(x, y) * maxSinglexisOffset;//左右偏移
						float4 pivotPosCS = mul(GetViewToHClipMatrix(), float4(testPosVS, 1.0));
						float4 pivotScreenPos = ComputeScreenPos(pivotPosCS);
						float2 screenUV = pivotScreenPos.xy / pivotScreenPos.w;
						
						if (screenUV.x > 1 || screenUV.x < 0 || screenUV.y > 1 || screenUV.y < 0)
						{
							continue;
						}
						
						float sampledSceneDepth = tex2Dlod(_CameraDepthTexture, float4(screenUV, 0, 0)).r;
						float linearEyeDepthFromSceneDepthTexture = LinearEyeDepth(sampledSceneDepth, _ZBufferParams);
						float linearEyeDepthFromSelfALU = pivotPosCS.w;//clip space .w is view space z, = linear eye depth
						
						//+1->看得见
						//+0->被遮挡
						visibilityTestPassedCount += (linearEyeDepthFromSelfALU + _DepthOcclusionTestZBias) < linearEyeDepthFromSceneDepthTexture?1: 0;
					}
				}
				
				//被遮挡的概率 0~1
				float visibilityResult01 = visibilityTestPassedCount * divider;
				
				//如果相机离闪光灯太近，请平滑淡出以防止闪光灯过多地阻挡相机（通常用于fps游戏）
				visibilityResult01 *= smoothstep(_StartFadeinDistanceWorldUnit, _EndFadeinDistanceWorldUnit, linearEyeDepthOfFlarePivot);
				
				//shader 闪烁动画
				
				//“uniform if”不会影响任何现代硬件（甚至移动设备）的性能
				if (_ShouldDoFlicker)
				{
					float flickerMul = 0;
					//可以用噪音图
					flickerMul += saturate(sin(_Time.y * _FlickerAnimSpeed * 1.0000)) * (1 - _FlickResultIntensityLowestPoint) + _FlickResultIntensityLowestPoint;
					flickerMul += saturate(sin(_Time.y * _FlickerAnimSpeed * 0.6437)) * (1 - _FlickResultIntensityLowestPoint) + _FlickResultIntensityLowestPoint;
					visibilityResult01 *= saturate(flickerMul / 2);
				}
				
				o.color.a *= visibilityResult01;
				
				
				o.color.rgb *= o.color.a;
				o.color.a = _UsePreMultiplyAlpha ? o.color.a: 0;
				
				//如果大概率看不见 则移动到GPU裁剪区域外  做优化
				o.position = visibilityResult01 < divider?float4(999, 999, 999, 1): o.position;
				
				return o;
			}
			
			half4 frag(v2f IN): SV_Target
			{
				return saturate(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) - _RemoveTextureArtifact) * IN.color;;
			}
			
			ENDHLSL
			
		}
	}
}
