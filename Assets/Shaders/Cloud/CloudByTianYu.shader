Shader "MyRP/Cloud/CloudByTianYu"
{
	Properties
	{
	}
	SubShader
	{
		Pass
		{
			Cull Front
			
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


			struct a2v
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float3 normal: NORMAL0;
				float4 uv0: TEXCOORD0;
				float3 uv1: TEXCOORD1;
			};

			TEXTURE3D(_ShapeTex);
			SAMPLER(sampler_ShapeTex);
			TEXTURE3D(_DetailTex);
			SAMPLER(sampler_DetailTex);
			TEXTURE2D(_ConeTracingTex);
			SAMPLER(sampler_ConeTracingTex);
			TEXTURE2D(_TemporalCloudRenderTarget);
			SAMPLER(sampler_TemporalCloudRenderTarget);


			CBUFFER_START(UnityPerMaterial)
			float _Density;
			float _ViewDistance;
			float _BlurDistance;
			float _LightingDensity;
			float _Gamma;
			float _Symmetric;
			float _TransparentDistance;
			float _TemporalHistoryFrames;
			float3 _ShapeScale;
			float3 _ShapeTranslation;
			float3 _DetailScale;
			float3 _DetailTranslation;
			float3 _DetailSecondaryTranslation;
			float3 _DetailTertiaryTranslation;
			float _DetailDensityScale;

			float4x4 _TemporalCloudVPMatrix;
			float4x4 _TemporalViewDirParams;
			float4 _TemporalCloudRenderTarget_TexelSize;
			CBUFFER_END


			v2f Vert(a2v v)
			{
				v2f o = (v2f)0;
				float4 mvpPos = mul(UNITY_MATRIX_M, float4(v.vertex.xyz, 1.0));
				o.uv1 = mvpPos.xyz;
				mvpPos = mul(UNITY_MATRIX_VP, mvpPos);
				o.pos = mvpPos;
				float4 tempPos = float4(mul(v.normal.xyz, (float3x3)GetWorldToObjectMatrix()), 1.0);
				float len = rsqrt(dot(tempPos.xyz, tempPos.xyz));
				o.normal.xyz = len * tempPos.xyz;
				mvpPos.y = mvpPos.y * _ProjectionParams.x;
				tempPos.xyw = mvpPos.xyw * 0.5;
				o.uv0.zw = mvpPos.zw;
				o.uv0.xy = tempPos.ww + tempPos.xy;

				return o;
			}


			float4 tempValV4_1;
			float3 u_xlat4;
			float u_xlat5;
			float u_xlat6;
			float4 u_xlat7;
			float2 u_xlat8;
			float4 u_xlat16_8;
			float3 u_xlat9;
			float3 u_xlat10;
			float u_xlat11;
			float u_xlat12;
			float u_xlat13;
			float2 tempValV2_0;
			float u_xlat16;
			float3 u_xlat18;
			float u_xlat16_18;
			float3 u_xlat19;
			float3 u_xlat16_19;
			float3 u_xlat20;
			float u_xlat22;
			float2 taaPixel;
			float u_xlat24;
			float u_xlat26;
			float u_xlat28;
			float u_xlat29;
			float u_xlat30;
			float tempValV1_0;
			float u_xlat35;
			float u_xlat36;
			float u_xlat37;
			float u_xlat39;
			float u_xlat40;
			float u_xlat41;

			float Noise31(float3 inVal)
			{
				float val = dot(inVal, float3(12.9898005, 78.2330017, 173.889999));
				val = sin(val);
				val = val * 43758.5469;
				return frac(val);
			}

			float Noise42(float3 inVal)
			{
				return float2(Noise31(inVal.zyx), Noise31(inVal.xyz));
			}

			float2 GetMaxMinDepthPercentage(float2 uv, float3 viewDir)
			{
				const float depthStep = 0.5f;
				float c_p = SampleSceneDepth(uv.xy);
				float ru_p = SampleSceneDepth(uv
					+ float2(depthStep, depthStep) * _TemporalCloudRenderTarget_TexelSize.xy);
				float lu_p = SampleSceneDepth(uv
					+ float2(-depthStep, depthStep) * _TemporalCloudRenderTarget_TexelSize.xy);
				float rd_p = SampleSceneDepth(uv
					+ float2(depthStep, -depthStep) * _TemporalCloudRenderTarget_TexelSize.xy);
				float ld_p = SampleSceneDepth(uv
					+ float2(-depthStep, -depthStep) * _TemporalCloudRenderTarget_TexelSize.xy);
				float maxD = max(c_p, max(ld_p, max(rd_p, max(lu_p, ru_p))));
				maxD = LinearEyeDepth(maxD, _ZBufferParams);
				float minD = min(c_p, min(ld_p, min(rd_p, min(lu_p, ru_p))));
				minD = LinearEyeDepth(minD, _ZBufferParams);
				float zLength = dot(UNITY_MATRIX_I_V._31_32_33, viewDir);
				return float2(maxD / zLength, minD / zLength);
			}

			float CalcConeTracing(float2 uv, float3 viewDir, out float coneTracing)
			{
				coneTracing = max(8.0,SAMPLE_TEXTURE2D(_ConeTracingTex, sampler_ConeTracingTex, uv).r);
				float s2 = dot(_MainLightPosition.xyz, viewDir.xyz) * _Symmetric;
				float t = s2 + s2;
				s2 = _Symmetric * _Symmetric;

				// t = -t + (1.0 + s2);
				// t = log2(t);
				// t = t * 1.5;
				// t = exp2(t);
				// t = (1.0 - s2) / t;
				// t = t * 0.785374999;

				t = 0.785375 * ((1.0 - s2) / (exp2(log2(-t + (1.0 + s2)) * 1.5)));

				return t;
			}

			half4 frag(v2f i) : SV_Target
			{
				float3 viewDir = normalize(i.uv1.xyz - _WorldSpaceCameraPos.xyz);
				float4 uv0;
				uv0.xy = i.uv0.xy / i.uv0.ww;

				//0.03125 = 1 / 32
				taaPixel.xy = 0.03125 * (uv0.xy * _TemporalCloudRenderTarget_TexelSize.zw)
					+ Noise42(frac(_Time.zyx * 0.1));

				float2 maxMinDepthPercentage = GetMaxMinDepthPercentage(uv0.xy, viewDir);
				tempValV4_1.x = maxMinDepthPercentage.x;
				tempValV1_0 = maxMinDepthPercentage.y;

				float2 shapeScale = float2(0.984375, 0.015625) / _ShapeScale.yy - _ShapeTranslation.yy;
				shapeScale.xy = (shapeScale.xy - _WorldSpaceCameraPos.yy) / viewDir.yy;

				float tempDistance = (0.001 < viewDir.y)
					                     ? min(shapeScale.x, maxMinDepthPercentage.x)
					                     : maxMinDepthPercentage.x;
				tempDistance = (viewDir.y < -0.001)
					               ? shapeScale.x
					               : min(shapeScale.y, tempDistance);

				float4 t_i_shapeScale = float4(1.0, 1.0, 1.0, 1.0) / _ShapeScale.yxyz;
				t_i_shapeScale.x = min(max(tempDistance.x, 0.0), _ViewDistance);

				float coneTracing;
				u_xlat12 = CalcConeTracing(uv0, viewDir,/*out*/ coneTracing);

				tempValV4_1.xyz = _ShapeScale.xzy * _ShapeTranslation.xzy;
				u_xlat36 = _LightingDensity * 64.0;
				u_xlat4.xyz = _DetailTranslation.xyz;
				u_xlat37 = 1.0;
				u_xlat5 = float(1.0);
				u_xlat16 = float(0.0);
				float2 coneTracingV2 = coneTracing;
				u_xlat6 = coneTracing;
				float forLoopTimer = 0.0;

				//while (true)
				for (int i = 0; i < 512; i++)
				{
					if (!((forLoopTimer < 32.0)
						&& (coneTracingV2.x < t_i_shapeScale.x)
						&& (0.001 < u_xlat5)))
					{
						break;
					}

					u_xlat28 = min(t_i_shapeScale.x, coneTracingV2.y);
					u_xlat7.xy = float2(u_xlat28, u_xlat28) * viewDir.xy + _WorldSpaceCameraPos.xy;
					u_xlat39 = (-coneTracingV2.x) + u_xlat28;
					u_xlat39 = max(u_xlat39, 1.0);
					u_xlat39 = min(u_xlat37, u_xlat39);
					u_xlat7.xy = taaPixel.xy * u_xlat7.xy;
					u_xlat7.x = dot(u_xlat7.xy, float2(12.9898005, 78.2330017));
					u_xlat7.x = sin(u_xlat7.x);
					u_xlat7.x = u_xlat7.x * 43758.5469;
					u_xlat7.x = frac(u_xlat7.x);
					u_xlat18.x = u_xlat7.x * (-u_xlat39) + u_xlat28;
					u_xlat18.xyz = u_xlat18.xxx * viewDir.xzy + _WorldSpaceCameraPos.xzy;
					u_xlat18.xyz = u_xlat18.xyz * _ShapeScale.xzy + tempValV4_1.xyz;
					u_xlat16_8 = SAMPLE_TEXTURE3D_LOD(_ShapeTex, sampler_ShapeTex, u_xlat18.xyz, 0.0);
					if (0.00001 < u_xlat16_8.x)
					{
						u_xlat18.xyz = u_xlat18.xyz * t_i_shapeScale.ywz + (-_ShapeTranslation.xzy);
						u_xlat41 = u_xlat16_8.w * 2.0 + -1.0;
						u_xlat20.xyz = (u_xlat41 < -0.05) ? _DetailTertiaryTranslation.xyz : u_xlat4.xyz;
						u_xlat9.xyz = (0.05 < u_xlat41) ? _DetailSecondaryTranslation.xyz : u_xlat20.xyz;
						u_xlat10.xyz = u_xlat9.xyz * _DetailScale.xyz;
						u_xlat18.xyz = u_xlat18.xyz * _DetailScale.xyz + u_xlat10.xyz;
						u_xlat16_18 = SAMPLE_TEXTURE3D_LOD(_DetailTex, sampler_DetailTex, u_xlat18.xyz, 0.0).r;
						u_xlat18.x = u_xlat16_18 * _DetailDensityScale;
					}
					else
					{
						u_xlat9.xyz = u_xlat4.xyz;
						u_xlat18.x = 0.0;
						//ENDIF
					}
					u_xlat18.x = (-u_xlat18.x) + u_xlat16_8.x;
					u_xlat18.x = clamp(u_xlat18.x, 0.0, 1.0);

					u_xlat18.x = (0.01 < u_xlat18.x) ? u_xlat18.x : float(0.0);
					u_xlat18.x = u_xlat18.x * _Density;
					u_xlat29 = u_xlat16_8.y * 64.0 + -32.0;
					u_xlat18.x = (8.0 < u_xlat29) ? 0.0 : u_xlat18.x;
					u_xlat40 = (_TransparentDistance < u_xlat5) ? u_xlat28 : u_xlat6;
					if (0.001 < u_xlat18.x)
					{
						if (8.0 < u_xlat37)
						{
							u_xlat8.x = forLoopTimer + 1.0;
							u_xlat4.xyz = u_xlat9.xyz;
							u_xlat37 = 2.0;
							coneTracingV2.y = coneTracingV2.x;
							u_xlat6 = u_xlat40;
							forLoopTimer = u_xlat8.x;
							continue;
							//ENDIF
						}
						u_xlat39 = u_xlat7.x * (-u_xlat39) + u_xlat39;
						u_xlat39 = max(u_xlat39, 1.0);
						u_xlat7.x = u_xlat36 * u_xlat16_8.z;
						u_xlat8.x = u_xlat18.x * -2.88539004;
						u_xlat8.x = exp2(u_xlat8.x);
						u_xlat8.x = (-u_xlat8.x) + 1.0;
						u_xlat8.x = u_xlat12 * u_xlat8.x;
						u_xlat7.x = u_xlat7.x * -1.44269502;
						u_xlat7.x = exp2(u_xlat7.x);
						u_xlat7.x = u_xlat7.x * u_xlat8.x;
						u_xlat39 = u_xlat39 * (-u_xlat18.x);
						u_xlat39 = u_xlat39 * 1.44269502;
						u_xlat39 = exp2(u_xlat39);
						u_xlat7.x = (-u_xlat7.x) * u_xlat39 + u_xlat7.x;
						u_xlat7.x = u_xlat7.x / u_xlat18.x;
						u_xlat16 = u_xlat5 * u_xlat7.x + u_xlat16;
						u_xlat5 = u_xlat5 * u_xlat39;
						//ENDIF
					}
					u_xlat39 = u_xlat37 * 1.10000002;
					u_xlat37 = min(u_xlat39, 16.0);
					u_xlat39 = max(u_xlat37, abs(u_xlat29));
					coneTracingV2.y = u_xlat39 + u_xlat28;
					coneTracingV2.x = u_xlat28;
					forLoopTimer = forLoopTimer + 1.0;
					u_xlat4.xyz = u_xlat9.xyz;
					u_xlat6 = u_xlat40;
				}
				u_xlat13 = _DetailDensityScale * 0.300000012;
				u_xlat24 = u_xlat37;
				u_xlat7.w = u_xlat5;
				u_xlat35 = u_xlat16;
				u_xlat4.xy = coneTracingV2.xy;
				u_xlat7.y = u_xlat6;
				u_xlat26 = forLoopTimer;
				while (true)
				{
					if (!((u_xlat26 < 96.0) && (u_xlat4.x < t_i_shapeScale.x) && (0.001 < u_xlat7.w)))
					{
						break;
					}
					u_xlat28 = (t_i_shapeScale.x < u_xlat4.y) ? t_i_shapeScale.x : u_xlat4.y;
					u_xlat8.xy = float2(u_xlat28, u_xlat28) * viewDir.xy + _WorldSpaceCameraPos.xy;
					u_xlat39 = (-u_xlat4.x) + u_xlat28;
					u_xlat39 = max(u_xlat39, 1.0);
					u_xlat39 = min(u_xlat24, u_xlat39);
					u_xlat8.xy = taaPixel.xy * u_xlat8.xy;
					u_xlat8.x = dot(u_xlat8.xy, float2(12.9898005, 78.2330017));
					u_xlat8.x = sin(u_xlat8.x);
					u_xlat8.x = u_xlat8.x * 43758.5469;
					u_xlat8.x = frac(u_xlat8.x);
					u_xlat19.x = u_xlat8.x * (-u_xlat39) + u_xlat28;
					u_xlat19.xyz = u_xlat19.xxx * viewDir.xzy + _WorldSpaceCameraPos.xzy;
					u_xlat19.xyz = u_xlat19.xyz * _ShapeScale.xzy + tempValV4_1.xyz;
					u_xlat16_19.xyz = SAMPLE_TEXTURE2D_LOD(_ShapeTex, sampler_ShapeTex, u_xlat19, 0.0).rgb;
					u_xlat9.x = (0.00001 < u_xlat16_19.x) ? u_xlat13 : float(0.0);
					u_xlat19.x = u_xlat16_19.x + (-u_xlat9.x);
					u_xlat19.x = clamp(u_xlat19.x, 0.0, 1.0);
					u_xlat19.x = (0.01 < u_xlat19.x) ? u_xlat19.x : float(0.0);
					u_xlat19.x = u_xlat19.x * _Density;
					u_xlat30 = u_xlat16_19.y * 64.0 + -32.0;
					u_xlat19.x = (8.0 < u_xlat30) ? 0.0 : u_xlat19.x;
					u_xlat9.x = (_TransparentDistance < u_xlat7.w) ? u_xlat28 : u_xlat7.y;
					if (0.001 < u_xlat19.x)
					{
						if (8.0 < u_xlat24)
						{
							u_xlat20.x = u_xlat26 + 1.0;
							u_xlat24 = 2.0;
							u_xlat4.y = u_xlat4.x;
							u_xlat7.y = u_xlat9.x;
							u_xlat26 = u_xlat20.x;
							continue;
							//ENDIF
						}
						u_xlat39 = u_xlat8.x * (-u_xlat39) + u_xlat39;
						u_xlat39 = max(u_xlat39, 1.0);
						u_xlat8.x = u_xlat36 * u_xlat16_19.z;
						u_xlat41 = u_xlat19.x * -2.88539004;
						u_xlat41 = exp2(u_xlat41);
						u_xlat41 = (-u_xlat41) + 1.0;
						u_xlat41 = u_xlat12 * u_xlat41;
						u_xlat8.x = u_xlat8.x * -1.44269502;
						u_xlat8.x = exp2(u_xlat8.x);
						u_xlat8.x = u_xlat8.x * u_xlat41;
						u_xlat39 = u_xlat39 * (-u_xlat19.x);
						u_xlat39 = u_xlat39 * 1.44269502;
						u_xlat39 = exp2(u_xlat39);
						u_xlat8.x = (-u_xlat8.x) * u_xlat39 + u_xlat8.x;
						u_xlat8.x = u_xlat8.x / u_xlat19.x;
						u_xlat35 = u_xlat7.w * u_xlat8.x + u_xlat35;
						u_xlat7.w = u_xlat39 * u_xlat7.w;
						//ENDIF
					}
					u_xlat39 = u_xlat24 * 1.10000002;
					u_xlat24 = min(u_xlat39, 64.0);
					u_xlat39 = max(u_xlat24, abs(u_xlat30));
					u_xlat4.y = u_xlat39 + u_xlat28;
					u_xlat4.x = u_xlat28;
					u_xlat26 = u_xlat26 + 1.0;
					u_xlat7.y = u_xlat9.x;
				}
				u_xlat12 = log2(u_xlat35);
				u_xlat12 = u_xlat12 * _Gamma;
				u_xlat12 = exp2(u_xlat12);
				u_xlat7.x = min(u_xlat12, 4.0);
				u_xlat12 = (_BlurDistance < t_i_shapeScale.x) ? 1023.0 : t_i_shapeScale.x;
				tempValV1_0 = (128.0 < tempValV1_0) ? 1023.0 : tempValV1_0;
				u_xlat7.z = (u_xlat7.y < tempValV1_0) ? tempValV1_0 : u_xlat12;
				tempValV1_0 = (-coneTracing) + u_xlat4.y;
				if (0.1 < tempValV1_0)
				{
					uv0.xyz = u_xlat4.yyy * viewDir.xyz + _WorldSpaceCameraPos.xyz;
					uv0.xyz = uv0.xyz + (-_ShapeTranslation.xyz);
					t_i_shapeScale = uv0.yyyy * _TemporalCloudVPMatrix._21_22_23_24;
					t_i_shapeScale = _TemporalCloudVPMatrix._11_12_13_14 * uv0.xxxx + t_i_shapeScale;
					uv0 = _TemporalCloudVPMatrix._31_32_33_34 * uv0.zzzz + t_i_shapeScale;
					uv0 = uv0 + _TemporalCloudVPMatrix._41_42_43_44;
					bool3 u_xlatb2 = uv0.xyz< uv0.www;
					bool3 u_xlatb3 = -uv0.www< uv0.xyz;
					u_xlatb2.x = u_xlatb2.x && u_xlatb3.x;
					u_xlatb2.y = u_xlatb2.y && u_xlatb3.y;
					u_xlatb2.z = u_xlatb2.z && u_xlatb3.z;
					if (u_xlatb2.x && u_xlatb2.y && u_xlatb2.z)
					{
						t_i_shapeScale.xz = uv0.xw * float2(0.5, 0.5);
						tempValV1_0 = uv0.y * _ProjectionParams.x;
						t_i_shapeScale.w = tempValV1_0 * 0.5;
						uv0.xy = t_i_shapeScale.zz + t_i_shapeScale.xw;
						t_i_shapeScale.xyz = _TemporalViewDirParams._41_42_43 + (-_WorldSpaceCameraPos.xyz);
						uv0.xy = uv0.xy / uv0.ww;
						uv0 = SAMPLE_TEXTURE2D(_TemporalCloudRenderTarget, sampler_TemporalCloudRenderTarget, uv0.xy);
						tempValV1_0 = uv0.x + uv0.x;
						tempValV1_0 = exp2(tempValV1_0);
						uv0.x = tempValV1_0 + -1.0;
						tempValV4_1.xy = uv0.zw * float2(10.0, 10.0);
						tempValV4_1.xy = exp2(tempValV4_1.xy);
						tempValV4_1.xy = tempValV4_1.xy + float2(-1.0, -1.0);
						viewDir.x = dot(t_i_shapeScale.xyz, viewDir.xyz);
						viewDir.xy = viewDir.xx + tempValV4_1.xy;
						uv0.zw = max(viewDir.xy, float2(0.0, 0.0));
						viewDir.x = dot(uv0, float4(1.0, 1.0, 1.0, 1.0));
						u_xlat11 = uv0.w + u_xlat7.z;
						u_xlat22 = (-u_xlat7.z) + uv0.w;
						u_xlat11 = ((8.0 < abs(u_xlat22)) && u_xlat11 < 2000.0) ? 1.0 : float(0.0);
						u_xlat22 = float(1.0) / _TemporalHistoryFrames;
						u_xlat11 = u_xlat11 + u_xlat22;
						u_xlat11 = clamp(u_xlat11, 0.0, 1.0);
						t_i_shapeScale = (-uv0) + u_xlat7.xwyz;
						uv0 = u_xlat11.xxxx * t_i_shapeScale + uv0;
						u_xlat7 = (true) ? uv0.xzwy : u_xlat7;
					}
				}
				viewDir.xyz = u_xlat7.xyz + float3(1.0, 1.0, 1.0);
				viewDir.xyz = log2(viewDir.xyz);
				u_xlat7.xyz = viewDir.xyz * float3(0.5, 0.1, 0.1);
				return u_xlat7.xwyz;
			}
			ENDHLSL
		}
	}
}