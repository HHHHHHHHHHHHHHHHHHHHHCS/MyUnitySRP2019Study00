Shader "MyRP/Cloud/RaymarchMeshCloud"
{
	Properties
	{
		[NoScaleOffset] _NoiseTex("Shape Noise Texture", 3D) = ""{}
		[NoScaleOffset] _NoiseDetail3D("Shape Detail Noise Texture", 3D) = ""{}
		[NoScaleOffset] _WeatherMap("Weather Map", 2D) = "white"{}
		[NoScaleOffset] _MaskNoise("Mask Noise", 2D) = "white"{}
		[NoScaleOffset] _BlueNoise("Blue Noise", 2D) = "white"{}

		[Header(Tiling)]_ShapeTiling("Shape Tiling", Float) = 0.01
		_DetailTiling("Detail Tiling", Float) = 0.1

		[Header(Lighting)]_ColA("Color A", Color) = (1, 1, 1, 1)
		_ColB("Color B", Color) = (1, 1, 1, 1)
		_ColorOffset1("Color Offset 1", Float) = 0.59
		_ColorOffset2("Color Offset 2", Float) = 1.02
		_LightAbsorptionTowardSun("Light Absorption Toward Sun", Float) = 0.1
		_LightAbsorptionThroughCloud("Light Absorption Through Cloud", Float) = 1.0
		_DarknessThreshold("Darkness Threshold", Range(0,1)) = 0.0
		_PhaseParams("Phase Params", Vector) = (0.72, 1.0, 0.5, 1.58)

		[Header(Density)]
		_DensityOffset("Density Offset", Float) = 4.02
		_DensityMultiplier("Density Multiplier", Float) = 2.31
		_Step("Step", Float) = 1.2
		_RayStep("Ray Step", Float) = 1.2
		_RayOffsetStrength("Ray Offset Strength", Float) = 1.5
		_HeightWeights("Height Weights", Range(0, 1)) = 1
		_ShapeNoiseWeights("Shape Noise Weights", Vector) = (-0.17, 27.17, -3.65, -0.08)
		_DetailWeights("Detail Weights", Float) = -3.76
		_DetailNoiseWeight("Detail Noise Weight", Float) = 0.12
		_DetailNoiseWeights("Detail Noise Weights", Vector) = (-0.17, 27.17, -3.65, -0.08)
		_XY_Speed_ZW_Warp("XY Speed ZW Warp", Vector) = (0.05, 1, 1, 10)


	}
	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
			"RenderPipeline" = "UniversalPipeline"
		}

		Pass
		{
			Name "RaymarchMeshCloud"
			Tags
			{
				"LightMode" = "UniversalForward"
			}


			ZWrite Off
			ZTest LEqual
			Blend One OneMinusSrcAlpha
			Cull Back //Off

			HLSLPROGRAM
			#pragma vertex DefaultVert
			#pragma fragment FragCloud

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


			struct a2v
			{
				float4 vertex: POSITION;
			};

			struct v2f
			{
				float4 pos: SV_POSITION;
			};


			TEXTURE3D(_NoiseTex);
			SAMPLER(sampler_NoiseTex);
			TEXTURE3D(_NoiseDetail3D);
			SAMPLER(sampler_NoiseDetail3D);
			TEXTURE2D(_WeatherMap);
			SAMPLER(sampler_WeatherMap);
			TEXTURE2D(_MaskNoise);
			SAMPLER(sampler_MaskNoise);
			TEXTURE2D(_BlueNoise);
			SAMPLER(sampler_BlueNoise);

			CBUFFER_START(UnityPerMaterial)
			float4 _BlueNoise_TexelSize;

			float _Step;
			float _RayStep;
			float _RayOffsetStrength;
			float _ShapeTiling;
			float _DetailTiling;

			float _DensityOffset;
			float _DensityMultiplier;
			float4 _ShapeNoiseWeights;
			float _DetailWeights;
			float _DetailNoiseWeight;
			float4 _DetailNoiseWeights;

			float _LightAbsorptionTowardSun;
			float _LightAbsorptionThroughCloud;
			float _DarknessThreshold;
			float3 _ColA;
			float3 _ColB;
			float _ColorOffset1;
			float _ColorOffset2;
			float4 _PhaseParams;
			float _HeightWeights;
			float4 _XY_Speed_ZW_Warp;

			CBUFFER_END

			float3 _BoundsMin;
			float3 _BoundsMax;

			float Remap(float v, float minOld, float maxOld, float minNew, float maxNew)
			{
				return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
			}

			// Henyey-Greenstein
			float HG(float a, float g)
			{
				float g2 = g * g;
				return (1 - g2) / (4 * 3.1415 * PositivePow(1 + g2 - 2 * g * (a), 1.5));
			}

			float Phase(float a)
			{
				float blend = 0.5;
				float hgBlend = HG(a, _PhaseParams.x) * (1 - blend) + HG(a, -_PhaseParams.y) * blend;
				return _PhaseParams.z + hgBlend * _PhaseParams.w;
			}

			inline float Beer(float d)
			{
				return exp(-d);
			}

			// case 1: 射线从外部相交 (0 <= dstA <= dstB)
			// dstA是dst到最近的交叉点，dstB dst到远交点
			// case 2: 射线从内部相交 (dstA < 0 < dstB)
			// dstA是dst在射线后相交的, dstB是dst到正向交集
			// case 3: 射线没有相交 (dstA > dstB)
			//边界框最小值       边界框最大值      世界相机位置      反向世界空间光线方向   
			float2 RayBoxDst(float3 boundsMin, float3 boundsMax,
			                 float3 rayOrigin, float3 invRaydir)
			{
				float3 t0 = (boundsMin - rayOrigin) * invRaydir;
				float3 t1 = (boundsMax - rayOrigin) * invRaydir;
				float3 tmin = min(t0, t1);
				float3 tmax = max(t0, t1);

				float dstA = max(max(tmin.x, tmin.y), tmin.z); //进入点
				float dstB = min(tmax.x, min(tmax.y, tmax.z)); //出去点

				float dstToBox = max(0, dstA);
				float dstInsideBox = max(0, dstB - dstToBox);
				return float2(dstToBox, dstInsideBox);
			}


			//计算世界空间坐标
			float4 GetWorldSpacePosition(float depth, float2 uv)
			{
				//URP 7.5之后 Inv_VP 会翻转
				// 屏幕空间 --> 世界空间
				#if  VERSION_GREATER_EQUAL(7,5)
					float4x4 invVP = UNITY_MATRIX_I_VP;
					invVP._12_22_32_42 = -invVP._12_22_32_42;
					float4 world_vector = mul(invVP, float4(2.0 * uv - 1.0, depth, 1.0));
				#else
					float4 world_vector = mul(UNITY_MATRIX_I_VP, float4(2.0 * uv - 1.0, depth, 1.0));
				#endif
				world_vector.xyzw /= world_vector.w;
				return world_vector;
			}

			float SampleDensity(float3 rayPos)
			{
				float3 boundsCenter = (_BoundsMax + _BoundsMin) * 0.5;
				float3 size = _BoundsMax - _BoundsMin;

				float time = 0 * _Time.y;
				float speedShape = time * _XY_Speed_ZW_Warp.x;
				float speedDetail = time * _XY_Speed_ZW_Warp.y;

				float3 uvwShape = rayPos * _ShapeTiling + float3(speedShape, speedShape * 0.2, 0);
				float3 uvwDetail = rayPos * _DetailTiling + float3(speedDetail, speedDetail * 0.2, 0);

				float2 uv = (size.xz * 0.5f + (rayPos.xz - boundsCenter.xz)) / max(size.x, size.z);
				float weatherMap = SAMPLE_TEXTURE2D_LOD(_WeatherMap, sampler_WeatherMap
				                                        , uv + float2(speedShape * 0.4, 0), 0).r;

				//边缘衰减
				const float containerEdgeFadeDst = 10;
				float dstFromEdgeX = min(containerEdgeFadeDst, min(rayPos.x - _BoundsMin.x, _BoundsMax.x - rayPos.x));
				float dstFromEdgeZ = min(containerEdgeFadeDst, min(rayPos.z - _BoundsMin.z, _BoundsMax.z - rayPos.z));
				float edgeWeight = min(dstFromEdgeZ, dstFromEdgeX) / containerEdgeFadeDst;

				float gMin = Remap(weatherMap, 0, 1, 0.1, 0.6);
				float gMax = Remap(weatherMap, 0, 1, gMin, 0.9);


				float heightPercent = (rayPos.y - _BoundsMin.y) / size.y;
				float heightGradient = saturate(Remap(heightPercent, 0.0, gMin, 0, 1))
					* saturate(Remap(heightPercent, 1, gMax, 0, 1));
				float heightGradient2 = saturate(Remap(heightPercent, 0.0, weatherMap, 1, 0))
					* saturate(Remap(heightPercent, 0.0, gMin, 0, 1));
				heightGradient = saturate(lerp(heightGradient, heightGradient2, _HeightWeights));
				heightGradient *= edgeWeight;

				float4 normalizedShapeWeights = _ShapeNoiseWeights / dot(_ShapeNoiseWeights, float4(1, 1, 1, 1));
				float4 maskNoise = SAMPLE_TEXTURE2D_LOD(_MaskNoise, sampler_MaskNoise
				                                        , uv + float2(speedShape * 0.5, 0), 0);
				float4 shapeNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTex, sampler_NoiseTex
				                                         , uvwShape + (maskNoise.r * _XY_Speed_ZW_Warp.z * 0.1), 0);
				float shapeFBM = dot(shapeNoise, normalizedShapeWeights) * heightGradient;
				float baseShapeDensity = shapeFBM + _DensityOffset * 0.01;

				if (baseShapeDensity > 0)
				{
					float4 detailNoises = SAMPLE_TEXTURE3D_LOD(_NoiseDetail3D, sampler_NoiseDetail3D
					                                           , uvwDetail + (shapeNoise.r * _XY_Speed_ZW_Warp.w * 0.1)
					                                           , 0);
					float4 normalizedDetailWeights = _DetailNoiseWeights / dot(_DetailNoiseWeights, float4(1, 1, 1, 1));
					float detailNoise = dot(detailNoises, normalizedDetailWeights);
					float detailFBM = PositivePow(detailNoise, _DetailWeights);
					float oneMinusShape = 1 - baseShapeDensity;
					float detailErodeWeight = oneMinusShape * oneMinusShape * oneMinusShape;
					float cloudDensity = baseShapeDensity - detailFBM * detailErodeWeight * _DetailNoiseWeight;

					return saturate(cloudDensity * _DensityMultiplier);
				}
				else
				{
					return 0;
				}
			}


			float3 Lightmarch(float3 position)
			{
				float3 dirToLight = _MainLightPosition.xyz;

				//灯光方向与边界框求交，超出部分不计算
				float dstInsideBox = RayBoxDst(_BoundsMin, _BoundsMax, position, 1 / dirToLight).y;
				float stepSize = dstInsideBox / 8;
				float totalDensity = 0;

				for (int step = 0; step < 8; step++)
				{
					//灯光步进次数
					position += dirToLight * stepSize; //向灯光步进
					//totalDensity += max(0, sampleDensity(position) * stepSize);                     totalDensity += max(0, sampleDensity(position) * stepSize);
					totalDensity += max(0, SampleDensity(position));
				}
				float transmittance = Beer(totalDensity * _LightAbsorptionTowardSun);

				//将重亮到暗映射为 3段颜色 ,亮->灯光颜色 中->ColorA 暗->ColorB
				float3 cloudColor = lerp(_ColA, _MainLightColor.rgb, saturate(transmittance * _ColorOffset1));
				cloudColor = lerp(_ColB, cloudColor, saturate(pow(transmittance * _ColorOffset2, 3)));
				return _DarknessThreshold + transmittance * (1 - _DarknessThreshold) * cloudColor;
			}


			v2f DefaultVert(a2v v)
			{
				v2f o;

				o.pos = TransformObjectToHClip(v.vertex);

				return o;
			}

			half4 FragCloud(v2f i) : SV_Target
			{
				float2 uv = i.pos.xy / _ScreenParams.xy;

				_BoundsMin = -0.5 * UNITY_MATRIX_M._11_22_33 + UNITY_MATRIX_M._14_24_34;
				_BoundsMax = 0.5 * UNITY_MATRIX_M._11_22_33 + UNITY_MATRIX_M._14_24_34;

				float depth = SampleSceneDepth(uv);
				float3 rayPos = _WorldSpaceCameraPos;

				//世界空间坐标
				float4 worldPos = GetWorldSpacePosition(depth, uv);
				//世界空间相机方向
				float3 worldViewDir = normalize(worldPos.xyz - rayPos.xyz);


				//float depthEyeLinear = LinearEyeDepth(depth) ;
				float depthEyeLinear = length(worldPos.xyz - _WorldSpaceCameraPos);

				float2 rayToContainerInfo = RayBoxDst(_BoundsMin, _BoundsMax, rayPos, (1 / worldViewDir));
				float dstToBox = rayToContainerInfo.x; //相机到容器的距离
				float dstInsideBox = rayToContainerInfo.y; //返回光线是否在容器中

				// 与云云容器的交汇点
				float3 entryPoint = rayPos + worldViewDir * dstToBox;

				//相机到物体的距离 - 相机到容器的距离
				float dstLimit = min(depthEyeLinear - dstToBox, dstInsideBox);


				//添加抖动
				float4 blueNoiseCoords = float4(_ScreenParams.xy * _BlueNoise_TexelSize.xy, 0, 0);
				blueNoiseCoords.xy *= 0.0;
				float blueNoise = SAMPLE_TEXTURE2D(_BlueNoise, sampler_BlueNoise,
				                                   uv * blueNoiseCoords.xy + blueNoiseCoords.zw).r;

				//向灯光方向的散射更强一些
				float cosAngle = dot(worldViewDir, _MainLightPosition.xyz);
				float3 phaseVal = Phase(cosAngle);

				float dstTravelled = blueNoise.r * _RayOffsetStrength;
				float sumDensity = 1;
				float3 lightEnergy = 0;
				const float sizeLoop = 512;
				float stepSize = exp(_Step) * _RayStep;

				int j;
				for (j = 0; j < sizeLoop; j++)
				{
					if (dstTravelled < dstLimit)
					{
						rayPos = entryPoint + (worldViewDir * dstTravelled);
						float density = SampleDensity(rayPos);

						if (density > 0)
						{
							float3 lightTransmittance = Lightmarch(rayPos);
							lightEnergy += density * stepSize * sumDensity * lightTransmittance * phaseVal;
							sumDensity *= Beer(density * stepSize * _LightAbsorptionThroughCloud);

							if (sumDensity < 0.01)
							{
								break;
							}
						}
					}
					else
					{
						break;
					}
					dstTravelled += stepSize;
				}

				return float4(lightEnergy, 1 - sumDensity);
			}
			ENDHLSL
		}
	}
}