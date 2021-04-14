Shader "MyRP/Cloud/SolidCloud"
{
	Properties
	{
		//不能添加  会造成属性优先级问题
		//[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", int) = 0
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

		//0.Cloud
		Pass
		{
			Blend One [_DstBlend]//OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragCloud

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

			#pragma multi_compile_local _ CLOUD_USE_XY_PLANE
			#pragma multi_compile_local _ CLOUD_SUN_SHADOWS_ON
			#pragma multi_compile_local _ CLOUD_DISTANCE_ON
			#pragma multi_compile_local _ CLOUD_AREA_SPHERE  //default CLOUD_AREA_BOX
			#pragma multi_compile_local _ CLOUD_FRAME_ON


			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


			TEXTURE2D(_NoiseTex);
			SAMPLER(sampler_linear_repeat_NoiseTex);

			// #if CLOUD_MASK
			// TEXTURE2D(_MaskTex);
			// SAMPLER(sampler_MaskTex);
			// #endif

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

			#if CLOUD_FRAME_ON
			int _Frame;
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

			half4 GetSolidCloudColor(float2 uv, float3 worldPos, float depth01, float dither)
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
				// dist -= distanceToCloud;
				// rs = max(rs, 0.01); //基本很难触发


				float4 dir = float4(rs * adir.xyz / adir.w, cloudLength / rs); //raymarch 方向  和  次数
				// dir.w = min(dir.w, 200);	//最大步长限制
				// dir.xyz *= cloudLength / rs / dir.w;

				float dirLength = _CloudData.y * _CloudData.z; // extracted from loop, dragged here.
				float4 ft4 = float4(cloudCeilingCut.xyz, 0);

				#if CLOUD_USE_XY_PLANE
				
				dir.xy *= _CloudData.w;
				dir.z /= _CloudData.y;

				// apply wind speed and direction; already defined above if the condition is true
				ft4.xy = (ft4.xy + _CloudWindDir.xz) * _CloudData.w;
				ft4.z /= dirLength;
				
				#else

				// Extracted operations from ray-march loop for additional optimizations
				dir.xz *= _CloudData.w;
				dir.y /= dirLength;

				// apply wind speed and direction; already defined above if the condition is true
				ft4.xz = (ft4.xz + _CloudWindDir.xz) * _CloudData.w;
				ft4.y /= dirLength;

				#endif


				// Jitter start to reduce banding on edges
				//		if (_CloudWindDir.w) {
				//			ft4.xyz += dir.xyz * tex2Dlod(_NoiseTex, float4(uv * 100.0, 0, 0)).aaa * _CloudWindDir.www;
				//		}


				#if CLOUD_USE_XY_PLANE

				// #if CLOUD_AREA_SPHERE || CLOUD_AREA_BOX
				float2 areaCenter = (cloudAreaPosition.xy + _CloudWindDir.xy) * _CloudData.w;
				// #endif

				#if CLOUD_DISTANCE_ON
				float2 camCenter = wsCameraPos.xy + _CloudWindDir.xy;
				camCenter *= _CloudData.w;
				#endif

				#else

				//#if CLOUD_AREA_SPHERE || CLOUD_AREA_BOX
				float2 areaCenter = (cloudAreaPosition.xz + _CloudWindDir.xz) * _CloudData.w;
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

				dir.w += frac(dither);
				half4 shadowData = half4(_SunShadowsData, 1.0 / dir.w);
				
				#if _MAIN_LIGHT_SHADOWS_CASCADE
					float3 startWPos = cloudCeilingCut.xyz;
					float3 endWPos = cloudCeilingCut.xyz +
						cloudLength * (1.0 + dither * shadowData.y) * adir.xyz / adir.w;
				#else
					// reduce banding
					float4 shadowCoords0 = TransformWorldToShadowCoord(cloudCeilingCut);
					float3 cloudEndPos = cloudCeilingCut.xyz +
						cloudLength * (1.0 + dither * shadowData.y) * adir.xyz / adir.w;
					float4 shadowCoords1 = TransformWorldToShadowCoord(cloudEndPos);
					// shadow out of range, exclude with a subtle falloff
					// shadowData.x *= 1; //saturate((_SunWorldPos.w - distanceToFog) / 35.0);
					// apply jitter to avoid banding
					//			shadowCoords0 += (shadowCoords1 - shadowCoords0) * dither * _SunShadowsData.y;
				#endif

				#endif

				// Ray-march
				half4 sum = zeros;
				half4 cloudCol = zeros;
				float2 pos, h;

				for (; dir.w > 1; dir.w --, ft4.xyz += dir.xyz)
				{
					#if CLOUD_USE_XY_PLANE
					pos = ft4.xy;
					h = ft4.z;
					#else
					pos = ft4.xz;
					h = ft4.y;
					#endif

					half4 ng = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_linear_repeat_NoiseTex, pos, 0);

					#if CLOUD_AREA_SPHERE
					
					float2 vd = (areaCenter - pos) * areaData.x;
					float voidDistance = dot(vd, vd);
					//边缘
					if (voidDistance > 1)
					{
						break;
					}
					ng.a -= abs(h) + voidDistance * _CloudAreaData.w - 0.3;
					
					#else //if CLOUD_AREA_BOX

					float2 vd = abs(areaCenter - pos) * areaData;
					float voidDistance = max(vd.x, vd.y);
					//边缘
					if (voidDistance > 1)
					{
						continue;
					}
					ng.a -= abs(h) + smoothstep(1 - _CloudAreaData.w, 1, voidDistance);

					#endif


					#if CLOUD_DISTANCE_ON
					float2 fd = camCenter - pos;
					float fdm = max(_CloudDistance.x - dot(fd, fd), 0) * _CloudDistance.y;
					ng.a -= fdm;
					#endif

					// #if CLOUD_MASK
					// ng.a -= SAMPLE_TEXTURE2D_LOD(_MaskTex, sampler_MaskTex, pos, 0).r;
					// #endif


					if (ng.a > 0)
					{
						cloudCol = half4(_CloudColor.rgb * (1.0 - ng.a), ng.a * 0.4);

						#if CLOUD_SUN_SHADOWS_ON
						float t = dir.w * shadowData.w;
						
						#if _MAIN_LIGHT_SHADOWS_CASCADE
						float3 wPos = lerp(endWPos, startWPos, t);
						float4 shadowCoords = TransformWorldToShadowCoord(wPos);
						#else
						float4 shadowCoords = lerp(shadowCoords1, shadowCoords0, t);
						#endif

						half shadowAtten = MainLightRealtimeShadow(shadowCoords);
						ng.rgb *= lerp(1.0, shadowAtten, shadowData.x * sum.a);
						cloudCol *= lerp(1, shadowAtten, shadowData.z);
						#endif

						cloudCol.rgb *= ng.rgb * cloudCol.aaa;
						sum += (lerp(1, 10, _CloudStepping.x) * (1.0 - sum.a)) * cloudCol;

						if (sum.a > 0.99)
						{
							break;
						}
					}
				}

				// adds fog fraction to prevent banding due stepping on low densities
				// sum += (cloudLength >= dist) * (sum.a<0.99) * cloudCol * (1.0-sum.a) * dir.w; // first operand not needed if dithering is enabled
				if (sum.a < 0.99)
				{
					sum += cloudCol * (1.0 - sum.a) * dir.w;
				}
				sum *= _CloudColor.a;
				return sum;
			}


			half4 FragCloud(v2f i):SV_TARGET
			{
				float2 uv = i.uv;

				//其实这里可以用小的RT 渲染到 uv*2+ 1 or 0 渲染到大的rt
				//但是 因为可能会做 raymarch mesh  而不是 屏幕
				//所以就先做屏幕切割法   不过如果存在极端情况 屏幕切割法应该没有用
				#if CLOUD_FRAME_ON

				// if (_Frame == 0 && (uv.x <=0.5 && uv.y <=0.5) == false)
				// {
				// 	discard;
				// }
				// else if (_Frame == 1 && (uv.x >=0.5 && uv.y <0.5) == false)
				// {
				// 	discard;
				// }
				// else if (_Frame == 2 && (uv.x <=0.5 && uv.y >=0.5) == false)
				// {
				// 	discard;
				// }
				// else if(_Frame == 3 &&(uv.x >=0.5 && uv.y >= 0.5) == false)
				// {
				// 	discard;
				// }

				if (_Frame == 0 && (uv.x <=0.5) == false)
				{
					discard;
				}
				else if (_Frame == 1 && (uv.x >=0.5) == false)
				{
					discard;
				}
				
				#endif

				float depth = SampleSceneDepth(uv);
				// 因为来源是一个大三角形  所以这样子还是不准确 所以换下面的方法
				//  VS :  o.camDir = GetWorldSpacePosition(uv) - _WorldSpaceCameraPos;
				// depth = Linear01Depth(depth,_ZBufferParams);
				// float3 wPos = _WorldSpaceCameraPos + i.camDir * depth;
				float3 wPos = GetWorldSpacePosition(uv, depth);

				float dither = GetDither(uv);

				half4 sum = GetSolidCloudColor(uv, wPos, depth, dither);
				sum *= 1.0 + dither * _CloudStepping.w;

				return half4(sum);
			}
			ENDHLSL
		}

		//1.GenerateNoise
		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragNoise

			TEXTURE2D(_NoiseTex);
			SAMPLER(sampler_linear_repeat_NoiseTex);

			float _NoiseStrength;
			float _NoiseDensity;
			int _NoiseSize;
			int _NoiseCount;
			int _NoiseSeed;
			float3 _LightColor;
			float4 _SpecularColor;


			inline float GetAlpha(float2 uv)
			{
				const float alpha = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_linear_repeat_NoiseTex, uv, 0).r;
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

		//2.Random Noise
		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragNoise

			#pragma multi_compile_local _ CLOUD_MASK

			TEXTURE2D(_NoiseTex);
			SAMPLER(sampler_linear_repeat_NoiseTex);
			float _Amount;

			#if CLOUD_MASK
			TEXTURE2D(_MaskTex);
			SAMPLER(sampler_MaskTex);
			#endif

			half4 FragNoise(v2f i):SV_TARGET
			{
				// _Amount = 0;
				float sint, cost;
				sincos(_Amount, sint, cost);
				half4 p0 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_linear_repeat_NoiseTex, i.uv, 0);
				half4 p1 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_linear_repeat_NoiseTex, i.uv + float2(0.25,0.25), 0);
				float t0 = (sint + 1.0) * 0.5;
				half4 r0 = lerp(p0, p1, t0);

				half4 p2 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_linear_repeat_NoiseTex, i.uv + float2(0.5,0.5), 0);
				half4 p3 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_linear_repeat_NoiseTex, i.uv + float2(0.75,0.75), 0);
				float t1 = (cost + 1.0) * 0.5;
				half4 r1 = lerp(p2, p3, t1);

				r0 = max(r0, r1);

				#if CLOUD_MASK
				r0.a = saturate(r0.a - SAMPLE_TEXTURE2D_LOD(_MaskTex, sampler_MaskTex, i.uv, 0).r);
				#endif

				return r0;
			}
			ENDHLSL
		}

		//3.Blend
		Pass
		{
			Blend One [_DstBlend]//OneMinusSrcAlpha


			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragOut

			#pragma multi_compile_local _ CLOUD_BLUR_ON


			TEXTURE2D(_BlendTex);
			SAMPLER(sampler_linear_repeat_BlendTex);

			float4 _NoiseTex_TexelSize;

			half4 FragOut(v2f i):SV_TARGET
			{
				#if !CLOUD_BLUR_ON
				return SAMPLE_TEXTURE2D_LOD(_BlendTex, sampler_linear_repeat_BlendTex, i.uv, 0);
				#else
				float2 step = 1 * _NoiseTex_TexelSize.xy;

				half4 col = 0;

				col += SAMPLE_TEXTURE2D_LOD(_BlendTex, sampler_linear_repeat_BlendTex
				                                 , i.uv + float2(step.x,0), 0);

				col += SAMPLE_TEXTURE2D_LOD(_BlendTex, sampler_linear_repeat_BlendTex
				                            , i.uv + float2(-step.x, 0), 0);

				col += SAMPLE_TEXTURE2D_LOD(_BlendTex, sampler_linear_repeat_BlendTex
				                            , i.uv + float2(0, step.y), 0);
				col += SAMPLE_TEXTURE2D_LOD(_BlendTex, sampler_linear_repeat_BlendTex
				                            , i.uv + float2(0, -step.y), 0);
				return col * 0.25;
				#endif
			}
			ENDHLSL
		}

		//4.Blend Mul RT
		Pass
		{
			Blend One [_DstBlend]

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragOut

			// TEXTURE2D(_TempBlendTex0);
			// SAMPLER(sampler_linear_repeat_TempBlendTex0);
			TEXTURE2D(_TempBlendTex1);
			SAMPLER(sampler_linear_repeat_TempBlendTex1);
			TEXTURE2D(_TempBlendTex2);
			SAMPLER(sampler_linear_repeat_TempBlendTex2);


			half4 FragOut(v2f i):SV_TARGET
			{
				half4 col = 0;
				// col += 0.333 * SAMPLE_TEXTURE2D_LOD(_TempBlendTex0, sampler_linear_repeat_TempBlendTex0, i.uv, 0);
				col += 0.8 * SAMPLE_TEXTURE2D_LOD(_TempBlendTex1, sampler_linear_repeat_TempBlendTex1, i.uv, 0);
				col += 0.2 * SAMPLE_TEXTURE2D_LOD(_TempBlendTex2, sampler_linear_repeat_TempBlendTex2, i.uv, 0);
				return col;
			}
			ENDHLSL
		}
	}
}