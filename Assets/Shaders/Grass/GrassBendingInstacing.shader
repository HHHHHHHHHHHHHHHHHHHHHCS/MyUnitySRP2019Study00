Shader "MyRP/Grass/GrassBendingInstancing"
{
	Properties
	{
		_BaseColor ("BaseColor", Color) = (1, 1, 1, 1)
		_GroundColor ("GroundColor", Color) = (0.5, 0.5, 0.5, 1)
		
		[Header(Grass Shape)]
		_GrassWidth ("GrassWidth", Float) = 1
		_GrassHeight ("GrassHeight", Float) = 1
		
		[Header(Wind)]
		_WindAIntensity ("WindIntensity", Float) = 1.77
		_WindAFrequency ("WindFrequency", Float) = 4
		_WindATiling ("WindTiling", Vector) = (0.1, 0.1, 0)
		_WindAWrap ("WindWrap", Vector) = (0.5, 0.5, 0)
		
		_WindBIntensity ("WindIntensity", Float) = 0.25
		_WindBFrequency ("WindFrequency", Float) = 7.7
		_WindBTiling ("WindTiling", Vector) = (0.37, 3.0, 0)
		_WindBWrap ("WindWrap", Vector) = (0.5, 0.5, 0)
		
		_WindCIntensity ("WindIntensity", Float) = 0.125
		_WindCFrequency ("WindFrequency", Float) = 11.7
		_WindCTiling ("WindTiling", Vector) = (0.77, 3.0, 0)
		_WindCWrap ("WindWrap", Vector) = (0.5, 0.5, 0)
		
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
			
			half3 ApplySingleDirectLight(Light light, half3 N, half3 V, half3 albedo, half positionOSY)
			{
				half3 H = normalize(light.direction + V);
				
				//diffuse
				half directDiffuse = dot(N, light.direction) * 0.5 + 0.5;//half lambert,to fake grass SSS
				
				//specular(8)
				float directSpecular = saturate(dot(N, H));
				//pow(directSpecular,8)
				directSpecular *= directSpecular;
				directSpecular *= directSpecular;
				directSpecular *= directSpecular;
				//directSpecular*=directSpecular;//if enable pow(16)
				
				directSpecular *= 0.1 * positionOSY;//顶端加一点反光
				
				half3 lighting = light.color * (light.shadowAttenuation * light.distanceAttenuation);
				half3 result = (albedo * directDiffuse + directSpecular) * lighting;
				
				return result;
			}
			
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
				float3 cameraTransformForwardWS = -UNITY_MATRIX_V[2].xyz ;//UNITY_MATRIX_V[2].xyz == -1 * world space camera Forward unit vector
				
				//Expand Billboard (billboard Left+right)
				float3 positionOS = input.vertex.x * cameraTransformRightWS * _GrassWidth;
				//Expand Billboard (billboard Up)
				positionOS += input.vertex.y * cameraTransformUpWS;
				
				//硬混合压草的RT
				float3 bendDir = cameraTransformForwardWS;
				bendDir.xz *= 0.5;//草弯曲的时候变得更矮
				bendDir.y = min(-0.5, bendDir.y);//阻止贴的太近
				positionOS = lerp(positionOS.xyz + bendDir * positionOS.y / - bendDir.y, positionOS.xyz, stepped * 0.95 + 0.05);//不能完全弯曲会产生 ZFighting
				
				//每颗草的高度
				positionOS.y *= perGrassHeight;
				
				//和摄像机的距离过远 进行适当的放大  避免三角形过小进行闪烁
				float3 viewWS = _WorldSpaceCameraPos - perGrassPivotPosWS;
				float viewWSLength = length(viewWS);
				positionOS += cameraTransformRightWS * max(0, viewWSLength * 0.0225);
				
				//如果超出了绘制距离  则进行光栅化优化   移到很远的地方  进行trinagle优化
				positionOS += viewWSLength < _DrawDistance?0: 999999;//移到很远的地方
				
				//移动草posOS -> posWS
				float3 positionWS = positionOS + perGrassPivotPosWS;
				
				//wind animation
				float wind = 0;
				wind += (sin(_Time.y * _WindAFrequency + perGrassPivotPosWS.x * _WindATiling.x + perGrassPivotPosWS.z * _WindATiling.y) * _WindAWrap.x + _WindAWrap.y) * _WindAIntensity; //windA
				wind += (sin(_Time.y * _WindBFrequency + perGrassPivotPosWS.x * _WindBTiling.x + perGrassPivotPosWS.z * _WindBTiling.y) * _WindBWrap.x + _WindBWrap.y) * _WindBIntensity; //windB
				wind += (sin(_Time.y * _WindCFrequency + perGrassPivotPosWS.x * _WindCTiling.x + perGrassPivotPosWS.z * _WindCTiling.y) * _WindCWrap.x + _WindCWrap.y) * _WindCIntensity; //windC
				wind *= input.vertex.y;//越高偏离越强
				float3 windOffset = cameraTransformRightWS * wind;//使用平面板效果 左右方向偏移
				positionWS.xyz += windOffset;
				
				o.position = TransformWorldToHClip(positionWS);
				
				/////////////////////////////////////////////////////////////////////
				//lighting & color
				/////////////////////////////////////////////////////////////////////
				
				//lighting data
				Light mainLight;
				#if _MAIN_LIGHT_SHADOWS
					mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
				#else
					mainLight = GetMainLight();
				#endif
				
				half3 randomAddToN = (_RandomNormal * sin(instanceID) + wind * - 0.25) * cameraTransformRightWS;//让每个草都是随机化发现
				half3 N = normalize(half3(0, 1, 0) + randomAddToN - cameraTransformForwardWS * 0.5);
				
				half3 V = viewWS / viewWSLength;//view normalize
				half3 albedo = lerp(_GroundColor, _BaseColor, input.vertex.y);//这里也可以用贴图
				
				//零阶段球谐 indirect
				half3 lightingResult = SampleSH(0) * albedo;
				
				//mainLight direct
				lightingResult += ApplySingleDirectLight(mainLight, N, V, albedo, positionOS.y);
				
				//Additional lights loop
				
				#if _ADDITIONAL_LIGHTS
					
					int additionalLightsCount = GetAdditionalLightsCount();
					
					for (int i = 0; i < additionalLightsCount; ++ i)
					{
						Light light = GetAdditionalLight(i, positionWS);
						
						lightingResult += ApplySingleDirectLight(light, N, V, albedo, positionOS.y);
					}
					
				#endif
				
				//fog
				float fogFactor = ComputeFogFactor(o.position.z);
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
