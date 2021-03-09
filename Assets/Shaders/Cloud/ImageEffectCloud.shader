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
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			
			struct a2v
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			struct v2f
			{
				float4 pos: SV_POSITION;
				float2 uv: TEXCOORD0;
				float3 viewVector: TEXCOORD1;
			};
			
			
			// Textures
			TEXTURE3D_PARAM(_NoiseTex, sampler_NoiseTex);
			TEXTURE3D_PARAM(_DetailNoiseTex, sampler_DetailNoiseTex);
			TEXTURE3D_PARAM(_WeatherMap, sampler_WeatherMap);
			TEXTURE3D_PARAM(_BlueNoise, sampler_BlueNoise);
			
			// Shape settings
			float4 _Params;
			int3 _MapSize;
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
			float4 _ColA;
			float4 _ColB;
			
			// Animation settings
			float _TimeScale;
			float _BaseSpeed;
			float _DetailSpeed;
			
			// Debug settings:
			int _DebugViewMode; // 0 = off; 1 = shape tex; 2 = detail tex; 3 = weathermap
			int _DebugGreyscale;
			int _DebugShowAllChannels;
			float _DebugNoiseSliceDepth;
			float4 _DebugChannelWeight;
			float _DebugTileAmount;
			float _ViewerSize;
			
			float Remap(float v, float minOld, float maxOld, float minNew, float maxNew)
			{
				return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
			}
			
			float Remap01(float v, float low, float high)
			{
				return(v - low) / (high - low);
			}
			
			float2 SquareUV(float2 uv)
			{
				return uv.xy * _ScreenParams.xy / 1000.0;
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
				return(1 - g2) / (4 * PI * pow(1 + g2 - 2 * g * a, 1.5));
			}
			
			float Phase(float a)
			{
				float blend = 0.5;
				float hgBlend = HG(a, _PhaseParams.x) * (1 - blend) + hgBlend(a, -_PhaseParams.y) * blend;
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
				//TODO:
			}
			
			v2f vert(appdata v)
			{
				v2f o;
				o.pos = TransformObjectToHClip(v.vertex);
				o.uv = v.uv;
				// Camera space matches OpenGL convention where cam forward is -z. In unity forward is positive z.
				// (https://docs.unity3d.com/ScriptReference/Camera-cameraToWorldMatrix.html)
				float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
				o.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0));
				return o;
			}
			
			fixed4 frag(v2f i): SV_Target
			{
				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv);
				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDHLSL
			
		}
	}
}
