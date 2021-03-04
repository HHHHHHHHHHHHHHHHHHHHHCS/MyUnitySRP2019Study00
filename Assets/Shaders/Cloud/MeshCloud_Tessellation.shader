Shader "MyRP/Cloud/MeshCloud_Tessellation"
{
	Properties
	{
		_3DNoise ("3D Noise", 3D) = "white" { }
		_NoiseScale ("Noise Scale", Range(0.1, 2)) = 1
		_NoiseSmoothness ("Noise Smoothness", Range(1, 10)) = 2
		_Speed ("Speed", Range(0, 10)) = 2
		_CloudColorLight ("Cloud Color Light", Color) = (1, 1, 1, 1)
		_CloudColorDark ("Cloud Color Dark", Color) = (1, 1, 1, 1)
		_BackLightStrength ("Back Light Strength", Range(0, 1)) = 0.5
		_BackSssStrength ("Back SSS Strength", Range(0, 10)) = 1
		//_DepthFactor ("Depth Factor", Range(0,5)) = 1
		
		// _Thickness ("Thickness", Float) = 0
		// _ClipRate ("Clip Rate", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" "Queue" = "AlphaTest" "IgnoreProjector" = "True" }
		LOD 100
		
		Pass
		{
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag
			
			#pragma multi_compile_instancing
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			
			//因为这种云是可以烘焙的  如果需要可以加上lightmapuUV
			struct a2v
			{
				float4 vertex: POSITION;
				float4 normal: NORMAL;
				// float2 uv: TEXCOORD0;
				// float2 lightmapUV: TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2g
			{
				float4 vertex: SV_POSITION;
				// float2 lightmapUV: TEXCOORD1;
				float3 worldNormal: TEXCOORD2;
				float distFade: TEXCOORD3;
				// DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct g2f
			{
				float4 pos: SV_POSITION;
				float3 worldPos: TEXCOORD0;
				float4 uv: TEXCOORD1;
				float3 worldNormal: TEXCOORD2;
				float2 fogFactorAndClipRate: TEXCOORD3;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					float4 shadowCoord: TEXCOORD4;
				#endif

				// DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
				// half4 fogFactorAndVertexLight: TEXCOORD4; // x: fogFactor, yzw: vertex light
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			TEXTURE3D(_3DNoise);
			SAMPLER(sampler_3DNoise);
			float _NoiseSmoothness;
			float _NoiseScale;
			float _Speed;
			half3 _CloudColorLight;
			half3 _CloudColorDark;
			float _BackLightStrength;
			float _BackSssStrength;
			
			uint _Count;
			float _Thickness[10];
			float _ClipRate[10];
			
			
			v2g vert(a2v v)
			{
				v2g o = (v2g)0;
				
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				
				o.vertex = v.vertex;
				o.worldNormal = TransformObjectToWorldNormal(v.normal.xyz);
				
				float4 pos = TransformObjectToHClip(v.vertex.xyz);
				
				//nowZ/far=z占比
				float distFade = (pos.w / _ProjectionParams.z) * 100;
				o.distFade = clamp(distFade, 1, 5);
				
				// OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, o.lightmapUV);
				// OUTPUT_SH(o.worldNormal.xyz, o.vertexSH);
				
				return o;
			}
			
			
			[maxvertexcount(60)]// or 60
			void geom(triangle v2g vertexs[3], inout TriangleStream < g2f > TriStream)
			{
				g2f o = (g2f)0;
				
				for (uint i = 0; i < _Count; i ++)
				{
					[unroll]
					for (uint j = 0; j < 3; j ++)
					{
						v2g v = vertexs[j];
						
						UNITY_TRANSFER_INSTANCE_ID(v, o);
						
						o.worldNormal = v.worldNormal.xyz;
						
						float4 vertex = v.vertex;
						
						float flowOffset = sin((v.vertex.x + v.vertex.y + v.vertex.z));
						//这里用(顶点位置-零点位置) 作为外扩的方向
						vertex.xyz += normalize(vertex.xyz) * (_Thickness[i] * v.distFade + flowOffset);
						
						o.worldPos = TransformObjectToWorld(vertex.xyz);
						
						o.pos = TransformWorldToHClip(o.worldPos);
						
						o.uv.xyz = vertex.xyz * 0.5 + 0.5;
						
						#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
							o.shadowCoord = TransformWorldToShadowCoord(o.worldPos);
						#endif
						
						
						o.fogFactorAndClipRate.x = ComputeFogFactor(o.pos.z);
						o.fogFactorAndClipRate.y = _ClipRate[i];
						
						
						TriStream.Append(o);
					}
					TriStream.RestartStrip();
				}
			}
			
			half4 frag(g2f i): SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				
				i.worldNormal = SafeNormalize(i.worldNormal);
				float4 shadowCoord = float4(0, 0, 0, 0);
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					shadowCoord = i.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					shadowCoord = TransformWorldToShadowCoord(i.worldPos);
					// #else
					// 	i.shadowCoord = float4(0, 0, 0, 0);
				#endif
				
				half clipRate = i.fogFactorAndClipRate.y;
				clipRate = PositivePow(clipRate, _NoiseSmoothness);
				
				//Light
				half3 viewDir = SafeNormalize(TransformWorldToViewDir(i.worldPos));
				Light mainLight = GetMainLight(shadowCoord);
				half3 lightDir = mainLight.direction;
				
				half NdotL = max(0, dot(i.worldNormal, lightDir));
				half smoothNdotL = saturate(pow(NdotL, 2 - clipRate));
				
				half NdotV = max(0, dot(i.worldNormal, viewDir));
				half smoothNdotV = saturate(pow(NdotV, 2 - clipRate));
				
				//SSS 越边缘效果越强
				half3 backLitDir = i.worldNormal * _BackSssStrength + lightDir;
				half backSSS = saturate(dot(viewDir, -backLitDir));
				backSSS = saturate(pow(backSSS, 2 + clipRate * 2) * 1.5);
				
				//3D Noise
				//越远UV 变动越小
				half distFade = 0.2 / clamp(PositivePow((i.pos.w / _ProjectionParams.z), 0.2), 1, 10);
				half3 flowUV = i.uv.xyz / _NoiseScale * distFade + _Time.x * _Speed;
				half noise = SAMPLE_TEXTURE3D(_3DNoise, sampler_3DNoise, flowUV).x;
				
				clip(noise - clipRate - frac((sin(i.worldPos.x + i.worldPos.y) * 99 + 11) * 99) * 0.1);
				
				half3 lightCol = lerp(mainLight.color, _CloudColorLight, 0.5);
				
				//Shadow Far Distance Fade
				//太远就不要阴影了
				half shadow = saturate(lerp(mainLight.shadowAttenuation, 1.0, (distance(i.worldPos.xyz, _WorldSpaceCameraPos.xyz) - 100) * 0.1));
				half finalLit = saturate(smoothNdotV * 0.5 + shadow * saturate(smoothNdotL + backSSS) * (1 - NdotV * 0.5));
				
				//Final Color
				half4 finalCol = half4(0, 0, 0, 1);
				finalCol.rgb = lerp(_CloudColorDark, lightCol, finalLit);// *atten
				finalCol.rgb = MixFog(finalCol.rgb, i.fogFactorAndClipRate.x);
				
				return finalCol;
			}
			
			ENDHLSL
			
		}
		/*
		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile_instancing
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			
			struct a2v
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
				float4 normal: NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 pos: SV_POSITION;
				float3 worldPos: TEXCOORD0;
				float4 uv: TEXCOORD1;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			TEXTURE3D(_3DNoise);
			SAMPLER(sampler_3DNoise);
			float _NoiseSmoothness;
			float _NoiseScale;
			float _Speed;
			
			float3 _LightDirection;
			
			
			UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_DEFINE_INSTANCED_PROP(float, _Thickness)
			UNITY_DEFINE_INSTANCED_PROP(float, _ClipRate)
			UNITY_INSTANCING_BUFFER_END(Props)
			
			
			v2f vert(a2v v)
			{
				v2f o = (v2f)0;
				
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				
				o.pos = TransformObjectToHClip(v.vertex.xyz);
				
				//nowZ/far=z占比
				float distFade = (o.pos.w / _ProjectionParams.z) * 100;
				distFade = clamp(distFade, 1, 5);
				
				float flowOffset = sin((v.vertex.x + v.vertex.y + v.vertex.z));
				v.vertex.xyz += v.normal.xyz * (UNITY_ACCESS_INSTANCED_PROP(Props, _Thickness) * distFade + flowOffset);
				
				o.uv.xyz = v.vertex.xyz * 0.5 + 0.5;
				
				o.worldPos = TransformObjectToWorld(v.vertex.xyz);
				
				float3 worldNormal = TransformObjectToWorldNormal(v.normal.xyz);
				
				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(o.worldPos, worldNormal, _LightDirection));
				
				#if UNITY_REVERSED_Z
					positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
				#else
					positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
				#endif
				
				o.pos = positionCS;
				
				return o;
			}
			
			half4 frag(v2f i): SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				
				half clipRate = UNITY_ACCESS_INSTANCED_PROP(Props, _ClipRate);
				clipRate = PositivePow(clipRate, _NoiseSmoothness);
				
				//3D Noise
				//越远UV 变动越小
				half distFade = 0.2 / clamp(PositivePow((i.pos.w / _ProjectionParams.z), 0.2), 1, 10);
				half3 flowUV = i.uv.xyz / _NoiseScale * distFade + _Time.x * _Speed;
				half noise = SAMPLE_TEXTURE3D(_3DNoise, sampler_3DNoise, flowUV).x;
				
				clip(noise - clipRate - frac((sin(i.worldPos.x + i.worldPos.y) * 99 + 11) * 99) * 0.1);
				
				return 0;
			}
			
			
			ENDHLSL
			
		}
		
		
		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }
			
			ColorMask 0
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile_instancing
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			
			struct a2v
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
				float4 normal: NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 pos: SV_POSITION;
				float3 worldPos: TEXCOORD0;
				float4 uv: TEXCOORD1;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			TEXTURE3D(_3DNoise);
			SAMPLER(sampler_3DNoise);
			float _NoiseSmoothness;
			float _NoiseScale;
			float _Speed;
			
			float3 _LightDirection;
			
			
			UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_DEFINE_INSTANCED_PROP(float, _Thickness)
			UNITY_DEFINE_INSTANCED_PROP(float, _ClipRate)
			UNITY_INSTANCING_BUFFER_END(Props)
			
			
			v2f vert(a2v v)
			{
				v2f o = (v2f)0;
				
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				
				o.pos = TransformObjectToHClip(v.vertex.xyz);
				
				//nowZ/far=z占比
				float distFade = (o.pos.w / _ProjectionParams.z) * 100;
				distFade = clamp(distFade, 1, 5);
				
				float flowOffset = sin((v.vertex.x + v.vertex.y + v.vertex.z));
				v.vertex.xyz += v.normal.xyz * (UNITY_ACCESS_INSTANCED_PROP(Props, _Thickness) * distFade + flowOffset);
				
				o.uv.xyz = v.vertex.xyz * 0.5 + 0.5;
				
				o.worldPos = TransformObjectToWorld(v.vertex.xyz);
				
				float3 worldNormal = TransformObjectToWorldNormal(v.normal.xyz);
				
				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(o.worldPos, worldNormal, _LightDirection));
				
				#if UNITY_REVERSED_Z
					positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
				#else
					positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
				#endif
				
				o.pos = positionCS;
				
				return o;
			}
			
			half4 frag(v2f i): SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				
				half clipRate = UNITY_ACCESS_INSTANCED_PROP(Props, _ClipRate);
				clipRate = PositivePow(clipRate, _NoiseSmoothness);
				
				//3D Noise
				//越远UV 变动越小
				half distFade = 0.2 / clamp(PositivePow((i.pos.w / _ProjectionParams.z), 0.2), 1, 10);
				half3 flowUV = i.uv.xyz / _NoiseScale * distFade + _Time.x * _Speed;
				half noise = SAMPLE_TEXTURE3D(_3DNoise, sampler_3DNoise, flowUV).x;
				
				clip(noise - clipRate - frac((sin(i.worldPos.x + i.worldPos.y) * 99 + 11) * 99) * 0.1);
				
				return 0;
			}
			
			
			ENDHLSL
			
		}
		*/
	}
}
