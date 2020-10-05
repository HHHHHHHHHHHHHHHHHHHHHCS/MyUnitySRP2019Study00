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
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			
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
			
			StructuredBuffer<float3> _AllInstancesTransformBuffer;
			StructuredBuffer<uint> _VisibleInstanceOnlyTransformIDBuffer;
			
			CBUFFER_END
			
			
			sampler2D _GrassBendingRT;
			
			half3 ApplySingleDirectLight(Light light, half3 N, half3 V, half3 albedo, half positionOSY)
			{
				half3 H = normalize(light.direction + V);
				
				half directDiffuse = dot(N, light.direction) * 0.5 + 0.5;
				
				//高处加点反光
				float directSpecular = saturate(dot(N, H));
				//pow(directSpecular,8)
				directSpecular *= directSpecular;
				directSpecular *= directSpecular;
				directSpecular *= directSpecular;
				//directSpecular *= directSpecular; //enable this line = change to pow(directSpecular,16)
				directSpecular *= 0.1 * positionOSY;
				
				half3 lighting = light.color * (light.shadowAttenuation * light.distanceAttenuation);
				half3 result = (albedo * directDiffuse + directSpecular) * lighting;
				
				return result;
			}
			
			v2f vert(a2v IN, uint instanceID: SV_InstanceID)
			{
				v2f o ;
				
				float3 perGrassPivotPosWS = _AllInstancesTransformBuffer[_VisibleInstanceOnlyTransformIDBuffer[instanceID]];
				
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
				
				//相机距离比例（如果草离相机很远，则使草地宽度更大，以隐藏小于像素大小的三角形闪烁）
				float3 viewWS = _WorldSpaceCameraPos - perGrassPivotPosWS;
				float viewWSLength = length(viewWS);
				positionOS += cameraTransformRightWS * IN.positionOS.x * max(0, viewWSLength * 0.0225);
				
				//移动草 posOS->posWS
				float3 positionWS = positionOS + perGrassPivotPosWS;
				
				float wind = 0;
				wind += (sin(_Time.y * _WindAFrequency + perGrassPivotPosWS.x * _WindATiling.x + perGrassPivotPosWS.z * _WindATiling.y) * _WindAWrap.x + _WindAWrap.y) * _WindAIntensity;
				wind += (sin(_Time.y * _WindBFrequency + perGrassPivotPosWS.x * _WindBTiling.x + perGrassPivotPosWS.z * _WindBTiling.y) * _WindBWrap.x + _WindBWrap.y) * _WindBIntensity;
				wind += (sin(_Time.y * _WindCFrequency + perGrassPivotPosWS.x * _WindCTiling.x + perGrassPivotPosWS.z * _WindCTiling.y) * _WindCWrap.x + _WindCWrap.y) * _WindCIntensity;
				wind *= IN.positionOS.y;//越高影响越强
				float3 windOffset = cameraTransformRightWS * wind;
				positionWS.xyz += windOffset;
				
				//posWS->posCS
				o.positionCS = TransformWorldToHClip(positionWS);
				
				Light mainLight;
				#if _MAIN_LIGHT_SHADOWS
					mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
				#else
					mainLight = GetMainLight();
				#endif
				
				//附加随机的Normal
				//默认草的法线是100%向上指向世界空间，这是一个重要但简单的草法线技巧
				//随机应用于普通照明，否则照明太均匀
				//将CameraTransformForwards应用于正常，因为草地是广告牌
				half3 randomAddToN = (_RandomNormal * sin(perGrassPivotPosWS.x * 82.32523 + perGrassPivotPosWS.z) + wind * - 0.25) * cameraTransformRightWS;
				half3 N = normalize(half3(0, 1, 0) + randomAddToN - cameraTransformForwardWS * 0.5);
				half3 V = viewWS / viewWSLength;
				
				half3 albedo = lerp(_GroundColor, _BaseColor, IN.positionOS.y);//高度决定不一样的颜色 , 可以替换
				half3 lightingResult = SampleSH(0) * albedo;//indirect
				lightingResult += ApplySingleDirectLight(mainLight, N, V, albedo, positionOS.y);//main direct light
				
				#if _ADDITIONAL_LIGHTS
					int additionalLightsCount = GetAdditionalLightsCount();
					for (int i = 0; i < additionalLightsCount; ++ i)
					{
						Light light = GetAdditionalLight(i, positionWS);
						lightingResult += ApplySingleDirectLight(light, N, V, albedo, positionOS.y);
					}
					
				#endif
				
				float fogFactor = ComputeFogFactor(o.positionCS.z);
				o.color = MixFog(lightingResult, fogFactor);
				
				return o;
			}
			
			half4 frag(v2f i): SV_Target
			{
				return half4(i.color, 1);
			}
			
			ENDHLSL
			
		}
		
		//复制 ShadowCaster Pass, 可以让草产生阴影
		//赋值 DepthOnly Pass , 可以让草产生 _CameraDepthTexture 深度图
	}
}
