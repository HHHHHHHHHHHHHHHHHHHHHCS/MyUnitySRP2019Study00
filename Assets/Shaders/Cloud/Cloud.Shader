Shader "MyRP/Cloud"
{
	Properties
	{
		_NoiseTex ("Noise Texture", 3D) = "white" { }
		_DetailNoiseTex ("Detail Noise Texture", 3D) = "white" { }
		_HeightGradient ("Height Gradient", 2D) = "white" { }
		_CoverageTex ("Coverage Texture", 2D) = "white" { }
		_CoverageScale ("Coverage Scale", Float) = 1
		// _Color ("Color", Color) = (1, 1, 1, 1)
		// _LightColor ("Color", Color) = (1, 1, 1, 1)
		// _DarkColor ("Color", Color) = (.5, .5, .5, 1)
		// _AmbientStrength ("AmbientStrength", Float) = 1
		_Scale ("Scale", Float) = 1
		_HeightScale ("Height Scale", Float) = 1
		// _HeightOffset ("Height Offset", Range(0, 1)) = 0
		_NoiseAmplitude ("Cloud Noise Amplitude", Vector) = (.5, .25, .125, .0625)
		_DetailAmplitude ("Detail Noise Amplitude", Vector) = (.5, .25, .125, .0625)
		_DetailScale ("Detail Scale", Float) = 1
		_DetailStrength ("Detail Strength", Float) = 1
		// _Near ("Near", Float) = 0
		// _Far ("Far", Float) = 20
		// _Step ("Step", Float) = 0.5
		_Samples ("Sample Count", Int) = 64
		_CloudThreshold ("Cloud Threshold", Range(0, 1)) = .5
		_DensityScale ("Density Scale", Range(0, 2)) = .5
		_LightScale ("Light Scale", Float) = 64
		_GroundRadius ("Ground Radius", Float) = 200
		_CloudBottom ("Min Cloud Altitude", Float) = 80
		_CloudTop ("Max Cloud Altitude", Float) = 90
		// _FBMOctave ("FBM Octave", Int) = 4
		_CloudType ("Cloud Type", Range(0, 1)) = 0
		_CloudShapeExponent ("Cloud Shape Exponent", Float) = 1
		// _ScatterFactor ("Scatter Factor", Range(-1, 1)) = 0
		_ScatterDistanceMultiply ("Scatter Distance Multiply", Float) = .5
		// _ScatterExtend ("Scatter Extend", Float) = 1
		// _OcclusionSampleDistance ("Occlusion Distance", Float) = 1
		_Absorption ("Cloud Absorption", Float) = 1
		_AbsorptionToLight ("Absorption To Light", Float) = 1
		_PowderEffectScale ("Powder Effect", Float) = 1
		_MotionSpeed ("MotionSpeed", Float) = 1
		_NoiseMotionVelocity ("Noise Velocity", Vector) = (0, 0, 0, 0)
		_DetailMotionVelocity ("Detail Velocity", Vector) = (0, 0, 0, 0)
		_CoverageMotionVelocity ("Coverage Velocity", Vector) = (0, 0, 0, 0)
		// _CurlTexScale ("Curl Noise Motion Scale", Float) = 1
		// _CurlMotionStrength ("Curl Noise Motion Strength", Float) = 1
		// _DetailCurlScale ("Detail Curl Motion  Scale", Float) = 1
		// _DetailCurlStrength ("Detail Curl Motion Scale", Float) = 1
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
	
	float4 _MainLightDirection;
	float4 _MainLightColor;
	float4 _AmbientSkyColor;
	sampler2D _CurlNoiseMotionTex;
	
	Texture3D _NoiseTex;
	Texture3D _DetailNoiseTex;
	SamplerState detail_linear_repeat_sampler;
	SamplerState noise_linear_repeat_sampler;
	sampler2D _HeightGradient;
	sampler2D _CoverageTex;
	
	float _CoverageScale;
	float _Scale;
	float _HeightScale;
	float4 _NoiseAmplitude;
	float4 _DetailAmplitude;
	float _DetailScale;
	float _DetailStrength;
	float _Samples;
	float _GroundRadius;
	float _CloudBottom;
	float _CloudTop;
	float _CloudThreshold;
	float _DensityScale;
	float _LightScale;
	float _CloudType;
	float _CloudShapeExponent;
	float _ScatterDistanceMultiply;
	float _Absorption;
	float _AbsorptionToLight;
	float _PowderEffectScale;
	
	float _MotionSpeed;
	float3 _NoiseMotionVelocity;
	float3 _DetailMotionVelocity;
	float2 _CoverageMotionVelocity;
	
	inline float2 CurlNoiseMotion(float2 uv, float uvScale, float strength)
	{
		return tex2D(_CurlNoiseMotionTex, uv.xy / uvScale).xy * strength;
	}
	
	//采取噪音
	float SampleNoise(float3 uv)
	{
		//uv 缩放
		float3 detailUV = uv.xyz / _DetailScale;
		float2 coverageUV = uv.xy / _CoverageScale;
		float3 cloudUV = uv.xyz;
		
		//uv 移动
		float speed = _Time.y * _MotionSpeed;
		detailUV.xyz += speed * _DetailMotionVelocity;
		//detailUV.xy += CurlNoiseMotion(uv.xy, _DetailCurlScale, _DetailCurlStrength);
		coverageUV += speed * _CoverageMotionVelocity;
		cloudUV.xyz += speed * _NoiseMotionVelocity;
		//cloudUV.xy += CurlNoiseMotion(uv.xy, _CurlTexScale, _CurlMotionStrength);
		
		
		//距离决定 detailNoise
		float4 detailNoise = _DetailNoiseTex.Sample(detail_linear_repeat_sampler, detailUV).rgba;
		float dn = dot(detailNoise, _DetailAmplitude);
		dn = lerp(-dn, dn, saturate(cloudUV.z * _HeightScale * 4));
		
		//noise - detailNoise
		float4 noise = _NoiseTex.Sample(noise_linear_repeat_sampler, cloudUV).rgba;
		float n = dot(noise, _NoiseAmplitude);
		n -= dn * _DetailStrength;
		
		//平滑noise
		float coverage = tex2D(_CoverageTex, coverageUV).r;
		
		return n * coverage;
	}
	
	inline float pow2(float3 v)
	{
		return pow(length(v), 2);
	}
	
	//射线距离检测 只是粗略检测一次距离
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
	
	//到球的方向转换为UV坐标
	float3 ToSphereCoord(float3 p)
	{
		float2 u = normalize(float2(p.y, p.z));
		float v = normalize(p).x;
		return float3(atan2(u.y, u.x), asin(v.x), length(p));
	}
	
	//云的形状
	float CloudShape(float height)
	{
		//利用高度图计算云的形状
		float4 heightGradient = tex2D(_HeightGradient, float2(0.5, height));
		
		float a = lerp(heightGradient.r, heightGradient.g, smoothstep(0, 0.5, _CloudType)),
		float b = lerp(a, heightGradient.b, smoothstep(.5, 1, _CloudType));
		float c = pow(b * heightGradient.a, _CloudShapeExponent);
		return c;
	}
	
	//云的强度
	inline float cloudDensity(float noise)
	{
		//(噪音-云的阀值)*强度
		return saturate((noise - _CloudThreshold) / (1 - _CloudThreshold)) * _DensityScale;
	}
	
	
	//云的强度
	float CloudDensityAt(float3 pos, float3 earthCenter)
	{
		float3 coord = ToSphereCoord(pos - earthCenter);
		float2 coverageUV = coord.xy / _CoverageScale;
		coord.xy /= _Scale;
		float height = smoothstep(_GroundRadius + _CloudBottom, _GroundRadius + _CloudTop, coord.z);//距离转换云层的高度
		coord.z = height / _HeightScale;
		//噪音*云的强度
		return CloudDensity(SampleNoise(coord) * CloudShape(height));// * smoothstep(_CoverageThreshold, 1, tex2D(_CoverageTex, coverageUV).r * .5 + .5);
	}
	
	//像光的方向计算遮挡
	float RayMarchOcclusion(float3 pos)
	{
		float occlusion = 0;
		float scatterDistance = (_CloudTop - _CloudBottom) * _ScatterDistanceMultiply;
		//云的强度越大  距离越大  越难穿透 遮挡越强
		[unroll]
		for (float i = 1; i < 6; i ++)
		{
			occlusion += CloudDensityAt(pos + (-_MainLightDirection * scatterDistance * (i / 5))) * (scatterDistance * .2);
		}
		return occlusion;
	}
	
	#define MAX_ITERATION 128
	#define IGNORE_DENSITY_THRESHOLD (0.00001) // 小于这个值的   不继续计算光照
	
	//云
	float RayMarchingCloud(float3 ray, float3 cameraCoord, float3 earthCenter, out float3 light)
	{
		light = 0;
		
		float near = 0, far = 0;
		float r = cameraCoord.z;
		
		bool bf = RayHitSphere(earthCenter, _WorldCameraPos, ray, _GroundRadius + _CloudBottom, near);
		bool bn = RayHitSphere(earthCenter, _WorldCameraPos, ray, _GroundRadius + _CloudTop, far);
		
		
		
		if (r < _GroundRadius + _CloudBottom)//云下面
		{
			//如果实在太矮了
			if ((_WorldCameraPos + ray * far).y < 0)
			{
				far = 0;
			}
		}
		else if(r < _GroundRadius + _CloudTop)//云里面
		{
			//近处有  优先近处
			if (bn)
			{
				far = near;
				near = 0;
			}
			else
			{
				near = 0;
			}
		}
		else//云上面
		{
			//远处没有
			if (!bf)
			{
				far = 0;
			}
		}
		
		//检测失败
		if (far <= 0)
			return 1;
		
		float stepSize = (far - near) / _Samples;
		//厚度
		float transmittance = 1;
		
		//循环远近 生成厚度的云
		[loop]
		for (float i = 1; i <= _Samples; i ++)
		{
			float dist = lerp(near, far, i / _Samples);
			
			float3 pos = _WorldCameraPos + dist * ray;
			float density = CloudDensityAt(pos);
			
			//厚度叠加
			transmittance *= exp(-density * stepSize * _Absorption);
			
			//强度过小 不生成光照
			if (density < IGNORE_DENSITY_THRESHOLD)
				continue;
			
			float occlusion = RayMarchOcclusion(pos);
			
			//遮挡
			float lightTransmittance = exp(-occlusion * _AbsorptionToLight) * (1 - exp( - (occlusion + 0.01) * 2 * _PowderEffectScale));
			
			//TODO:
			light += _Color * _MainLightColor * _LightScale * lightTransmittance * density * transmittance * stepSize * phaseHG(dot(-ray, -_MainLightDirection));
			
			
			//如果已经太小了 则跳出
			if (transmittance < 0.01)
				break;
		}
		
		return transmittance;
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
		float3 earthCenter = float3(0, -_GroundRadius, 0);
		float d = 0;
		float hit = RayHitSphere(earthCenter, _WorldCameraPos, ray, _GroundRadius + _CloudBottom, d);
		float3 pos = _WorldCameraPos + ray * d;
		float3 coord = ToSphereCoord(pos - earthCenter);
		
		// if(!hit)
		//     return 0;
		// return float4(frac(coord.xy), 0, 1);
		float3 light;
		float transmittance;
		transmittance = RayMarchingCloud(ray, coord, earthCenter, light);
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