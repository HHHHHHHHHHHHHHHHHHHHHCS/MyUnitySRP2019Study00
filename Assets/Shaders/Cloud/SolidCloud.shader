Shader "MyRP/Cloud/SolidCloud"
{
	Properties
	{

	}
	SubShader
	{
		Pass
		{
			ZTest Always
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#pragma multi_compile CLOUD_SUN_SHADOWS_ON
			#pragma multi_compile CLOUD_DISTANCE_ON
			#pragma multi_compile CLOUD_AREA_BOX CLOUD_AREA_SPHERE //default CLOUD_AREA_BOX

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct a2v
			{
				// float4 vertex: POSITION;
				// float2 uv: TEXCOORD0;
				uint vertexID:SV_VERTEXID;
			};

			struct v2f
			{
				float4 pos: SV_POSITION;
				float2 uv: TEXCOORD0;
				// float3 camDir:TEXCOORD1;
			};

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
			// TEXTURE2D(_SunDepthTexture);
			// float4 _SunDepthTexture_TexelSize;
			// float4x4 _SunProj;
			// float4 _SunWorldPos;
			// half4 _SunShadowsData;
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


			float3 GetShadowCoords(float3 worldPos)
			{
				// shadowCoord = TransformWorldToShadowCoord(worldPos);
				// float4 shadowClipPos = mul(_SunProj, float4(worldPos, 1.0));
				// // transform from clip to texture space
				// shadowClipPos.xy /= shadowClipPos.w;
				// shadowClipPos.xy *= 0.5;
				// shadowClipPos.xy += 0.5;
				// shadowClipPos.z = 0;
				// return shadowClipPos.xyz;
				return 0;
			}

			half4 GetFogColor(float2 uv, float3 worldPos, float depth01, float dither)
			{
				const half4 zeros = half4(0.0, 0.0, 0.0, 0.0);
				const float3 wsCameraPos = _WorldSpaceCameraPos;

				// early exit if fog is not crossed
				if ((wsCameraPos.y > _CloudData.y && worldPos.y > _CloudData.y) ||
					(wsCameraPos.y < -_CloudData.y && worldPos.y < -_CloudData.y))
				{
					return zeros;
				}


				// Determine "fog length" and initial ray position between object and camera, cutting by fog distance params
				float4 adir = float4(worldPos - wsCameraPos, 0);
				adir.w = length(adir.xyz);

				#if CLOUD_AREA_SPHERE
				// compute sphere intersection or early exit if ray does not sphere
				float3 oc = wsCameraPos - _CloudAreaPosition;
				float3 nadir = adir.xyz / adir.w;
				float b = dot(nadir, oc);
				float c = dot(oc, oc) - _CloudAreaData.y;
				float t = b * b - c;
				if (t >= 0) t = sqrt(t);
				float distanceToFog = max(-b - t, 0);
				float dist = min(adir.w, _CloudDistance.z);
				float t1 = min(-b + t, dist);
				float fogLength = t1 - distanceToFog;
				if (fogLength < 0) return zeros;
				float3 fogCeilingCut = wsCameraPos + nadir * distanceToFog;
				float2 areaData =  _CloudAreaData.xz;
				#else //if CLOUD_AREA_BOX
				// compute box intersectionor early exit if ray does not cross box
				float3 ro = _CloudAreaPosition - wsCameraPos;
				float3 invR = adir.w / adir.xyz;
				float3 boxmax = 1.0 / _CloudAreaData.xyz;
				float3 tbot = invR * (ro - boxmax);
				float3 ttop = invR * (ro + boxmax);
				//get max length
				float3 tmin = min(ttop, tbot);
				float2 tt0 = max(tmin.xx, tmin.yz);
				float distanceToCloud = max(tt0.x, tt0.y);
				distanceToCloud = max(distanceToCloud, 0);
				//get min length
				float3 tmax = max(ttop, tbot);
				tt0 = min(tmax.xx, tmax.yz);
				float t1 = min(tt0.x, tt0.y);
				float dist = min(adir.w, _CloudDistance.z); //得到距离
				t1 = min(t1, dist);
				float cloudLength = t1 - distanceToCloud; //计算碰撞情况
				if (cloudLength <= 0)
				{
					return zeros;
				}

				float3 cloudCeilingCut = wsCameraPos + distanceToCloud / invR;

				float2 areaData = _CloudAreaData.xz / _CloudData.w;
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

				// Extracted operations from ray-march loop for additional optimizations
				dir.xz *= _CloudData.w;
				float dirLength = _CloudData.y * _CloudData.z; // extracted from loop, dragged here.
				dir.y /= dirLength;
				float4 ft4 = float4(cloudCeilingCut.xyz, 0);
				ft4.xz += _CloudWindDir.xz;
				// apply wind speed and direction; already defined above if the condition is true
				ft4.xz *= _CloudData.w;
				ft4.y /= dirLength;

				// Jitter start to reduce banding on edges
				//		if (_CloudWindDir.w) {
				//			ft4.xyz += dir.xyz * tex2Dlod(_NoiseTex, float4(uv * 100.0, 0, 0)).aaa * _CloudWindDir.www;
				//		}


				//#if CLOUD_AREA_SPHERE || CLOUD_AREA_BOX
				float2 areaCenter = _CloudAreaPosition.xz + _CloudWindDir.xz;
				areaCenter *= _CloudData.w;
				//#endif

				#if CLOUD_DISTANCE_ON
				float2 camCenter = wsCameraPos.xz + _CloudWindDir.xz;
				camCenter *= _CloudData.w;
				#endif

				// Shadow preparation
				#if CLOUD_SUN_SHADOWS_ON
				// fogCeilingCut.y += _CloudData.x;
				// // reduce banding
				// dir.w += frac(dither);
				//
				// float3 shadowCoords0 = GetShadowCoords(fogCeilingCut);
				// float3 fogEndPos = fogCeilingCut.xyz +
				// 	fogLength * (1.0 + dither * _SunShadowsData.y) * adir.xyz / adir.w;
				// float3 shadowCoords1 = GetShadowCoords(fogEndPos);
				// // shadow out of range, exclude with a subtle falloff
				// _SunShadowsData.x *= saturate((_SunWorldPos.w - distanceToFog) / 35.0);
				// _SunShadowsData.w = 1.0 / dir.w;
				// // apply jitter to avoid banding
				// //			shadowCoords0 += (shadowCoords1 - shadowCoords0) * dither * _SunShadowsData.y;
				#endif

				// Ray-march
				half4 sum = zeros;
				half4 fgCol = zeros;

				for (; dir.w > 1; dir.w--, ft4.xyz += dir.xyz)
				{
					#if CLOUD_AREA_SPHERE
					float2 vd = (areaCenter - ft4.xz) * _CloudAreaData.x;
					float voidDistance = dot(vd, vd);
					if (voidDistance > 1) continue;
					half4 ng = tex2Dlod(_NoiseTex, ft4.xzww);
					ng.a -= abs(ft4.y) + voidDistance * _CloudAreaData.w - 0.3;

					#else //if CLOUD_AREA_BOX

					float2 vd = abs(areaCenter - ft4.xz) * areaData;
					float voidDistance = max(vd.x, vd.y);
					if (voidDistance > 1) continue;
					half4 ng = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, float2(ft4.x,1-ft4.z), 0);
					ng.a -= abs(ft4.y);
					#endif

					#if CLOUD_DISTANCE_ON
					float2 fd = camCenter - ft4.xz;
					float fdm = max(_CloudDistance.x - dot(fd, fd), 0) * _CloudDistance.y;
					ng.a -= fdm;
					#endif

					if (ng.a > 0)
					{
						fgCol = half4(_CloudColor.rgb * (1.0 - ng.a), ng.a * 0.4);

						#if CLOUD_SUN_SHADOWS_ON
						// float t = dir.w * _SunShadowsData.w;
						// float3 shadowCoords = lerp(shadowCoords1, shadowCoords0, t);
						// float4 sunDepthWorldPos = tex2Dlod(_SunDepthTexture, shadowCoords.xyzz);
						// float sunDepth = 1.0 / sunDepthWorldPos.r; //DecodeFloatRGBA(sunDepthWorldPos);
						// float3 curPos = lerp(fogEndPos, fogCeilingCut, t);
						// float sunDist = distance(curPos, _SunWorldPos.xyz);
						// float shadowAtten = saturate(sunDepth - sunDist);
						// ng.rgb *= lerp(1.0, shadowAtten, _SunShadowsData.x * sum.a);
						// fgCol *= lerp(1, shadowAtten, _SunShadowsData.z);
						#endif

						fgCol.rgb *= ng.rgb * fgCol.aaa;
						sum += fgCol * (1.0 - sum.a);
						if (sum.a > 0.99) break;
					}
				}

				// adds fog fraction to prevent banding due stepping on low densities
				//		sum += (fogLength >= dist) * (sum.a<0.99) * fgCol * (1.0-sum.a) * dir.w; // first operand not needed if dithering is enabled
				sum += (sum.a < 0.99) * fgCol * (1.0 - sum.a) * dir.w;
				sum *= _CloudColor.a;

				return sum;
			}


			v2f Vert(a2v v)
			{
				v2f o = (v2f)0;

				float4 vertex = GetFullScreenTriangleVertexPosition(v.vertexID);
				float2 uv = GetFullScreenTriangleTexCoord(v.vertexID);

				o.pos = vertex; //TransformObjectToHClip(vertex.xyz);
				o.uv = uv;
				// o.camDir = GetWorldSpacePosition(uv) - _WorldSpaceCameraPos;
				return o;
			}

			float4 _CameraDepthTexture_TexelSize;
			half4 Frag(v2f i):SV_TARGET
			{
				float depth = SampleSceneDepth(i.uv);
				// 因为来源是一个大三角形  所以这样子还是不准确 所以换下面的方法
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
	}
}