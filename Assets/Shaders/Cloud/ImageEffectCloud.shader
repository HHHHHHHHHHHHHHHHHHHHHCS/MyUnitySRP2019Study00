Shader "MyRP/Cloud/ImageEffectCloud"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" { }
	}
	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always
		
		Pass
		{
			Name "ImageEffectCloud"
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile_local _ _OnlyCloud
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			
			
			struct a2v
			{
				// float4 vertex: POSITION;
				// float2 uv: TEXCOORD0;
				uint vertexID: SV_VERTEXID;
			};
			
			struct v2f
			{
				float4 pos: SV_POSITION;
				float2 uv: TEXCOORD0;
				float3 viewVector: TEXCOORD1;
			};
			
			#define DEBUG_MODE 0
			
			// Textures
			TEXTURE3D(_NoiseTex);
			SAMPLER(sampler_NoiseTex);
			TEXTURE3D(_DetailNoiseTex);
			SAMPLER(sampler_DetailNoiseTex);
			
			// TEXTURE2D(_WeatherMap);
			// SAMPLER(sampler_WeatherMap);
			TEXTURE2D(_BlueNoise);
			SAMPLER(sampler_BlueNoise);
			
			
			// Shape settings
			// float4 _Params;
			// int3 _MapSize;
			float _DensityMultiplier;
			float _DensityOffset;
			float _Scale;
			float _DetailNoiseScale;
			float _DetailNoiseWeight;
			float3 _DetailWeights;
			float4 _ShapeNoiseWeights;
			float4 _PhaseParams;
			
			// March settings
			int _NumStepsLight;
			float _RayOffsetStrength;
			
			float3 _BoundsMin;
			float3 _BoundsMax;
			
			float3 _ShapeOffset;
			float3 _DetailOffset;
			
			// Light settings
			float _LightAbsorptionTowardSun;
			float _LightAbsorptionThroughCloud;
			float _DarknessThreshold;
			// float4 _ColA;
			// float4 _ColB;
			
			// Animation settings
			float _TimeScale;
			float _BaseSpeed;
			float _DetailSpeed;
			
			// Debug settings: 因为贴图在上面所以 这个要在后面定义
			#include "ImageEffectCloud_Debug.hlsl"
			
			float Remap(float v, float minOld, float maxOld, float minNew, float maxNew)
			{
				return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
			}
			
			/*
			float Remap01(float v, float low, float high)
			{
				return(v - low) / (high - low);
			}
			*/
			
			float2 SquareUV(float2 uv)
			{
				return frac(uv.xy * _ScreenParams.xy / 1000.0);
			}
			
			// Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
			// https://zhuanlan.zhihu.com/p/125834180
			float2 RayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRayDir)
			{
				// Adapted from: http://jcgt.org/published/0007/03/04/
				float3 t0 = (boundsMin - rayOrigin) * invRayDir;
				float3 t1 = (boundsMax - rayOrigin) * invRayDir;
				
				float3 tmin = min(t0, t1);
				float3 tmax = max(t0, t1);
				
				float dstA = max(max(tmin.x, tmin.y), tmin.z);
				float dstB = min(min(tmax.x, tmax.y), tmax.z);
				
				// CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
				// dstA is dst to nearest intersection, dstB dst to far intersection
				
				// CASE 2: ray intersects box from inside (dstA < 0 < dstB)
				// dstA is the dst to intersection behind the ray, dstB is dst to forward intersection
				
				// CASE 3: ray misses box (dstA > dstB)
				
				float dstToBox = max(0, dstA);
				float dstInsideBox = max(0, dstB - dstToBox);
				return float2(dstToBox, dstInsideBox);
			}
			
			// Henyey-Greenstein
			// https://zhuanlan.zhihu.com/p/34836881
			float HG(float a, float g)
			{
				float g2 = g * g;
				return(1 - g2) / (4 * PI * PositivePow(1 + g2 - 2 * g * a, 1.5));
			}
			
			float Phase(float a)
			{
				float blend = 0.5;
				float hgBlend = HG(a, _PhaseParams.x) * (1 - blend) + HG(a, -_PhaseParams.y) * blend;
				return _PhaseParams.z + hgBlend * _PhaseParams.w;
			}
			
			float Beer(float d)
			{
				float beer = exp(-d);
				return beer;
			}
			
			float SampleDensity(float3 rayPos)
			{
				// Constants:
				const int mipLevel = 0;
				const float baseScale = 1 / 1000.0;
				const float offsetSpeed = 1 / 100.0;
				
				// Calculate texture sample positions
				float time = _Time.x * _TimeScale;
				float3 size = _BoundsMax - _BoundsMin;
				float3 boundsCenter = (_BoundsMin + _BoundsMax) * 0.5;
				float3 uvw = (size * 0.5 + rayPos) * baseScale * _Scale;
				float3 shapeSamplePos = uvw + _ShapeOffset * offsetSpeed
				+ float3(time, time * 0.1, time * 0.2) * _BaseSpeed;
				
				
				// Calculate falloff at along x/z edges of the cloud container
				const float containerEdgeFadeDst = 50;
				float dstFromEdgeX = min(containerEdgeFadeDst, min(rayPos.x - _BoundsMin.x, _BoundsMax.x - rayPos.x));
				float dstFromEdgeZ = min(containerEdgeFadeDst, min(rayPos.z - _BoundsMin.z, _BoundsMax.z - rayPos.z));
				float edgeWeight = min(dstFromEdgeZ, dstFromEdgeX) / containerEdgeFadeDst;
				
				// Calculate height gradient from weather map
				//float2 weatherUV = (size.xz * .5 + (rayPos.xz-boundsCentre.xz)) / max(size.x,size.z);
				//float weatherMap = _WeatherMap.SampleLevel(sampler_WeatherMap, weatherUV, mipLevel).x;
				
				const float gMin = 0.2;
				const float gMax = 0.7;
				float heightPercent = (rayPos.y - _BoundsMin.y) / size.y;
				float heightGradient = saturate(Remap(heightPercent, 0.0, gMin, 0, 1))
				* saturate(Remap(heightPercent, 1, gMax, 0, 1));
				heightGradient *= edgeWeight;
				
				//Calculate base shape density
				float4 shapeNoise = _NoiseTex.SampleLevel(sampler_NoiseTex, shapeSamplePos, mipLevel);
				//形状的权重
				float4 normalizedShapeWeights = _ShapeNoiseWeights / dot(_ShapeNoiseWeights, float4(1, 1, 1, 1));
				//越高 shapeFBM越大
				float shapeFBM = dot(shapeNoise, normalizedShapeWeights) * heightGradient;
				float baseShapeDensity = shapeFBM + _DensityOffset * 0.1;
				
				//Save sampling from detail tex if shape density <=0
				if (baseShapeDensity > 0)
				{
					//Sample detail noise
					float3 detailSamplePos = uvw * _DetailNoiseScale + _DetailOffset * offsetSpeed
					+ float3(time * 0.4, -time, time * 0.1) * _DetailSpeed;
					float3 detailNoise = _DetailNoiseTex.SampleLevel(sampler_DetailNoiseTex, detailSamplePos, mipLevel).
					rgb;
					float3 normalizedDetailWeights = _DetailWeights / dot(_DetailWeights, float3(1, 1, 1));
					float detailFBM = dot(detailNoise, normalizedDetailWeights);
					
					// Subtract detail noise from base shape (weighted by inverse density so that edges get eroded more than centre)
					// shapeFBM 越大    越接近中心  侵蚀力度越小
					float oneMinusShape = 1 - shapeFBM;
					float detailErodeWeight = oneMinusShape * oneMinusShape * oneMinusShape;
					//detailFBM 越小 越容易被侵蚀
					float cloudDensity = baseShapeDensity - (1 - detailFBM) * detailErodeWeight * _DetailNoiseWeight;
					
					//可能出现负数  就没有效果
					return cloudDensity * _DensityMultiplier * 0.1;
				}
				else
				{
					return 0;
				}
			}
			
			// Calculate proportion of light that reaches the given point from the lightsource
			float LightMarch(float3 position)
			{
				float3 dirToLight = _MainLightPosition.xyz;
				float dstInsideBox = RayBoxDst(_BoundsMin, _BoundsMax, position, 1 / dirToLight).y;
				
				float stepSize = dstInsideBox / _NumStepsLight;
				float totalDensity = 0;
				
				for (int step = 0; step < _NumStepsLight; step ++)
				{
					position += dirToLight * stepSize;
					totalDensity += max(0.0, SampleDensity(position) * stepSize);
				}
				
				float transmittance = exp(-totalDensity * _LightAbsorptionTowardSun);
				// _DarknessThreshold 决定了 向光采样 的强度
				return _DarknessThreshold + transmittance * (1 - _DarknessThreshold);
			}
			
			v2f vert(a2v v)
			{
				v2f o;
				
				float4 vertex = GetFullScreenTriangleVertexPosition(v.vertexID);
				float2 uv = GetFullScreenTriangleTexCoord(v.vertexID);
				
				o.pos = vertex; //TransformObjectToHClip(vertex.xyz);
				o.uv = uv;
				
				// Camera space matches OpenGL convention where cam forward is -z. In unity forward is positive z.
				// (https://docs.unity3d.com/ScriptReference/Camera-cameraToWorldMatrix.html)
				float3 viewVector = mul(unity_CameraInvProjection, float4(uv * 2 - 1, 0, -1)).xyz;
				o.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;
				return o;
			}
			
			float4 frag(v2f i): SV_Target
			{
				DrawDebugView(i.uv);
				
				// Create ray
				float3 rayPos = _WorldSpaceCameraPos;
				float viewLength = length(i.viewVector);
				float3 rayDir = i.viewVector / viewLength;
				
				float3 backgroundCol = SampleSceneColor(i.uv);
				
				if ((rayDir.y <= 0.000001 && rayPos.y < _BoundsMin.y) ||
				(rayDir.y >= 0.000001 && rayPos.y > _BoundsMax.y))
				{
					//就算是 godray  但是alpha是0 所以没有关系
					return float4(backgroundCol, 1);
				}
				
				// Depth and cloud container intersection info:
				float2 rayToContainerInfo = RayBoxDst(_BoundsMin, _BoundsMax, rayPos, 1 / rayDir);
				float dstToBox = rayToContainerInfo.x;
				float dstInsideBox = rayToContainerInfo.y;
				
				float nonlin_depth = SampleSceneDepth(i.uv);
				//这里如果不乘viewLength  则四周区域会缺失/畸变
				float depth = LinearEyeDepth(nonlin_depth, _ZBufferParams) * viewLength;
				
				// Phase function makes clouds brighter around sun
				float cosAngle = dot(rayDir, _MainLightPosition.xyz);
				float phaseVal = Phase(cosAngle);
				
				// point of intersection with the cloud container
				float3 entryPoint = rayPos + rayDir * dstToBox;
				
				// random starting offset (makes low-res results noisy rather than jagged/glitchy, which is nicer)
				float randomOffset = _BlueNoise.SampleLevel(sampler_BlueNoise, SquareUV(i.uv * 3), 0).r;
				randomOffset *= _RayOffsetStrength;
				
				const float stepSize = 11;
				
				// March through volume:
				float transmittance = 1;
				float lightEnergy = 0;
				
				float dstTravelled = randomOffset;
				// depth = depth > _ProjectionParams.z - 1 ? 2 * dstToBox : depth;
				float dstLimit = min(depth - dstToBox, dstInsideBox);
				
				while(dstTravelled < dstLimit)
				{
					rayPos = entryPoint + rayDir * dstTravelled;
					float density = SampleDensity(rayPos);
					
					if (density > 0)
					{
						float lightTransmittance = LightMarch(rayPos);
						//视野方向的强度 * stepSize * 透射率 * 光方向强度 * 大气散射
						lightEnergy += density * stepSize * transmittance * lightTransmittance * phaseVal;
						//强度越高 透射率越低
						//Beer–Lambert  https://zhuanlan.zhihu.com/p/151851272
						transmittance *= Beer(density * stepSize * _LightAbsorptionThroughCloud);
						
						
						// Exit early if T is close to zero as further samples won't affect the result much
						//当透射率很小了 就退出去   因为颜色已经不怎么会改变了
						if (transmittance < 0.01)
						{
							break;
						}
					}
					dstTravelled += stepSize;
				}
				
				// Add clouds to background
				// float3 backgroundCol = SampleSceneColor(i.uv);
				float3 cloudCol = lightEnergy * _MainLightColor.rgb;
				
				#ifdef _OnlyCloud
					if(transmittance == 1)
					{
						return float4(backgroundCol, 0);
					}
					return float4(cloudCol,  transmittance);
				#else
					float3 col = backgroundCol * transmittance + cloudCol;
					return float4(col, 0);
				#endif
			}
			ENDHLSL
			
		}
	}
}