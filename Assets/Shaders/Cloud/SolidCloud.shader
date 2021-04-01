Shader "MyRP/Cloud/SolidCloud"
{
	Properties
	{

	}
	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

	struct a2v
	{
		uint vertexID:SV_VERTEXID;
	};

	struct v2f
	{
		float4 pos: SV_POSITION;
		float2 uv: TEXCOORD0;
	};

	v2f Vert(a2v v)
	{
		v2f o = (v2f)0;
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		o.uv = GetFullScreenTriangleTexCoord(v.vertexID);
		return o;
	}
	ENDHLSL

	SubShader
	{
		ZTest Always
		ZWrite Off

		//Cloud
		Pass
		{
			Blend One Zero//OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragCloud

			#pragma multi_compile MAIN_LIGHT_CALCULATE_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

			#pragma multi_compile _ CLOUD_SUN_SHADOWS_ON
			#pragma multi_compile _ CLOUD_DISTANCE_ON
			#pragma multi_compile _ CLOUD_AREA_SPHERE  //default CLOUD_AREA_BOX
			#pragma multi_compile _ CLOUD_USE_XY_PLANE

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


			TEXTURE2D(_NoiseTex);
			SAMPLER(sampler_NoiseTex);

			float4 _CloudDistance;
			float4 _CloudData; // x = _CloudBaseHeight, y = _CloudHeight, z = density, w = scale;
			float3 _CloudWindDir;
			float4 _CloudStepping;
			half4 _CloudColor;


			//#if CLOUD_AREA_SPHERE || CLOUD_AREA_BOX
			float3 _CloudAreaPosition; // xyz
			float4 _CloudAreaData;
			//#endif

			#if CLOUD_SUN_SHADOWS_ON
			half3 _SunShadowsData; //x:sunShadowsStrength  y:sunShadowsJitterStrength  z:sunShadowsCancellation
			#endif


			//计算世界空间坐标
			float3 GetWorldSpacePosition(float2 uv, float depth = 1.0)
			{
				// 屏幕空间 --> 世界空间
				const float4 world_vector = mul(UNITY_MATRIX_I_VP, float4(2.0 * uv - 1.0, depth, 1.0));
				return world_vector.xyz / world_vector.w;
			}

			inline float GetDither(float2 uv)
			{
				float dither = dot(float2(2.4084507, 3.2535211), uv * _ScreenParams.xy); // _MainTex_TexelSize.zw);
				dither = frac(dither) - 0.5;
				//		dither = frac(sin(dot(uv ,float2(12.9898,78.233))) * 43758.5453) - 0.5;
				return dither;
			}

			half4 GetFogColor(float2 uv, float3 worldPos, float depth01, float dither)
			{
				const half4 zeros = half4(0.0, 0.0, 0.0, 0.0);

				#if CLOUD_USE_XY_PLANE
				const float3 cloudAreaPosition = float3(_CloudAreaPosition.xy, 0);
				const float planeOffset = _CloudAreaPosition.z;
				const float3 wsCameraPos = float3(_WorldSpaceCameraPos.x
				                                  , _WorldSpaceCameraPos.y, _WorldSpaceCameraPos.z - planeOffset);
				worldPos.z -= planeOffset;
				if ((wsCameraPos.z > _CloudData.y && worldPos.z > _CloudData.y) ||
					(wsCameraPos.z < -_CloudData.y && worldPos.z < -_CloudData.y))
				{
					return zeros;
				}
				#else
				const float3 cloudAreaPosition = float3(_CloudAreaPosition.x, 0, _CloudAreaPosition.z);
				const float planeOffset = _CloudAreaPosition.y;
				const float3 wsCameraPos = float3(_WorldSpaceCameraPos.x
				                                  , _WorldSpaceCameraPos.y - planeOffset, _WorldSpaceCameraPos.z);
				worldPos.y -= planeOffset;
				// early exit if fog is not crossed
				if ((wsCameraPos.y > _CloudData.y && worldPos.y > _CloudData.y) ||
					(wsCameraPos.y < -_CloudData.y && worldPos.y < -_CloudData.y))
				{
					return zeros;
				}
				#endif


				// Determine "fog length" and initial ray position between object and camera, cutting by fog distance params
				float4 adir = float4(worldPos - wsCameraPos, 0);
				adir.w = length(adir.xyz);

				#if CLOUD_AREA_SPHERE
				// compute sphere intersection or early exit if ray does not sphere
				float3 oc = wsCameraPos - cloudAreaPosition;
				float3 nadir = adir.xyz / adir.w;
				float b = dot(nadir, oc);
				float c = dot(oc, oc) - _CloudAreaData.y;
				float t = b * b - c;
				if (t >= 0)
				{
					t = sqrt(t);
				}
				float distanceToCloud = max(-b - t, 0);
				float dist = min(adir.w, _CloudDistance.z);
				float t1 = min(-b + t, dist);
				float cloudLength = t1 - distanceToCloud;
				if (cloudLength < 0)
				{
					return zeros;
				}
				float3 cloudCeilingCut = wsCameraPos + nadir * distanceToCloud;
				float2 areaData = _CloudAreaData.xz;

				#else //if CLOUD_AREA_BOX

				// compute box intersectionor early exit if ray does not cross box
				float3 ro = cloudAreaPosition - wsCameraPos;
				float3 invR = adir.w / adir.xyz;
				float3 boxmax = 1.0 / _CloudAreaData.xyz;
				float3 tbot = invR * (ro - boxmax);
				float3 ttop = invR * (ro + boxmax);
				//get max length
				float3 tmin = min(ttop, tbot);
				float distanceToCloud = max(tmin.x, max(tmin.y, tmin.z));
				distanceToCloud = max(distanceToCloud, 0);
				//get min length
				float3 tmax = max(ttop, tbot);
				float t1 = min(tmax.x, min(tmax.y, tmax.z));
				float dist = min(adir.w, _CloudDistance.z); //得到距离
				t1 = min(t1, dist);
				float cloudLength = t1 - distanceToCloud; //计算碰撞情况
				if (cloudLength <= 0)
				{
					return zeros;
				}
				float3 cloudCeilingCut = wsCameraPos + distanceToCloud / invR;
				#if CLOUD_USE_XY_PLANE
				float2 areaData = _CloudAreaData.xy / _CloudData.w;
				#else
				float2 areaData = _CloudAreaData.xz / _CloudData.w;
				#endif
				#endif

				// 计算 每一步 ray march 方向长度
				float rs = 0.1 + max(log(cloudLength), 0) * _CloudStepping.x;
				// stepping ratio with atten detail with distance
				rs *= _CloudData.z; // prevents lag when density is too low
				rs *= saturate(dist * _CloudStepping.y);
				dist -= distanceToCloud;
				rs = max(rs, 0.01);

				float4 dir = float4(adir.xyz * rs / adir.w, cloudLength / rs); //raymarch 方向  和  次数
				//		dir.w = min(dir.w, 200);	// maximum iterations could be clamped to improve performance under some point of view, most of time got unnoticieable

				#if CLOUD_USE_XY_PLANE
				dir.xy *= _CloudData.w;
				float dirLength = _CloudData.y * _CloudData.z; // extracted from loop, dragged here.
				dir.z /= _CloudData.y;
				float4 ft4 = float4(cloudCeilingCut.xyz, 0);
				ft4.xy += _CloudWindDir.xz;
				// apply wind speed and direction; already defined above if the condition is true
				ft4.xy *= _CloudData.w;
				ft4.z /= dirLength;
				#else
				// Extracted operations from ray-march loop for additional optimizations
				dir.xz *= _CloudData.w;
				float dirLength = _CloudData.y * _CloudData.z; // extracted from loop, dragged here.
				dir.y /= dirLength;
				float4 ft4 = float4(cloudCeilingCut.xyz, 0);
				ft4.xz += _CloudWindDir.xz;
				// apply wind speed and direction; already defined above if the condition is true
				ft4.xz *= _CloudData.w;
				ft4.y /= dirLength;
				#endif


				// Jitter start to reduce banding on edges
				//		if (_CloudWindDir.w) {
				//			ft4.xyz += dir.xyz * tex2Dlod(_NoiseTex, float4(uv * 100.0, 0, 0)).aaa * _CloudWindDir.www;
				//		}


				#if CLOUD_USE_XY_PLANE
				// #if CLOUD_AREA_SPHERE || CLOUD_AREA_BOX
				float2 areaCenter = cloudAreaPosition.xy + _CloudWindDir.xy;
				areaCenter *= _CloudData.w;
				// #endif

				#if CLOUD_DISTANCE_ON
				float2 camCenter = wsCameraPos.xy + _CloudWindDir.xy;
				camCenter *= _CloudData.w;
				#endif
				#else
				//#if CLOUD_AREA_SPHERE || CLOUD_AREA_BOX
				float2 areaCenter = cloudAreaPosition.xz + _CloudWindDir.xz;
				areaCenter *= _CloudData.w;
				//#endif

				#if CLOUD_DISTANCE_ON
				float2 camCenter = wsCameraPos.xz + _CloudWindDir.xz;
				camCenter *= _CloudData.w;
				#endif

				#endif


				// Shadow preparation
				#if CLOUD_SUN_SHADOWS_ON
				#if CLOUD_USE_XY_PLANE
				cloudCeilingCut.z += planeOffset;
				#else
				cloudCeilingCut.y += planeOffset;
				#endif
				// reduce banding
				dir.w += frac(dither);
				half4 shadowData = half4(_SunShadowsData, 1.0 / dir.w);
				float4 shadowCoords0 = TransformWorldToShadowCoord(cloudCeilingCut);
				float3 fogEndPos = cloudCeilingCut.xyz +
					cloudLength * (1.0 + dither * shadowData.y) * adir.xyz / adir.w;
				float4 shadowCoords1 = TransformWorldToShadowCoord(fogEndPos);
				// shadow out of range, exclude with a subtle falloff
				// shadowData.x *= 1; //saturate((_SunWorldPos.w - distanceToFog) / 35.0);
				// apply jitter to avoid banding
				//			shadowCoords0 += (shadowCoords1 - shadowCoords0) * dither * _SunShadowsData.y;
				#endif

				// Ray-march
				half4 sum = zeros;
				half4 fgCol = zeros;


				for (; dir.w > 1; dir.w--, ft4.xyz += dir.xyz)
				{
					#if CLOUD_USE_XY_PLANE
					half4 ng = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, ft4.xy, 0);
					#else
					half4 ng = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, ft4.xz, 0);
					#endif


					#if CLOUD_AREA_SPHERE
					#if CLOUD_USE_XY_PLANE
					float2 vd = (areaCenter - ft4.xy) * areaData.x;
					#else
					float2 vd = (areaCenter - ft4.xz) * areaData.x;
					#endif
					float voidDistance = dot(vd, vd);
					//边缘
					if (voidDistance > 1)
					{
						break;
					}
					#if CLOUD_USE_XY_PLANE
					ng.a -= abs(ft4.z) + voidDistance * _CloudAreaData.w - 0.3;
					#else
					ng.a -= abs(ft4.y) + voidDistance * _CloudAreaData.w - 0.3;
					#endif
					#else //if CLOUD_AREA_BOX
					#if CLOUD_USE_XY_PLANE
					float2 vd = abs(areaCenter - ft4.xy) * areaData;
					#else
					float2 vd = abs(areaCenter - ft4.xz) * areaData;
					#endif
					float voidDistance = max(vd.x, vd.y);
					//边缘
					if (voidDistance > 1)
					{
						continue;
					}

					//四边形 >0.9 之后 变小

					#if CLOUD_USE_XY_PLANE
					ng.a -= abs(ft4.z);
					#else
					ng.a -= abs(ft4.y); //+ voidDistance * _CloudAreaData.w - 0.3;
					#endif
					#endif


					#if CLOUD_DISTANCE_ON
					#if CLOUD_USE_XY_PLANE
					float2 fd = camCenter - ft4.xy;
					#else
					float2 fd = camCenter - ft4.xz;
					#endif
					float fdm = max(_CloudDistance.x - dot(fd, fd), 0) * _CloudDistance.y;
					ng.a -= fdm;
					#endif

					// smoothstep(0.9,1,voidDistance)
					// if (ng.a * (abs(ft4.y) + 3)>1.0)
					// {
					// 	ng.a -= + voidDistance * _CloudAreaData.w - 0.3;
					// }

					if (ng.a > 0)
					{
						fgCol = half4(_CloudColor.rgb * (1.0 - ng.a), ng.a * 0.4);
						#if CLOUD_SUN_SHADOWS_ON
						float t = dir.w * shadowData.w;
						float4 shadowCoords = lerp(shadowCoords1, shadowCoords0, t);
						half shadowAtten = MainLightRealtimeShadow(shadowCoords);
						ng.rgb *= lerp(1.0, shadowAtten, shadowData.x * sum.a);
						fgCol *= lerp(1, shadowAtten, shadowData.z);
						#endif

						fgCol.rgb *= ng.rgb * fgCol.aaa;
						sum += fgCol * (1.0 - sum.a);

						if (sum.a > 0.99)
						{
							break;
						}
					}
				}


				// adds fog fraction to prevent banding due stepping on low densities
				// sum += (cloudLength >= dist) * (sum.a<0.99) * fgCol * (1.0-sum.a) * dir.w; // first operand not needed if dithering is enabled
				if (sum.a < 0.99)
				{
					sum += fgCol * (1.0 - sum.a) * dir.w;
				}
				sum *= _CloudColor.a;
				return sum;
			}


			half4 FragCloud(v2f i):SV_TARGET
			{
				float depth = SampleSceneDepth(i.uv);
				// 因为来源是一个大三角形  所以这样子还是不准确 所以换下面的方法
				//  VS :  o.camDir = GetWorldSpacePosition(uv) - _WorldSpaceCameraPos;
				// depth = Linear01Depth(depth,_ZBufferParams);
				// float3 wPos = _WorldSpaceCameraPos + i.camDir * depth;
				float3 wPos = GetWorldSpacePosition(i.uv, depth);

				float dither = GetDither(i.uv);

				half4 sum = GetFogColor(i.uv, wPos, depth, dither);
				sum *= 1.0 + dither * _CloudStepping.w;

				return half4(sum);
			}
			ENDHLSL
		}

		//generateNoise
		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragNoise

			TEXTURE2D(_NoiseTex);
			SAMPLER(sampler_NoiseTex);

			float _NoiseStrength;
			float _NoiseDensity;
			int _NoiseSize;
			int _NoiseCount;
			int _NoiseSeed;
			float3 _LightColor;
			float4 _SpecularColor;


			inline float GetAlpha(float2 uv)
			{
				const float alpha = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, uv, 0).r;
				return pow((1.0f - alpha * _NoiseStrength), _NoiseDensity);
				// return (1.0f - alpha * _NoiseStrength) * _NoiseDensity;
				// return smoothstep(0, 1, (1.0f - alpha * _NoiseStrength) * _NoiseDensity);
			}


			half4 FragNoise(v2f i):SV_TARGET
			{
				int k = i.pos.y * _NoiseSize + i.pos.x;
				int rd = (k + _NoiseSeed) % _NoiseCount;
				float a = GetAlpha(i.uv);
				float2 rdUV = float2(rd % _NoiseSize, floor(rd / _NoiseSize)) / _NoiseSize;
				float r = saturate((a - GetAlpha(rdUV)) * _SpecularColor.a);
				return half4(_LightColor.rgb + _SpecularColor.rgb * r, a);
			}
			ENDHLSL
		}

		//Random Noise
		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragNoise

			TEXTURE2D(_NoiseTex);
			SAMPLER(sampler_NoiseTex);
			float _Amount;

			half4 FragNoise(v2f i):SV_TARGET
			{
				float sint, cost;
				sincos(_Amount, sint, cost);
				half4 p0 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, i.uv, 0);
				half4 p1 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, i.uv + float2(0.25,0.25), 0);
				float t0 = (sint + 1.0) * 0.5;
				half4 r0 = lerp(p0, p1, t0);

				half4 p2 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, i.uv + float2(0.5,0.5), 0);
				half4 p3 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, i.uv + float2(0.75,0.75), 0);
				float t1 = (cost + 1.0) * 0.5;
				half4 r1 = lerp(p2, p3, t1);
				return max(r0, r1);
			}
			ENDHLSL
		}
	}
}