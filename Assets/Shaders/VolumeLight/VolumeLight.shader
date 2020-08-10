Shader "MyRP/VolumeLight"
{
	Properties
	{
		[HideInInspector] _MainTex ("MainTex", 2D) = "white" { }
		_ExtinctionTex ("Extinction", 3D) = "white" { }
		_AbsorptionRatio ("Absorption Ration", Range(0, 1)) = 0.5
		_UVScale ("UV Scale", Vector) = (1, 1, 1, 1)
		_Seed ("Seed", Float) = 1
		_HGFactor ("HG Phase Factor", Range(-1, 1)) = 0
	}
	HLSLINCLUDE
	
	#include "Noise.hlsl"
	#include "../Standard/Lib.hlsl"
	#include "../Standard/Lighting.hlsl"
	#include "../Shadow/ShadowLib.hlsl"
	
	sampler2D _MainTex;
	float4 _MainTex_TexelSize;
	sampler3D _ExtinctionTex;
	float _AbsorptionRatio;
	float3 _UVScale;
	float _Seed;
	float _HGFactor;
	
	RWTexture2DArray<half2> _RWVolumeDepthTexture;
	Texture2D<half2> _VolumeDepthTexture;
	sampler2D _SampleNoise;
	float4 _SampleNoise_TexelSize;
	sampler2D _CameraDepthTex;
	
	uint _VolumeIndex;
	float4x4 _LightProjectionMatrix;
	int _Steps;
	float2 _RangeLimit;
	float3 _TransmittanceExtinction;
	float _IncomingLoss;
	float _LightDistance;
	float4 _BoundaryPlanes[6];
	int _BoundaryPlaneCount;
	float4 _FrameSize;
	float _GlobalFogExtinction;
	
	//Noise [0,1]
	float Randf(float2 pos, float seed)
	{
		return Gold_Noise(pos, seed) * 0.5 + 0.5;
	}
	
	//到plane的距离  传入的时候是负数
	float IntersectPlane(float4 plane, float3 origin, float3 dir, out bool intersect)
	{
		float d = dot(dir, plane.xyz);
		intersect = d != 0;
		return - dot(float4(origin.xyz, 1.0), plane) / d;
	}
	
	//到 Boundary Plane 最近距离和最远距离 和 距离差
	float GetBoundary(float3 ray, out float near, out float far)
	{
		float maxNear = _ProjectionParams.y;
		float minFar = _ProjectionParams.z;
		bool intersected = false;
		for (int i = 0; i < _BoundaryPlaneCount; i ++)
		{
			float t = IntersectPlane(_BoundaryPlanes[i], _WorldCameraPos, ray, intersected);
			if (intersected && dot(ray, _BoundaryPlanes[i].xyz) < 0)//front face
			{
				maxNear = max(maxNear, t);
			}
			else if (intersected)
			{
				minFar = min(minFar, t);
			}
		}
		near = maxNear;
		far = minFar;
		return minFar - maxNear;
	}
	
	//让Noise 在整个屏幕进行repeat采样  获取抖动uv
	float SampleOffset(float2 screenPos)
	{
		//([0,1]UV) * 屏幕尺寸 * (1/Noise尺寸)
		return tex2D(_SampleNoise, screenPos * _FrameSize.xy * _SampleNoise_TexelSize.xy) * 2 - 1;
	}
	
	float3 ExtinctionAt(float3 pos)
	{
		return 1 * _TransmittanceExtinction;
	}
	
	//大气散射
	float PhaseHG(float3 lightDir, float3 viewDir)
	{
		float g = _HGFactor;
		return(1 - g * g) / (4 * PI * pow(1 + g * g - 2 * g * dot(viewDir, lightDir), 1.5));
	}
	
	//灯光最终颜色
	float3 LightAt(float3 pos, out float3 lightDir)
	{
		lightDir = normalize(_LightPosition.xyz - pos * _LightPosition.w);
		//如果是direction light 则 用 asset.lightdistance   否则 用 dinstance(lightpos-objPos)
		float3 lightDistance = lerp(_LightDistance, distance(_LightPosition.xyz, pos), _LightPosition.w);
		//根据距离决定 透光量
		float3 transmittance = lerp(1, exp(-lightDistance * _TransmittanceExtinction), _IncomingLoss);
		float3 inScatter = _TransmittanceExtinction * (1 - _AbsorptionRatio);
		
		float3 lightColor = _LightColor.rgb;
		//聚光灯角度 遮挡
		lightColor *= step(_LightCosHalfAngle, dot(lightDir, _LightDirection));
		lightColor *= ShadowAt(pos);//裁剪被遮挡的灯光
		lightColor *= inScatter;
		lightColor *= transmittance;
		
		return lightColor;
	}
	
	//最终颜色 = for(灯光颜色 * 步长散射)
	float3 Scattering(float3 ray, float near, float far, out float3 transmittance)
	{
		transmittance = 1;
		float3 totalLight = 0;
		float stepSize = (far - near) / _Steps;
		[loop]
		for (int i = 1; i < _Steps; i ++)
		{
			float3 pos = _WorldCameraPos + ray * (near + stepSize * i);
			//越远效果越淡
			transmittance *= exp(-stepSize * ExtinctionAt(pos));
			/*if(transmittance.x < 0.01 && transmittance.y < 0.01 && transmittance.z < 0.01)
				break;*/
			float3 lightDir;
			totalLight += transmittance * LightAt(pos, lightDir) * stepSize * PhaseHG(lightDir, -ray);
		}
		return totalLight;
	}
	
	float4 volumeDepth(v2f_default i, fixed facing: VFACE): SV_TARGET
	{
		uint3 coord = uint3(_ScreenParams.xy * i.pos.xy, _VolumeIndex);
		half2 depth = _RWVolumeDepthTexture[coord].rg;
		if (facing > 0)
		{
			depth.r = distance(i.worldPos, _WorldCameraPos);
		}
		else
		{
			depth.g = distance(i.worldPos, _WorldCameraPos);
		}
		_RWVolumeDepthTexture[coord] = depth;
		return float4(depth.rg, 0, 0);
	}
	
	float4 volumeLight(v2f_default i): SV_TARGET
	{
		i.screenPos /= i.screenPos.w;
		
		float3 ray = normalize(i.worldPos - _WorldCameraPos);
		float cameraDepth = tex2D(_CameraDepthTex, i.screenPos.xy).r;
		float near, far, depth;
		depth = GetBoundary(ray, near, far);
		
		//Z Culling  如果比摄像机的当前深度要深 则隐藏
		float3 nearWorldPos = _WorldCameraPos + ray * near;
		float4 p = UnityWorldToClipPos(nearWorldPos);
		p /= p.w;
		clip(p.z - cameraDepth);
		
		//摄像机的UV做射线  返回世界空间的Z
		float cameraWorldDepth = DepthToWorldDistance(i.screenPos.xy, cameraDepth);
		//根据传入的range 限制范围
		far = min(far, cameraWorldDepth);
		near = max(_RangeLimit.x, near);
		far = min(_RangeLimit.y, far);
		
		//抖动采样Noise  让深度进行不规则的长短变化
		float offset = SampleOffset(i.screenPos.xy) * (far - near) / _Steps;
		far += offset;
		near += offset;
		
		float3 transmittance = 1;
		float3 color = 0;
		color = Scattering(ray, near, far, transmittance);
		
		return float4(color, 1);
	}
	
	float4 mixScreen(v2f_default i): SV_TARGET
	{
		float4 col = 0;
		float R = 4;
		for (int j = 0; j < 4; j ++)
		{
			float dist = j * R;
			float noise = Randf(i.uv.xy, _Seed + j);
			dist += noise;
			dist = sqrt(dist);
			
			float2 rot;
			float ang = noise * 2 * PI;
			sincos(ang, rot.x, rot.y);
			float2 offset = rot * dist * _MainTex_TexelSize.xy;
			col += tex2D(_MainTex, i.uv.xy + offset).rgba;
		}
		
		//return tex2D(_MainTex, i.uv.xy);
		return col / 4;
	}
	
	float4 globalFog(v2f_ray i): SV_TARGET
	{
		float3 ray = normalize(i.ray);
		float depth = tex2D(_CameraDepthTex, i.uv).r;
		float3 worldPos = _WorldCameraPos + LinearEyeDepth(depth) * i.ray;
		float z = distance(_WorldCameraPos, worldPos);
		float transmittance = exp(-_GlobalFogExtinction * z);
		
		float3 color = _AmbientLight * (1 - transmittance);
		return float4(color.rgb, 1 - transmittance);
	}
	
	ENDHLSL
	
	SubShader
	{
		//0.Distance between front & backface by debug
		Pass
		{
			Name "Volume Light Distance"
			ZTest Off
			ZWrite Off
			Cull Off
			Blend One Zero
			
			HLSLPROGRAM
			
			#pragma target 5.0
			#pragma vertex vert_default
			#pragma fragment volumeDepth
			
			ENDHLSL
			
		}
		
		//1.Volume Spot Light Scattering
		//顶点阶段是to clip 的
		Pass
		{
			Name"Volume Spot Light Scattering"
			ZTest Less
			ZWrite Off
			Cull Front //正面没有用 裁剪正面的
			Blend One One
			
			HLSLPROGRAM
			
			#pragma target 5.0
			#pragma vertex vert_default
			#pragma fragment volumeLight
			
			ENDHLSL
			
		}
		
		//2.Volume Direction Light Scattering
		//顶点阶段是blit的
		Pass
		{
			Name "Volume Direction Light Scattering"
			ZTest Less
			ZWrite Off
			Cull Off
			Blend One One
			
			HLSLPROGRAM
			
			#pragma target 5.0
			#pragma vertex vert_blit_default
			#pragma fragment volumeLight
			
			ENDHLSL
			
		}
		
		//3.Blit to Screen Buffer
		Pass
		{
			Name "Volume Light  Mixed"
			ZTest Off
			ZWrite On
			Cull Back
			Blend One One
			
			HLSLPROGRAM
			
			#pragma target 5.0
			#pragma vertex vert_default
			#pragma fragment mixScreen
			
			ENDHLSL
			
		}
		
		//4.Global Fog
		Pass
		{
			Name "Global Fog"
			ZTest Off
			ZWrite On
			Cull Off
			Blend One OneMinusSrcAlpha
			
			HLSLPROGRAM
			
			#pragma target 5.0
			#pragma vertex vert_ray
			#pragma fragment globalFog
			
			ENDHLSL
			
		}
	}
}
