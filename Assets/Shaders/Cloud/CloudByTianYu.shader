Shader "MyRP/Cloud/CloudByTianYu"
{
	Properties
	{
	}
	SubShader
	{


		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

			CBUFFER_START(UnityPerMaterial)

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
				tempPos.xzw = mvpPos.xwy * 0.5;
				o.uv0.zw = mvpPos.zw;
				o.uv0.xy = tempPos.zz + tempPos.xw;

				return o;
			}


			float3 u_xlat0;
			bool u_xlatb0;
			float4 u_xlat1;
			float u_xlat16_1;
			float4 u_xlat2;
			bool3 u_xlatb2;
			float4 u_xlat3;
			bool3 u_xlatb3;
			float3 u_xlat4;
			float u_xlat5;
			float u_xlat6;
			float4 u_xlat7;
			float2 u_xlat8;
			float4 u_xlat16_8;
			bool u_xlatb8;
			float3 u_xlat9;
			bool u_xlatb9;
			float3 u_xlat10;
			float u_xlat11;
			bool u_xlatb11;
			float u_xlat12;
			bool u_xlatb12;
			float u_xlat13;
			float2 u_xlat14;
			float u_xlat16;
			float u_xlat17;
			float3 u_xlat18;
			float u_xlat16_18;
			float3 u_xlat19;
			float3 u_xlat16_19;
			float3 u_xlat20;
			bool u_xlatb20;
			float u_xlat22;
			bool u_xlatb22;
			float2 u_xlat23;
			bool u_xlatb23;
			float u_xlat24;
			float u_xlat26;
			float2 u_xlat27;
			float u_xlat28;
			bool u_xlatb28;
			float u_xlat29;
			bool u_xlatb29;
			float u_xlat30;
			float u_xlat33;
			bool u_xlatb33;
			bool u_xlatb34;
			float u_xlat35;
			float u_xlat36;
			float u_xlat37;
			float u_xlat39;
			bool u_xlatb39;
			float u_xlat40;
			bool u_xlatb40;
			float u_xlat41;
			bool u_xlatb41;

			half4 frag(v2f i) : SV_Target
			{
				u_xlat0.xyz = (-i.uv1.xyz) + _WorldSpaceCameraPos.xyz;
				u_xlat33 = dot(u_xlat0.xyz, u_xlat0.xyz);
				u_xlat33 = sqrt(u_xlat33);
				u_xlat0.xyz = (-u_xlat0.xyz) / u_xlat33;
				u_xlat1.xy = i.uv0.xy / i.uv0.ww;
				u_xlat2.xyz = _Time.zyx * float3(0.100000001, 0.100000001, 0.100000001);
				u_xlat2.xyz = frac(u_xlat2.xyz);
				u_xlat23.xy = u_xlat1.xy * _TemporalCloudRenderTarget_TexelSize.zw;
				u_xlat33 = dot(u_xlat2.zyx, float3(12.9898005, 78.2330017, 173.889999));
				u_xlat33 = sin(u_xlat33);
				u_xlat33 = u_xlat33 * 43758.5469;
				u_xlat3.x = frac(u_xlat33);
				u_xlat33 = dot(u_xlat2.xyz, float3(12.9898005, 78.2330017, 173.889999));
				u_xlat33 = sin(u_xlat33);
				u_xlat33 = u_xlat33 * 43758.5469;
				u_xlat3.y = frac(u_xlat33);
				u_xlat23.xy = u_xlat23.xy * float2(0.03125, 0.03125) + u_xlat3.xy;
				u_xlat2 = (-_TemporalCloudRenderTarget_TexelSize.xyxy) * float4(0.5, 0.5, -0.5, 0.5) + u_xlat1.xyxy;
				u_xlat2 = u_xlat2 * unity_DynamicResolutionParams.xyxy + unity_DynamicResolutionParams.zwzw;
				u_xlat33 = textureLod(_CameraDepthTexture, u_xlat2.xy, 0.0).x;
				u_xlat2.x = textureLod(_CameraDepthTexture, u_xlat2.zw, 0.0).x;
				u_xlat3 = (-_TemporalCloudRenderTarget_TexelSize.xyxy) * float4(0.5, -0.5, -0.5, -0.5) + u_xlat1.xyxy;
				u_xlat3 = u_xlat3 * unity_DynamicResolutionParams.xyxy + unity_DynamicResolutionParams.zwzw;
				u_xlat13 = textureLod(_CameraDepthTexture, u_xlat3.xy, 0.0).x;
				u_xlat24 = textureLod(_CameraDepthTexture, u_xlat3.zw, 0.0).x;
				u_xlat3.xy = u_xlat1.xy * unity_DynamicResolutionParams.xy + unity_DynamicResolutionParams.zw;
				u_xlat35 = textureLod(_CameraDepthTexture, u_xlat3.xy, 0.0).x;
				u_xlat3.x = max(u_xlat35, u_xlat24);
				u_xlat3.x = max(u_xlat13, u_xlat3.x);
				u_xlat3.x = max(u_xlat2.x, u_xlat3.x);
				u_xlat3.x = max(u_xlat33, u_xlat3.x);
				u_xlat3.x = _ZBufferParams.z * u_xlat3.x + _ZBufferParams.w;
				u_xlat3.x = float(1.0) / u_xlat3.x;
				u_xlat14.x = dot(hlslcc_mtx4x4unity_CameraToWorld[2].xyz, u_xlat0.xyz);
				u_xlat3.x = u_xlat3.x / u_xlat14.x;
				u_xlat24 = min(u_xlat35, u_xlat24);
				u_xlat13 = min(u_xlat24, u_xlat13);
				u_xlat2.x = min(u_xlat13, u_xlat2.x);
				u_xlat33 = min(u_xlat33, u_xlat2.x);
				u_xlat33 = _ZBufferParams.z * u_xlat33 + _ZBufferParams.w;
				u_xlat33 = float(1.0) / u_xlat33;
				u_xlat33 = u_xlat33 / u_xlat14.x;
				u_xlat2 = float4(1.0, 1.0, 1.0, 1.0) / _ShapeScale.yxyz;
				u_xlat14.xy = u_xlat2.xx * float2(0.984375, 0.015625) + (-_ShapeTranslation.yy);
				#ifdef UNITY_ADRENO_ES3
    u_xlatb2.x = !!(0.00999999978<u_xlat0.y);
				#else
				u_xlatb2.x = 0.00999999978 < u_xlat0.y;
				#endif
				u_xlat14.xy = u_xlat14.xy + (-_WorldSpaceCameraPos.yy);
				u_xlat14.xy = u_xlat14.xy / u_xlat0.yy;
				u_xlat14.x = min(u_xlat14.x, u_xlat3.x);
				u_xlat2.x = (u_xlatb2.x) ? u_xlat14.x : u_xlat3.x;
				#ifdef UNITY_ADRENO_ES3
    u_xlatb3.x = !!(u_xlat0.y<-0.00999999978);
				#else
				u_xlatb3.x = u_xlat0.y < -0.00999999978;
				#endif
				u_xlat14.x = min(u_xlat14.y, u_xlat2.x);
				u_xlat2.x = (u_xlatb3.x) ? u_xlat14.x : u_xlat2.x;
				u_xlat2.x = max(u_xlat2.x, 0.0);
				u_xlat2.x = min(u_xlat2.x, _ViewDistance);
				u_xlat16_1 = texture(_ConeTracingTex, u_xlat1.xy).x;
				u_xlat16_1 = max(u_xlat16_1, 8.0);
				u_xlat12 = dot(_WorldSpaceLightPos0.xyz, u_xlat0.xyz);
				u_xlat3.x = (-_Symmetric) * _Symmetric + 1.0;
				u_xlat14.x = _Symmetric * _Symmetric + 1.0;
				u_xlat12 = dot(float2(u_xlat12), float2(_Symmetric));
				u_xlat12 = (-u_xlat12) + u_xlat14.x;
				u_xlat12 = log2(u_xlat12);
				u_xlat12 = u_xlat12 * 1.5;
				u_xlat12 = exp2(u_xlat12);
				u_xlat12 = u_xlat3.x / u_xlat12;
				u_xlat12 = u_xlat12 * 0.785374999;
				u_xlat3.xyz = _ShapeScale.xzy * _ShapeTranslation.xzy;
				u_xlat36 = _LightingDensity * 64.0;
				u_xlat4.xyz = _DetailTranslation.xyz;
				u_xlat37 = 1.0;
				u_xlat5 = float(1.0);
				u_xlat16 = float(0.0);
				u_xlat27.xy = float2(u_xlat16_1);
				u_xlat6 = u_xlat16_1;
				u_xlat17 = 0.0;
				while (true)
				{
					#ifdef UNITY_ADRENO_ES3
        u_xlatb28 = !!(u_xlat17<32.0);
					#else
					u_xlatb28 = u_xlat17 < 32.0;
					#endif
					#ifdef UNITY_ADRENO_ES3
        u_xlatb39 = !!(u_xlat27.x<u_xlat2.x);
					#else
					u_xlatb39 = u_xlat27.x < u_xlat2.x;
					#endif
					u_xlatb28 = u_xlatb39 && u_xlatb28;
					#ifdef UNITY_ADRENO_ES3
        u_xlatb39 = !!(0.00100000005<u_xlat5);
					#else
					u_xlatb39 = 0.00100000005 < u_xlat5;
					#endif
					u_xlatb28 = u_xlatb39 && u_xlatb28;
					if (!u_xlatb28) { break; }
					#ifdef UNITY_ADRENO_ES3
        u_xlatb28 = !!(u_xlat2.x<u_xlat27.y);
					#else
					u_xlatb28 = u_xlat2.x < u_xlat27.y;
					#endif
					u_xlat28 = (u_xlatb28) ? u_xlat2.x : u_xlat27.y;
					u_xlat7.xy = float2(u_xlat28) * u_xlat0.xy + _WorldSpaceCameraPos.xy;
					u_xlat39 = (-u_xlat27.x) + u_xlat28;
					u_xlat39 = max(u_xlat39, 1.0);
					u_xlat39 = min(u_xlat37, u_xlat39);
					u_xlat7.xy = u_xlat23.xy * u_xlat7.xy;
					u_xlat7.x = dot(u_xlat7.xy, float2(12.9898005, 78.2330017));
					u_xlat7.x = sin(u_xlat7.x);
					u_xlat7.x = u_xlat7.x * 43758.5469;
					u_xlat7.x = frac(u_xlat7.x);
					u_xlat18.x = u_xlat7.x * (-u_xlat39) + u_xlat28;
					u_xlat18.xyz = u_xlat18.xxx * u_xlat0.xzy + _WorldSpaceCameraPos.xzy;
					u_xlat18.xyz = u_xlat18.xyz * _ShapeScale.xzy + u_xlat3.xyz;
					u_xlat16_8 = textureLod(_ShapeTex, u_xlat18.xyz, 0.0);
					#ifdef UNITY_ADRENO_ES3
        u_xlatb9 = !!(9.99999975e-05<u_xlat16_8.x);
					#else
					u_xlatb9 = 9.99999975e-05 < u_xlat16_8.x;
					#endif
					if (u_xlatb9)
					{
						u_xlat18.xyz = u_xlat18.xyz * u_xlat2.ywz + (-_ShapeTranslation.xzy);
						u_xlat41 = u_xlat16_8.w * 2.0 + -1.0;
						#ifdef UNITY_ADRENO_ES3
            u_xlatb9 = !!(0.0500000007<u_xlat41);
						#else
						u_xlatb9 = 0.0500000007 < u_xlat41;
						#endif
						#ifdef UNITY_ADRENO_ES3
            u_xlatb41 = !!(u_xlat41<-0.0500000007);
						#else
						u_xlatb41 = u_xlat41 < -0.0500000007;
						#endif
						u_xlat20.xyz = (bool(u_xlatb41)) ? _DetailTertiaryTranslation.xyz : u_xlat4.xyz;
						u_xlat9.xyz = (bool(u_xlatb9)) ? _DetailSecondaryTranslation.xyz : u_xlat20.xyz;
						u_xlat10.xyz = u_xlat9.xyz * _DetailScale.xyz;
						u_xlat18.xyz = u_xlat18.xyz * _DetailScale.xyz + u_xlat10.xyz;
						u_xlat16_18 = textureLod(_DetailTex, u_xlat18.xyz, 0.0).x;
						u_xlat18.x = u_xlat16_18 * _DetailDensityScale;
					}
					else
					{
						u_xlat9.xyz = u_xlat4.xyz;
						u_xlat18.x = 0.0;
						//ENDIF
					}
					u_xlat18.x = (-u_xlat18.x) + u_xlat16_8.x;
					#ifdef UNITY_ADRENO_ES3
        u_xlat18.x = min(max(u_xlat18.x, 0.0), 1.0);
					#else
					u_xlat18.x = clamp(u_xlat18.x, 0.0, 1.0);
					#endif
					#ifdef UNITY_ADRENO_ES3
        u_xlatb29 = !!(0.00999999978<u_xlat18.x);
					#else
					u_xlatb29 = 0.00999999978 < u_xlat18.x;
					#endif
					u_xlat18.x = u_xlatb29 ? u_xlat18.x : float(0.0);
					u_xlat18.x = u_xlat18.x * _Density;
					u_xlat29 = u_xlat16_8.y * 64.0 + -32.0;
					#ifdef UNITY_ADRENO_ES3
        u_xlatb40 = !!(8.0<u_xlat29);
					#else
					u_xlatb40 = 8.0 < u_xlat29;
					#endif
					u_xlat18.x = (u_xlatb40) ? 0.0 : u_xlat18.x;
					#ifdef UNITY_ADRENO_ES3
        u_xlatb40 = !!(_TransparentDistance<u_xlat5);
					#else
					u_xlatb40 = _TransparentDistance < u_xlat5;
					#endif
					u_xlat40 = (u_xlatb40) ? u_xlat28 : u_xlat6;
					#ifdef UNITY_ADRENO_ES3
        u_xlatb8 = !!(0.00100000005<u_xlat18.x);
					#else
					u_xlatb8 = 0.00100000005 < u_xlat18.x;
					#endif
					if (u_xlatb8)
					{
						#ifdef UNITY_ADRENO_ES3
            u_xlatb8 = !!(8.0<u_xlat37);
						#else
						u_xlatb8 = 8.0 < u_xlat37;
						#endif
						if (u_xlatb8)
						{
							u_xlat8.x = u_xlat17 + 1.0;
							u_xlat4.xyz = u_xlat9.xyz;
							u_xlat37 = 2.0;
							u_xlat27.y = u_xlat27.x;
							u_xlat6 = u_xlat40;
							u_xlat17 = u_xlat8.x;
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
					u_xlat27.y = u_xlat39 + u_xlat28;
					u_xlat27.x = u_xlat28;
					u_xlat17 = u_xlat17 + 1.0;
					u_xlat4.xyz = u_xlat9.xyz;
					u_xlat6 = u_xlat40;
				}
				u_xlat13 = _DetailDensityScale * 0.300000012;
				u_xlat24 = u_xlat37;
				u_xlat7.w = u_xlat5;
				u_xlat35 = u_xlat16;
				u_xlat4.xy = u_xlat27.xy;
				u_xlat7.y = u_xlat6;
				u_xlat26 = u_xlat17;
				while (true)
				{
					#ifdef UNITY_ADRENO_ES3
        u_xlatb28 = !!(u_xlat26<96.0);
					#else
					u_xlatb28 = u_xlat26 < 96.0;
					#endif
					#ifdef UNITY_ADRENO_ES3
        u_xlatb39 = !!(u_xlat4.x<u_xlat2.x);
					#else
					u_xlatb39 = u_xlat4.x < u_xlat2.x;
					#endif
					u_xlatb28 = u_xlatb39 && u_xlatb28;
					#ifdef UNITY_ADRENO_ES3
        u_xlatb39 = !!(0.00100000005<u_xlat7.w);
					#else
					u_xlatb39 = 0.00100000005 < u_xlat7.w;
					#endif
					u_xlatb28 = u_xlatb39 && u_xlatb28;
					if (!u_xlatb28) { break; }
					#ifdef UNITY_ADRENO_ES3
        u_xlatb28 = !!(u_xlat2.x<u_xlat4.y);
					#else
					u_xlatb28 = u_xlat2.x < u_xlat4.y;
					#endif
					u_xlat28 = (u_xlatb28) ? u_xlat2.x : u_xlat4.y;
					u_xlat8.xy = float2(u_xlat28) * u_xlat0.xy + _WorldSpaceCameraPos.xy;
					u_xlat39 = (-u_xlat4.x) + u_xlat28;
					u_xlat39 = max(u_xlat39, 1.0);
					u_xlat39 = min(u_xlat24, u_xlat39);
					u_xlat8.xy = u_xlat23.xy * u_xlat8.xy;
					u_xlat8.x = dot(u_xlat8.xy, float2(12.9898005, 78.2330017));
					u_xlat8.x = sin(u_xlat8.x);
					u_xlat8.x = u_xlat8.x * 43758.5469;
					u_xlat8.x = frac(u_xlat8.x);
					u_xlat19.x = u_xlat8.x * (-u_xlat39) + u_xlat28;
					u_xlat19.xyz = u_xlat19.xxx * u_xlat0.xzy + _WorldSpaceCameraPos.xzy;
					u_xlat19.xyz = u_xlat19.xyz * _ShapeScale.xzy + u_xlat3.xyz;
					u_xlat16_19.xyz = textureLod(_ShapeTex, u_xlat19.xyz, 0.0).xyz;
					#ifdef UNITY_ADRENO_ES3
        u_xlatb9 = !!(9.99999975e-05<u_xlat16_19.x);
					#else
					u_xlatb9 = 9.99999975e-05 < u_xlat16_19.x;
					#endif
					u_xlat9.x = u_xlatb9 ? u_xlat13 : float(0.0);
					u_xlat19.x = u_xlat16_19.x + (-u_xlat9.x);
					#ifdef UNITY_ADRENO_ES3
        u_xlat19.x = min(max(u_xlat19.x, 0.0), 1.0);
					#else
					u_xlat19.x = clamp(u_xlat19.x, 0.0, 1.0);
					#endif
					#ifdef UNITY_ADRENO_ES3
        u_xlatb9 = !!(0.00999999978<u_xlat19.x);
					#else
					u_xlatb9 = 0.00999999978 < u_xlat19.x;
					#endif
					u_xlat19.x = u_xlatb9 ? u_xlat19.x : float(0.0);
					u_xlat19.x = u_xlat19.x * _Density;
					u_xlat30 = u_xlat16_19.y * 64.0 + -32.0;
					#ifdef UNITY_ADRENO_ES3
        u_xlatb9 = !!(8.0<u_xlat30);
					#else
					u_xlatb9 = 8.0 < u_xlat30;
					#endif
					u_xlat19.x = (u_xlatb9) ? 0.0 : u_xlat19.x;
					#ifdef UNITY_ADRENO_ES3
        u_xlatb9 = !!(_TransparentDistance<u_xlat7.w);
					#else
					u_xlatb9 = _TransparentDistance < u_xlat7.w;
					#endif
					u_xlat9.x = (u_xlatb9) ? u_xlat28 : u_xlat7.y;
					#ifdef UNITY_ADRENO_ES3
        u_xlatb20 = !!(0.00100000005<u_xlat19.x);
					#else
					u_xlatb20 = 0.00100000005 < u_xlat19.x;
					#endif
					if (u_xlatb20)
					{
						#ifdef UNITY_ADRENO_ES3
            u_xlatb20 = !!(8.0<u_xlat24);
						#else
						u_xlatb20 = 8.0 < u_xlat24;
						#endif
						if (u_xlatb20)
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
				#ifdef UNITY_ADRENO_ES3
    u_xlatb12 = !!(_BlurDistance<u_xlat2.x);
				#else
				u_xlatb12 = _BlurDistance < u_xlat2.x;
				#endif
				u_xlat12 = (u_xlatb12) ? 1023.0 : u_xlat2.x;
				#ifdef UNITY_ADRENO_ES3
    u_xlatb23 = !!(u_xlat7.y<u_xlat33);
				#else
				u_xlatb23 = u_xlat7.y < u_xlat33;
				#endif
				#ifdef UNITY_ADRENO_ES3
    u_xlatb34 = !!(128.0<u_xlat33);
				#else
				u_xlatb34 = 128.0 < u_xlat33;
				#endif
				u_xlat33 = (u_xlatb34) ? 1023.0 : u_xlat33;
				u_xlat7.z = (u_xlatb23) ? u_xlat33 : u_xlat12;
				u_xlat33 = (-u_xlat16_1) + u_xlat4.y;
				#ifdef UNITY_ADRENO_ES3
    u_xlatb33 = !!(0.100000001<u_xlat33);
				#else
				u_xlatb33 = 0.100000001 < u_xlat33;
				#endif
				if (u_xlatb33)
				{
					u_xlat1.xyz = u_xlat4.yyy * u_xlat0.xyz + _WorldSpaceCameraPos.xyz;
					u_xlat1.xyz = u_xlat1.xyz + (-_ShapeTranslation.xyz);
					u_xlat2 = u_xlat1.yyyy * hlslcc_mtx4x4_TemporalCloudVPMatrix[1];
					u_xlat2 = hlslcc_mtx4x4_TemporalCloudVPMatrix[0] * u_xlat1.xxxx + u_xlat2;
					u_xlat1 = hlslcc_mtx4x4_TemporalCloudVPMatrix[2] * u_xlat1.zzzz + u_xlat2;
					u_xlat1 = u_xlat1 + hlslcc_mtx4x4_TemporalCloudVPMatrix[3];
					u_xlatb2.xyz = lessThan(u_xlat1.xyzx, u_xlat1.wwww).xyz;
					u_xlatb3.xyz = lessThan((-u_xlat1.wwww), u_xlat1.xyzx).xyz;
					u_xlatb2.x = u_xlatb2.x && u_xlatb3.x;
					u_xlatb2.y = u_xlatb2.y && u_xlatb3.y;
					u_xlatb2.z = u_xlatb2.z && u_xlatb3.z;
					u_xlatb33 = u_xlatb2.y && u_xlatb2.x;
					u_xlatb33 = u_xlatb2.z && u_xlatb33;
					if (u_xlatb33)
					{
						u_xlat2.xz = u_xlat1.xw * float2(0.5, 0.5);
						u_xlat33 = u_xlat1.y * _ProjectionParams.x;
						u_xlat2.w = u_xlat33 * 0.5;
						u_xlat1.xy = u_xlat2.zz + u_xlat2.xw;
						u_xlat2.xyz = hlslcc_mtx4x4_TemporalViewDirParams[3].xyz + (-_WorldSpaceCameraPos.xyz);
						u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
						u_xlat1 = texture(_TemporalCloudRenderTarget, u_xlat1.xy);
						u_xlat33 = u_xlat1.x + u_xlat1.x;
						u_xlat33 = exp2(u_xlat33);
						u_xlat1.x = u_xlat33 + -1.0;
						u_xlat3.xy = u_xlat1.zw * float2(10.0, 10.0);
						u_xlat3.xy = exp2(u_xlat3.xy);
						u_xlat3.xy = u_xlat3.xy + float2(-1.0, -1.0);
						u_xlat0.x = dot(u_xlat2.xyz, u_xlat0.xyz);
						u_xlat0.xy = u_xlat0.xx + u_xlat3.xy;
						u_xlat1.zw = max(u_xlat0.xy, float2(0.0, 0.0));
						u_xlat0.x = dot(u_xlat1, float4(1.0, 1.0, 1.0, 1.0));
						#ifdef UNITY_ADRENO_ES3
            u_xlatb0 = !!(u_xlat0.x==u_xlat0.x);
						#else
						u_xlatb0 = u_xlat0.x == u_xlat0.x;
						#endif
						u_xlat11 = u_xlat1.w + u_xlat7.z;
						#ifdef UNITY_ADRENO_ES3
            u_xlatb11 = !!(u_xlat11<2000.0);
						#else
						u_xlatb11 = u_xlat11 < 2000.0;
						#endif
						u_xlat22 = (-u_xlat7.z) + u_xlat1.w;
						#ifdef UNITY_ADRENO_ES3
            u_xlatb22 = !!(8.0<abs(u_xlat22));
						#else
						u_xlatb22 = 8.0 < abs(u_xlat22);
						#endif
						u_xlatb11 = u_xlatb22 && u_xlatb11;
						u_xlat11 = u_xlatb11 ? 1.0 : float(0.0);
						u_xlat22 = float(1.0) / _TemporalHistoryFrames;
						u_xlat11 = u_xlat11 + u_xlat22;
						#ifdef UNITY_ADRENO_ES3
            u_xlat11 = min(max(u_xlat11, 0.0), 1.0);
						#else
						u_xlat11 = clamp(u_xlat11, 0.0, 1.0);
						#endif
						u_xlat2 = (-u_xlat1) + u_xlat7.xwyz;
						u_xlat1 = float4(u_xlat11) * u_xlat2 + u_xlat1;
						u_xlat7 = (bool(u_xlatb0)) ? u_xlat1.xzwy : u_xlat7;
						//ENDIF
					}
					//ENDIF
				}
				u_xlat0.xyz = u_xlat7.xyz + float3(1.0, 1.0, 1.0);
				u_xlat0.xyz = log2(u_xlat0.xyz);
				u_xlat7.xyz = u_xlat0.xyz * float3(0.5, 0.100000001, 0.100000001);
				SV_Target0 = u_xlat7.xwyz;
				return;
			}
			ENDHLSL
		}
	}
}