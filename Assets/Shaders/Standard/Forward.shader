Shader"MyRP/ForwardDefault"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "white" { }
		_Color ("Color", Color) = (1, 1, 1, 1)
		_Normal ("Normal texture", 2D) = "bump" { }
		_BumpScale ("Bump scale", Float) = 1.0
	}
	
	HLSLINCLUDE
	
	#include "UnityCG.cginc"
	#include "./Lib.hlsl"
	#include "./Light.hlsl"
	#include "./Shadow/ShadowLib.hlsl"
	
	float4 _Color;
	sampler2D _MainTex;
	float4 _MainTex_ST;
	sampler2D _Normal;
	float4 _Normal_ST;
	float _BumpScale;
	
	float4 Light(v2f_defulat i, float3 ambient)
	{
		i.uv = TRANSFORM_TEX(i.uv, _MainTex);
		float4 albedo = tex2D(_MainTex, i.uv) * _Color;
		float4 packNormal = tex2D(_Normal, i.uv);
		float3 normal = UnpackNormal(packNormal);
		normal.xy *= _BumpScale;
		normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
		normal = TangentSpaceToWorld(i.normal, i.tangent, normal);
		
		float3 lightDir, lightColor;
		LightAt(i.worldPos, lightDir, lightColor);
		lightColor *= ShadowAt(i);
		
		float3 diffuse = BRDF_Lambertian(albedo);
		float3 color = PBR_Light(diffuse, lightColor, lightDir, normal) + ambient * albedo;
		
		return float4(color.rgb, albedo.a);
	}
	
	float4 forwardBase(v2f_default i): SV_TARGET
	{
		return light(i, _AmbientLight);
	}
	
	float4 forwardAdd(v2f_default i): SV_TARGET
	{
		return light(i, 0);
	}
	
	ENDHLSL
	
	SubShader
	{
		Tags
		{ 
			"RenderType" = "Opaque"
			"RenderPipeline" = "MyRenderPipeline" 
			"IgnoreProjector" = "true"
		}
		
		Pass
		{
			Tags { "LightMode" = "ForwardBase" }
			
			Cull Back
			ZWrite On
			ZTest Less
			
			HLSLPROGRAM
			
			#pragma vertex vert_default
			#pragma fragment forwardBase
			
			ENDHLSL
			
		}
		
		Pass
		{
			Tags { "LightMode" = "ForwardAdd" }
			
			Cull Back
			Blend One One
			ZWrite Off
			ZTest LEqual
			
			HLSLPROGRAM
			
			#pragma vertex vert_default
			#pragma fragment forwardAdd
			
			ENDHLSL
			
		}
		
		/*
		//Unity强制规定 需要LightMode = MotionVectors
		Pass
		{
			Name "MotionVectors"
			
			Tags { "LightMode" = "MotionVectors" }
			
			Cull Back
			ZWrite On
			ZTest Less
			
			HLSLPROGRAM
			
			#include "./VelocityBuffer.hlsl"
			
			#pragma vertex vert_velocity
			#pragma fragment frag_velocity
			
			#define SHADERPASS SHADERPASS_MOTION_VECTORS
			
			ENDHLSL
			
		}
		*/
	}
	CustomEditor "MyRenderPipeline.Editor.Material.ForwardLitEditorGUI"
}