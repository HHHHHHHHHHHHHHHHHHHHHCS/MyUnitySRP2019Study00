﻿Shader "MyRP/Transparent"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "white" { }
		_Color ("Color", Color) = (1, 1, 1, 1)
		_Normal ("Normal Texture", 2D) = "bump" { }
		_BumpScale ("Bump Scale", Float) = 1.0
		_Roughness ("Roughness", Range(0, 10)) = 0
		_F0 ("Fresnel 0", Range(0, 1)) = 0.2
		_FresnelExponent ("Fresnel Exponent", Range(1, 64)) = 5
	}
	
	HLSLINCLUDE
	
	#include "UnityCG.cginc"
	#include "Lib.hlsl"
	
	struct DepthPeelingOutput
	{
		float4 color: SV_TARGET0;
		float depth: SV_TARGET1;
	};
	
	float4 _Color;
	sampler2D _MainTex;
	sampler2D _Normal;
	float4 _Normal_ST;
	float _BumpScale;
	
	float4 _Roughness;
	float _F0;
	float _FresnelExponent;
	
	sampler2D _MaxDepthTex;
	sampler2D _DepthTex;
	
	float4 RenderFragment(v2f_legacy i)
	{
		float4 packNormal = tex2D(_Normal, i.uv);
		float3 normal = UnpackNormal(packNormal);
		normal.xy *= _BumpScale;
		normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
		normal = normalize(TangentSpaceToWorld(i, normal));
		
		float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
		float3 reflectDir = normalize(reflect(-viewDir, normal));
		float3 reflection = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectDir, _Roughness);
		float nl = dot(viewDir, normal) > 0?dot(viewDir, normal): dot(viewDir, -normal);//透明的处理
		float fresnel = saturate(FresnelFunc(_F0, nl, _FresnelExponent));
		float4 color = tex2D(_MainTex, i.uv) * _Color;
		
		//color = dot(viewDir, normal) > 0 ? lerp(color, float4(reflection, 1), fresnel): color;
		//color = lerp(color, float4(reflection, 1), fresnel);
		color.rgb = color.rgb * color.a * (1 - fresnel) + reflection.rgb * fresnel;
		color.a = lerp(color.a, 1, fresnel);
		return color;
	}
	
	//default
	float4 frag(v2f_legacy i): SV_TARGET
	{
		return RenderFragment(i);
	}
	
	//First
	DepthPeelingOutput DepthPeelingFirstPass(v2f_legacy i)
	{
		DepthPeelingOutput o;
		o.color = RenderFragment(i);
		o.depth = i.pos.z;
		return o;
	}
	
	//for render 剔除没有叠加的部分
	//越近深度越大 应该要考虑OPENGL/DX 深度相反的问题
	DepthPeelingOutput DepthPeelingPass(v2f_legacy i)
	{
		i.screenPos /= i.screenPos.w;
		float maxDepth = tex2D(_MaxDepthTex, i.screenPos.xy).r;
		float selfDepth = i.pos.z;
		if (selfDepth >= maxDepth)
			clip(-1);//discard;//效果一样
		
		//clip(maxDepth - i.pos.z);//这样写没有考虑 == 的情况
		
		DepthPeelingOutput o;
		o.color = RenderFragment(i);
		o.depth = i.pos.z;
		return o;
	}
	
	//for add
	float4 BlitAddPass(v2f_legacy i, out float depthOut: SV_DEPTH): SV_TARGET
	{
		float4 color = tex2D(_MainTex, i.uv);
		float depth = tex2D(_DepthTex, i.uv);
		clip(depth <= 0? - 1: 1);
		depthOut = depth;
		return color;
	}
	
	//final  这样子透明是不会被遮挡的  还需要专门的深度处理
	void FinalPass(v2f_legacy i, out float4 colorOut: SV_TARGET, out float depthOut: SV_DEPTH)
	{
		float4 color = tex2D(_MainTex, i.uv);
		float depth = tex2D(_DepthTex, i.uv);
		clip(depth <= 0? - 1: 1);
		depthOut = depth;
		colorOut = color;
	}
	
	ENDHLSL
	
	SubShader
	{
		Tags
		{ "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True"
			/*"RenderPipeline" = "MyRenderPipeline"*/
		}
		
		Pass
		{
			Tags { "LightMode" = "TransparentBack" }
			
			ZWrite Off
			ZTest On
			Blend One OneMinusSrcAlpha
			Cull Front
			
			HLSLPROGRAM
			
			#pragma vertex vert_legacy
			#pragma fragment frag
			
			
			ENDHLSL
			
		}
		
		
		// #1
		Pass
		{
			Name "TransparentFront"
			Tags { "LightMode" = "TransparentFront" }
			
			ZWrite Off
			ZTest On
			Blend One OneMinusSrcAlpha
			Cull Back
			
			HLSLPROGRAM
			
			#pragma vertex vert_legacy
			#pragma fragment frag
			
			ENDHLSL
			
		}
		
		
		// #2
		Pass
		{
			Tags { "LightMode" = "DepthPeelingFirstPass" }
			
			ZWrite On
			ZTest LEqual
			Cull Off
			
			HLSLPROGRAM
			
			#pragma vertex vert_legacy
			#pragma fragment DepthPeelingFirstPass
			
			ENDHLSL
			
		}
		
		// #3
		Pass
		{
			Tags { "LightMode" = "DepthPeelingPass" }
			
			ZWrite On
			ZTest LEqual
			Cull Off
			
			HLSLPROGRAM
			
			#pragma vertex vert_legacy
			#pragma fragment DepthPeelingPass
			
			ENDHLSL
			
		}
		
		
		
		// #4
		Pass
		{
			Tags { "LightMode" = "DepthPeelingBlitAddPass" }
			
			ZWrite On
			ZTest LEqual
			Cull Off
			Blend One OneMinusSrcAlpha
			
			HLSLPROGRAM
			
			#pragma vertex vert_legacy
			#pragma fragment BlitAddPass
			
			ENDHLSL
			
		}
		
		// #5
		Pass
		{
			Tags { "LightMode" = "DepthPeelingFinalPass" }
			
			ZWrite On
			ZTest LEqual
			Cull Off
			Blend One OneMinusSrcAlpha
			
			HLSLPROGRAM
			
			#pragma vertex vert_legacy
			#pragma fragment FinalPass
			
			ENDHLSL
			
		}
	}
}
