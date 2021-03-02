Shader "MyRP/Cloud/ParallaxCloud"
{
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		_MainTex ("MainTex", 2D) = "white" { }
		_Alpha ("Alpha", Range(0, 1)) = 0.5
		_Height ("Displacement Amount", range(0, 1)) = 0.15
		_HeightAmount ("Turbulence Amount", range(0, 2)) = 1
		_HeightTileSpeed ("Turbulence Tile&Speed", Vector) = (1.0, 1.0, 0.05, 0.0)
		_LightIntensity ("Ambient Intensity", Range(0, 3)) = 1.0
		[Toggle] _UseFixedLight ("Use Fixed Light", Int) = 1
		_FixedLightDir ("Fixed Light Direction", Vector) = (0.981, 0.122, -0.148, 0.0)
	}
	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent-1" "IgnoreProjector" = "True" }
		LOD 100
		
		Pass
		{
			// Name "Cloud"
			// Tags { "LightMode" = "Transparent" }
			
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			
			
			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			float4 _MainTex_ST;
			half _Height;
			float4 _HeightTileSpeed;
			half _HeightAmount;
			half4 _Color;
			half _Alpha;
			half _LightIntensity;
			half _DirectLightAmount;
			half4 _LightingColor;
			half4 _FixedLightDir;
			half _UseFixedLight;
			
			struct a2v
			{
				float4 vertex: POSITION;
				float4 normal: NORMAL;
				float4 tangent: TANGENT;
				float4 color: COLOR;
				float2 texcoord: TEXCOORD0;
			};
			
			struct v2f
			{
				float4 pos: SV_POSITION;
				float4 uv12: TEXCOORD0;
				float3 normalDir: TEXCOORD1;
				float3 viewDir: TEXCOORD2;
				float3 wPos: TEXCOORD3;
				float4 color: TEXCOORD4;
			};
			
			v2f vert(a2v v)
			{
				v2f o = (v2f)o;
				
				o.wPos = TransformObjectToWorld(v.vertex.xyz);
				o.pos = TransformWorldToHClip(o.wPos.xyz);
				o.uv12.xy = TRANSFORM_TEX(v.texcoord, _MainTex) + frac(_Time.y * _HeightTileSpeed.zw);
				o.uv12.zw = v.texcoord * _HeightTileSpeed.xy;
				o.normalDir = TransformObjectToWorldNormal(v.normal.xyz);
				o.color = v.color;
				float3 wViewDir = TransformWorldToObject(_WorldSpaceCameraPos) - v.vertex.xyz;
				float3 binormal = cross(normalize(v.normal.xyz), normalize(v.tangent.xyz)) * v.tangent.w * GetOddNegativeScale();
				float3x3 TBN = float3x3(v.tangent.xyz, binormal, v.normal.xyz);
				o.viewDir = mul(TBN, wViewDir);
				
				return o;
			}
			
			half4 frag(v2f IN): SV_TARGET
			{
				float3 viewRay = -1 * normalize(IN.viewDir);
				viewRay.xy *= _Height;
				viewRay.z = abs(viewRay.z) + 0.2;
				
				
				float3 shadeP = float3(IN.uv12.xy, 0.0);
				float3 shadeP2 = float3(IN.uv12.zw, 0.0);
				
				const float linearStep = 16;
				
				half4 T = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadeP2.xy);
				float h2 = T.a * _HeightAmount;
				float d = 1.0 - SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, shadeP.xy, 0).a * h2;
				
				
				//mainLight
				float3 lioffset = viewRay / (viewRay.z * linearStep);
				float prev_d = d;
				float3 prev_shadeP = shadeP;
				float3 mainP = shadeP;
				
				while(d > mainP.z)
				{
					prev_shadeP = mainP;
					mainP += lioffset;
					prev_d = d;
					d = 1.0 - SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, mainP.xy, 0).a * h2;
				}
				
				
				float d1 = d - mainP.z;
				float d2 = prev_d - prev_shadeP.z;
				float w = d1 / (d1 - d2);
				mainP = lerp(mainP, prev_shadeP, w);
				
				half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainP.xy) * T * _Color;
				half alpha = lerp(c.a, 1.0, _Alpha) * IN.color.r;
				
				
				//GetMainLight()
				float3 normal = normalize(IN.normalDir);
				half3 lightDir1 = normalize(_FixedLightDir.xyz);
				half3 lightDir2 = _MainLightPosition.xyz;
				half3 lightDir = lerp(lightDir2, lightDir1, _UseFixedLight);
				float NdotL = max(0, dot(normal, lightDir));
				half3 lightColor = _MainLightColor.rgb;
				half3 finalColor = c.rgb * (NdotL * lightColor + 1.0);
				
				float3 sioffset = viewRay / viewRay.z;
				float3 addShadP = shadeP + sioffset * d;
				
				c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, addShadP.xy) * T * _Color;
				alpha = lerp(c.a, 1.0, _Alpha) * IN.color.r;
				
				
				int additionalCount = GetAdditionalLightsCount();
				for (int i = 0; i < additionalCount; i ++)
				{
					Light light = GetAdditionalLight(i, IN.wPos);
					lightDir2 = light.direction;
					NdotL = max(0, dot(normal, lightDir));
					lightColor = light.color * light.distanceAttenuation;
					finalColor += c.rgb * (NdotL * lightColor + unity_AmbientEquator.rgb);
				}
				
				return half4(finalColor.rgb, alpha);
			}
			
			
			ENDHLSL
			
		}
	}
}
