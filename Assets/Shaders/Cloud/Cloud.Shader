Shader "MyRP/Cloud"
{
	Properties
	{
		_NoiseTex ("Noise Texture", 3D) = "white" { }
		_DetailNoiseTex ("Detail Noise Texture", 3D) = "white" { }
		_HeightGradient ("Height Gradient", 2D) = "white" { }
		_CoverageTex ("Coverage Texture", 2D) = "white" { }
		_CoverageScale ("Coverage Scale", Float) = 1
		_Color ("Color", Color) = (1, 1, 1, 1)
		_LightColor ("Color", Color) = (1, 1, 1, 1)
		_DarkColor ("Color", Color) = (.5, .5, .5, 1)
		_AmbientStrength ("AmbientStrength", Float) = 1
		_Scale ("Scale", Float) = 1
		_HeightScale ("Height Scale", Float) = 1
		_HeightOffset ("Height Offset", Range(0, 1)) = 0
		_NoiseAmplitude ("Cloud Noise Amplitude", Vector) = (.5, .25, .125, .0625)
		_DetailAmplitude ("Detail Noise Amplitude", Vector) = (.5, .25, .125, .0625)
		_DetailScale ("Detail Scale", Float) = 1
		_DetailStrength ("Detail Strength", Float) = 1
		_Near ("Near", Float) = 0
		_Far ("Far", Float) = 20
		_Step ("Step", Float) = 0.5
		_Samples ("Sample Count", Int) = 64
		_CloudThreshold ("Cloud Threshold", Range(0, 1)) = .5
		_DensityScale ("Density Scale", Range(0, 2)) = .5
		_LightScale ("Light Scale", Float) = 64
		_GroundRadius ("Ground Radius", Float) = 200
		_CloudBottom ("Min Cloud Altitude", Float) = 80
		_CloudTop ("Max Cloud Altitude", Float) = 90
		_FBMOctave ("FBM Octave", Int) = 4
		_CloudType ("Cloud Type", Range(0, 1)) = 0
		_CloudShapeExponent ("Cloud Shape Exponent", Float) = 1
		_ScatterFactor ("Scatter Factor", Range(-1, 1)) = 0
		_ScatterDistanceMultiply ("Scatter Distance Multiply", Float) = .5
		_ScatterExtend ("Scatter Extend", Float) = 1
		_OcclusionSampleDistance ("Occlusion Distance", Float) = 1
		_Absorption ("Cloud Absorption", Float) = 1
		_AbsorptionToLight ("Absorption To Light", Float) = 1
		_PowderEffectScale ("Powder Effect", Float) = 1
		_MotionSpeed ("MotionSpeed", Float) = 1
		_NoiseMotionVelocity ("Noise Velocity", Vector) = (0, 0, 0, 0)
		_DetailMotionVelocity ("Detail Velocity", Vector) = (0, 0, 0, 0)
		_CoverageMotionVelocity ("Coverage Velocity", Vector) = (0, 0, 0, 0)
		_CurlTexScale ("Curl Noise Motion Scale", Float) = 1
		_CurlMotionStrength ("Curl Noise Motion Strength", Float) = 1
		_DetailCurlScale ("Detail Curl Motion  Scale", Float) = 1
		_DetailCurlStrength ("Detail Curl Motion Scale", Float) = 1
	}
	
	HLSLINCLUDE
	
	#include "UnityCG.cginc"
	
	struct v2f
	{
		float4 pos: SV_POSITION;
		float2 uv: TEXCOORD0;
		float3 ray: TEXCOORD1;
		float3 worldPos: TEXCOORD2;
	};
	
	float3 _WorldCameraPos;
	float4x4 _ViewProjectionInverseMatrix;
	
	float _CloudBottom;
	float _GroundRadius;
	
	inline float pow2(float3 v)
	{
		return pow(length(v), 2);
	}
	
	//射线距离检测
	bool RayHitSphere(float3 o, float3 p, float3 ray, float r, out float t)
	{
		t = 0;

		//如果超出了 检测的距离
		if (pow(dot(ray, o - p), 2) - pow2(ray) * (pow2(o - p) - pow(r, 2)) < 0)
		{
			return false;
		}
		
		/*
		t = length(p - o) >= r
		? (1/pow2(ray) * (
			dot(ray, o)
		- dot(ray, p)
		- .5*sqrt(
			4*pow(dot(ray, o-p), 2)
		- 4*pow2(ray)*(
			pow2(o - p) -
			pow(r, 2)
		)
		)))
		: (1/pow2(ray) * (
			dot(ray, o)
		- dot(ray, p)
		+ .5*sqrt(
			4*pow(dot(ray, o-p), 2)
		- 4*pow2(ray)*(
			pow2(o - p) -
			pow(r, 2)
		)
		)
		));
		*/

		/*
		Dot[dir, o]
		- Dot[dir, p]
		+ Sqrt[
		Dot[dir, o - p]^2
		- Norm[dir]^2*(
			Norm[o - p]^2
			- r^2)
		]
		*/
		
		float a = dot(ray, o);
		float b = dot(ray, p);
		float c = pow(dot(ray, o - p), 2);
		float d = pow2(ray) * (pow2(o - p) - pow(r, 2));

		t = a - b;

		if (length(p - o) >= r)
		{
			//在r 的范围内
			t = t - sqrt(c - d);
		}
		else
		{
			//在r 的范围外
			t = t + sqrt(c - d);
		}
		
		if (t < 0)
			return false;
		return true;
	}
	
	v2f cloudSkyVert(appdata_full i)
	{
		v2f o;
		o.pos = float4(i.vertex.x, i.vertex.y * _ProjectionParams.x, 1, 1);
		o.uv = i.texcoord;
		float4 p = float4(i.vertex.x, i.vertex.y, 1, 1);
		p = p * _ProjectionParams.z;//far plane
		o.worldPos = mul(_ViewProjectionInverseMatrix, float4(p.xyzw));//view->world
		o.ray = normalize(o.worldPos - _WorldCameraPos);
		return o;
	}
	
	float4 CloudOnSkyFrag(v2f i): SV_TARGET
	{
		float3 ray = normalize(i.ray);
		float3 earthCenter = float3(0, _GroundRadius, 0);
		float d = 0;
		float hit = RayHitSphere(earthCenter, _WorldCameraPos, ray, _GroundRadius + _CloudBottom, d);
	}
	
	ENDHLSL
	
	SubShader
	{
		Pass
		{
			Cull Off
			ZWrite Off
			ZTest Off
			Blend One OneMinusSrcAlpha
			
			HLSLPROGRAM
			
			#pragma vertex cloudSkyVert
			#pragma fragment cloudOnSkyFrag
			
			ENDHLSL
			
		}
		
		
		Pass
		{
			Cull Back
			ZWrite Off
			ZTest Off
			Blend One OneMinusSrcAlpha
			
			HLSLPROGRAM
			
			#pragma vertex cloudCubeVert
			#pragma fragment cloudCubeFrag
			
			ENDHLSL
			
		}
	}
}