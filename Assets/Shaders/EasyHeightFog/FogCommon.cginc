#ifndef __FOG_COMMON_CGINC__
	#define __FOG_COMMON_CGINC__
	
	#include "UnityCG.cginc"
	
	#define My_FOG_COORDS(ID) float4 _fogCoord: TEXCOORD##ID;
	
	#define My_TRANSFER_FOG(v2f, vertex) v2f##._fogCoord = GetExponentialHeightFog(-WorldSpaceViewDir(vertex))
	
	#define My_APPLY_FOG(v2f, pixelColor) pixelColor = half4(pixelColor.rgb * v2f._fogCoord.a + v2f._fogCoord.rgb, pixelColor.a)
	
	
	// x : FogDensity * exp2(-FogHeightFalloff * (CameraWorldPosition.y - FogHeight))
	// y : FogHeightFalloff
	// [useless] z : CosTerminatorAngle
	// w : StartDistance
	float4 _ExponentialFogParameters;
	
	// FogDensitySecond * exp2(-FogHeightFalloffSecond * (CameraWorldPosition.y - FogHeightSecond))
	// FogHeightFalloffSecond
	// FogDensitySecond
	// FogHeightSecond
	float4 _ExponentialFogParameters2;
	
	// FogDensity in x
	// FogHeight in y
	// [useless] whether to use cubemap fog color in z
	// FogCutoffDistance in w
	float4 _ExponentialFogParameters3;
	
	// xyz : directinal inscattering color
	// w : cosine exponent
	float4 _DirectionalInscatteringColor;
	
	// xyz : directional light's direction. 方向光照射方向的反方向
	// w : direactional inscattering start distance
	float4 _InscatteringLightDirection;
	
	// xyz : fog inscattering color
	// w : min transparency
	float4 _ExponentialFogColorParameter;
	
	static const float FLT_EPSILON2 = 0.01f;
	
	float Pow2(float x)
	{
		return x * x;
	}
	
	// UE 4.22 HeightFogCommon.ush
	// Calculate the line integral of the ray from the camera to the receiver position through the fog density function
	// The exponential fog density function is d = GlobalDensity * exp(-HeightFalloff * y)
	float CalculateLineIntegralShared(float FogHeightFalloff, float RayDirectionY, float RayOriginTerms)
	{
		float Falloff = max(-127.0f, FogHeightFalloff * RayDirectionY);    // if it's lower than -127.0, then exp2() goes crazy in OpenGL's GLSL.
		float LineIntegral = (1.0f - exp2(-Falloff)) / Falloff;
		float LineIntegralTaylor = log(2.0) - (0.5 * Pow2(log(2.0))) * Falloff;		// Taylor expansion around 0
		
		return RayOriginTerms * (abs(Falloff) > FLT_EPSILON2 ? LineIntegral: LineIntegralTaylor);
	}
	
	
	// UE 4.22 HeightFogCommon.ush
	// @param WorldPositionRelativeToCamera = WorldPosition - InCameraPosition
	half4 GetExponentialHeightFog(float3 WorldPositionRelativeToCamera) // camera to vertex
	{
		const half MinFogOpacity = _ExponentialFogColorParameter.w;
		
		// Receiver 指着色点
		float3 CameraToReceiver = WorldPositionRelativeToCamera;
		float CameraToReceiverLengthSqr = dot(CameraToReceiver, CameraToReceiver);
		float CameraToReceiverLengthInv = rsqrt(CameraToReceiverLengthSqr); // 平方根的倒数
		float CameraToReceiverLength = CameraToReceiverLengthSqr * CameraToReceiverLengthInv;
		half3 CameraToReceiverNormalized = CameraToReceiver * CameraToReceiverLengthInv;
		
		// FogDensity * exp2(-FogHeightFalloff * (CameraWorldPosition.y - FogHeight))
		float RayOriginTerms = _ExponentialFogParameters.x;
		float RayOriginTermsSecond = _ExponentialFogParameters2.x;
		float RayLength = CameraToReceiverLength;
		float RayDirectionY = CameraToReceiver.y;
		
		// Factor in StartDistance
		// _ExponentialFogParameters.w 是 StartDistance
		float ExcludeDistance = _ExponentialFogParameters.w;
		
		if (ExcludeDistance > 0)
		{
			// 到相交点所占时间
			float ExcludeIntersectionTime = ExcludeDistance * CameraToReceiverLengthInv;
			// 相机到相交点的 y 偏移
			float CameraToExclusionIntersectionY = ExcludeIntersectionTime * CameraToReceiver.y;
			// 相交点的 y 坐标
			float ExclusionIntersectionY = _WorldSpaceCameraPos.y + CameraToExclusionIntersectionY;
			// 相交点到着色点的 y 偏移
			float ExclusionIntersectionToReceiverY = CameraToReceiver.y - CameraToExclusionIntersectionY;
			
			// Calculate fog off of the ray starting from the exclusion distance, instead of starting from the camera
			// 相交点到着色点的距离
			RayLength = (1.0f - ExcludeIntersectionTime) * CameraToReceiverLength;
			// 相交点到着色点的 y 偏移
			RayDirectionY = ExclusionIntersectionToReceiverY;
			// _ExponentialFogParameters.y : height falloff
			// _ExponentialFogParameters3.y ： fog height
			// height falloff * height
			float Exponent = max(-127.0f, _ExponentialFogParameters.y * (ExclusionIntersectionY - _ExponentialFogParameters3.y));
			// _ExponentialFogParameters3.x : fog density
			RayOriginTerms = _ExponentialFogParameters3.x * exp2(-Exponent);
			
			// _ExponentialFogParameters2.y : FogHeightFalloffSecond
			// _ExponentialFogParameters2.w : fog height second
			float ExponentSecond = max(-127.0f, _ExponentialFogParameters2.y * (ExclusionIntersectionY - _ExponentialFogParameters2.w));
			RayOriginTermsSecond = _ExponentialFogParameters2.z * exp2(-ExponentSecond);
		}
		
		// Calculate the "shared" line integral (this term is also used for the directional light inscattering) by adding the two line integrals together (from two different height falloffs and densities)
		// _ExponentialFogParameters.y : fog height falloff
		float ExponentialHeightLineIntegralShared = CalculateLineIntegralShared(_ExponentialFogParameters.y, RayDirectionY, RayOriginTerms)
		+ CalculateLineIntegralShared(_ExponentialFogParameters2.y, RayDirectionY, RayOriginTermsSecond);
		// fog amount，最终的积分值
		float ExponentialHeightLineIntegral = ExponentialHeightLineIntegralShared * RayLength;
		
		// 雾色
		half3 InscatteringColor = _ExponentialFogColorParameter.xyz;
		half3 DirectionalInscattering = 0;
		
		// if _InscatteringLightDirection.w is negative then it's disabled, otherwise it holds directional inscattering start distance
		if (_InscatteringLightDirection.w >= 0)
		{
			float DirectionalInscatteringStartDistance = _InscatteringLightDirection.w;
			// Setup a cosine lobe around the light direction to approximate inscattering from the directional light off of the ambient haze;
			half3 DirectionalLightInscattering = _DirectionalInscatteringColor.xyz * pow(saturate(dot(CameraToReceiverNormalized, _InscatteringLightDirection.xyz)), _DirectionalInscatteringColor.w);
			
			// Calculate the line integral of the eye ray through the haze, using a special starting distance to limit the inscattering to the distance
			float DirExponentialHeightLineIntegral = ExponentialHeightLineIntegralShared * max(RayLength - DirectionalInscatteringStartDistance, 0.0f);
			// Calculate the amount of light that made it through the fog using the transmission equation
			half DirectionalInscatteringFogFactor = saturate(exp2(-DirExponentialHeightLineIntegral));
			// Final inscattering from the light
			DirectionalInscattering = DirectionalLightInscattering * (1 - DirectionalInscatteringFogFactor);
		}
		
		// Calculate the amount of light that made it through the fog using the transmission equation
		// 最终的系数
		half ExpFogFactor = max(saturate(exp2(-ExponentialHeightLineIntegral)), MinFogOpacity);
		
		// _ExponentialFogParameters3.w : FogCutoffDistance
		if (_ExponentialFogParameters3.w > 0 && CameraToReceiverLength > _ExponentialFogParameters3.w)
		{
			ExpFogFactor = 1;
			DirectionalInscattering = 0;
		}
		
		half3 FogColor = (InscatteringColor) * (1 - ExpFogFactor) + DirectionalInscattering;
		
		return half4(FogColor, ExpFogFactor);
	}
	
	
#endif //__FOG_COMMON_CGINC__
