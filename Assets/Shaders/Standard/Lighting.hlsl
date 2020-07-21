#ifndef MYRP_LIGHT
	#define MYRP_LIGHT
	
	float4 _AmbientLight;
	float4 _MainLightPosition;
	float4 _MainLightColor;
	float4 _LightPosition;
	float4 _LightColor;
	float3 _LightDirection;
	float _LightCosHalfAngle;
	
	
	#define PI 3.14159265358979323846264338327950288419716939937510
	
	float3 BRDF_Lambertian(float3 albedo)
	{
		return albedo / PI;
	}
	
	float3 PBR_Light(float3 brdf, float3 lightColor, float3 lightDir, float3 normal)
	{
		return PI * lightColor * brdf * saturate(dot(lightDir, normal));
	}
	
	void LightAt(float3 worldPos, out float3 lightDir, out float3 lightColor)
	{
		lightDir = normalize(_LightPosition.xyz - worldPos * _LightPosition.w);
		lightColor = _LightColor.rgb * step(_LightCosHalfAngle, dot(lightDir, _LightDirection));
		return;
	}
	
#endif