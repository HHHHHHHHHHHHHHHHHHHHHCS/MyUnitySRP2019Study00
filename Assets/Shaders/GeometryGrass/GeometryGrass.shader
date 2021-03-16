Shader "MyRP/GeometryGrass/GeometryGrass"
{
	Properties
	{
		_BottomColor("Bottom Color", Color) = (0,1,0,1)
		_TopColor("Top Color", Color) = (1,1,0,1)
		_GrassHeight("Grass Height", Float) = 1
		_GrassWidth("Grass Width", Float) = 0.06
		_RandomHeight("Grass Height Randomness", Float) = 0.25
		_WindSpeed("Wind Speed", Float) = 100
		_WindStrength("Wind Strength", Float) = 0.05
		_Radius("Interactor Radius", Float) = 0.3
		_Strength("Interactor Strength", Float) = 5
		_Rad("Blade Radius", Range(0,1)) = 0.6
		_BladeForward("Blade Forward Amount", Float) = 0.38
		_BladeCurve("Blade Curvature Amount", Range(1, 4)) = 2
		_AmbientStrength("Ambient Strength", Range(0,1)) = 0.5
		_MinDist("Min Distance", Float) = 40
		_MaxDist("Max Distance", Float) = 60
	}
	HLSLINCLUDE
	#pragma vertex vert
	#pragma fragment frag
	//https://zhuanlan.zhihu.com/p/107966502
	#pragma require geometry
	#pragma geometry geom

	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

	#define GrassSegments 5 // segments per blade
	#define GrassBlades 4 // blades per vertex

	#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
	#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

	#pragma multi_compile_fog

	struct a2v
	{
		// The positionOS variable contains the vertex positions in object
		// space.
		float4 positionOS : POSITION;
		float3 normal :NORMAL;
		float2 texcoord : TEXCOORD0;
		float4 color : COLOR;
	};

	struct v2g
	{
		float4 pos : SV_POSITION;
		float3 norm : NORMAL;
		float2 uv : TEXCOORD0;
		float3 color : COLOR;
	};

	struct g2f
	{
		float4 pos : SV_POSITION;
		float3 norm : NORMAL;
		float2 uv : TEXCOORD0;
		float3 diffuseColor : COLOR;
		float3 worldPos : TEXCOORD3;
		float3 shadows : TEXCOORD4;
		float fogFactor : TEXCOORD5;
	};

	CBUFFER_START(UnityPerMaterial)
	half _GrassHeight;
	half _GrassWidth;
	half _WindSpeed;
	float _WindStrength;
	half _Radius, _Strength;
	float _Rad;

	float _RandomHeight;
	float _BladeForward;
	float _BladeCurve;

	float _MinDist, _MaxDist;

	float4 _TopColor;
	float4 _BottomColor;
	float _AmbientStrength;
	
	CBUFFER_END
	
	uniform float3 _PositionMoving;


	float rand(float3 co)
	{
		return frac(sin(dot(co.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
	}

	// Construct a rotation matrix that rotates around the provided axis, sourced from:
	// https://gist.github.com/keijiro/ee439d5e7388f3aafc5296005c8c3f33
	float3x3 AngleAxis3x3(float angle, float3 axis)
	{
		float c, s;
		sincos(angle, s, c);

		float t = 1 - c;
		float x = axis.x;
		float y = axis.y;
		float z = axis.z;

		return float3x3(
			t * x * x + c, t * x * y - s * z, t * x * z + s * y,
			t * x * y + s * z, t * y * y + c, t * y * z - s * x,
			t * x * z - s * y, t * y * z + s * x, t * z * z + c
		);
	}


	v2g vert(a2v v)
	{
		// float3 v0 = v.positionOS.xyz;

		v2g OUT;
		OUT.pos = v.positionOS;
		OUT.norm = v.normal;
		OUT.uv = v.texcoord;
		OUT.color = v.color.rgb;
		return OUT;
	}

	g2f GrassVertex(float3 vertexPos, float width, float height, float offset, float curve, float2 uv,
	                float3x3 rotation, float3 faceNormal, float3 color, float3 worldPos)
	{
		g2f OUT;
		OUT.pos = TransformObjectToHClip(
			vertexPos + mul(rotation, float3(width, height, curve) + float3(0, 0, offset)));
		OUT.norm = faceNormal;
		OUT.diffuseColor = color;
		OUT.uv = uv;
		OUT.shadows = TransformObjectToWorld(
			vertexPos + mul(rotation, float3(width, height, curve) + float3(0, 0, offset)));
		OUT.worldPos = worldPos;
		float fogFactor = ComputeFogFactor(OUT.pos.z);

		OUT.fogFactor = fogFactor;


		return OUT;
	}

	[maxvertexcount(48)]
	void geom(point v2g IN[1], inout TriangleStream<g2f> triStream)
	{
		float forward = rand(IN[0].pos.yyz) * _BladeForward;
		Light mainLight = GetMainLight();


		float3 lightPosition = mainLight.direction;

		float3 perpendicularAngle = float3(0, 0, 1);
		float3 faceNormal = cross(perpendicularAngle, IN[0].norm) * lightPosition;

		float3 worldPos = TransformObjectToWorld(IN[0].pos.xyz);

		// camera distance for culling 
		float distanceFromCamera = distance(worldPos, _WorldSpaceCameraPos);
		float distanceFade = 1 - saturate((distanceFromCamera - _MinDist) / _MaxDist);

		float3 v0 = IN[0].pos.xyz ;

		float3 wind1 = float3(
			sin(_Time.x * _WindSpeed + v0.x) + sin(_Time.x * _WindSpeed + v0.z * 2) + sin(
				_Time.x * _WindSpeed * 0.1 + v0.x), 0,
			cos(_Time.x * _WindSpeed + v0.x * 2) + cos(_Time.x * _WindSpeed + v0.z));

		wind1 *= _WindStrength;


		// Interactivity
		float3 dis = distance(_PositionMoving, worldPos); // distance for radius
		float3 radius = 1 - saturate(dis / _Radius); // in world radius based on objects interaction radius
		float3 sphereDisp = worldPos - _PositionMoving; // position comparison
		sphereDisp *= radius; // position multiplied by radius for falloff
		// increase strength
		sphereDisp = clamp(sphereDisp.xyz * _Strength, -0.8, 0.8);

		// set vertex color
		float3 color = IN[0].color;
		// set grass height
		_GrassHeight *= IN[0].uv.y;
		_GrassWidth *= IN[0].uv.x;
		_GrassHeight *= clamp(rand(IN[0].pos.xyz), 1 - _RandomHeight, 1 + _RandomHeight);

		// grassblades geometry
		for (int j = 0; j < (GrassBlades * distanceFade); j++)
		{
			// set rotation and radius of the blades
			float3x3 facingRotationMatrix = AngleAxis3x3(rand(IN[0].pos.xyz) * TWO_PI + j, float3(0, 1, -0.1));
			float3x3 transformationMatrix = facingRotationMatrix;
			float radius = j / (float)GrassBlades;
			float offset = (1 - radius) * _Rad;
			for (int i = 0; i < GrassSegments; i++)
			{
				// taper width, increase height;
				float t = i / (float)GrassSegments;
				float segmentHeight = _GrassHeight * t;
				float segmentWidth = _GrassWidth * (1 - t);

				// the first (0) grass segment is thinner
				segmentWidth = i == 0 ? _GrassWidth * 0.3 : segmentWidth;

				float segmentForward = PositivePow(t, _BladeCurve) * forward;

				// Add below the line declaring float segmentWidth.
				float3x3 transformMatrix = i == 0 ? facingRotationMatrix : transformationMatrix;

				// first grass (0) segment does not get displaced by interactivity
				float3 newPos = i == 0 ? v0 : v0 + ((float3(sphereDisp.x, sphereDisp.y, sphereDisp.z) + wind1) * t);
				
				// every segment adds 2 new triangles
				triStream.Append(GrassVertex(newPos, segmentWidth, segmentHeight, offset, segmentForward, float2(0, t),
				                             transformMatrix, faceNormal, color, worldPos));
				triStream.Append(GrassVertex(newPos, -segmentWidth, segmentHeight, offset, segmentForward, float2(1, t),
				                             transformMatrix, faceNormal, color, worldPos));
			}
			// Add just below the loop to insert the vertex at the tip of the blade.
			triStream.Append(GrassVertex(v0 + float3(sphereDisp.x * 1.5, sphereDisp.y, sphereDisp.z * 1.5) + wind1, 0,
			                             _GrassHeight, offset, forward, float2(0.5, 1), transformationMatrix,
			                             faceNormal, color, worldPos));
			// restart the strip to start another grass blade
			triStream.RestartStrip();
		}
	}
	ENDHLSL

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline"
		}

		Pass
		{
			HLSLPROGRAM



			// The fragment shader definition.            
			half4 frag(g2f i) : SV_Target
			{
				float shadow = 0;
				
				#if SHADOWS_SCREEN
					// Defining the color variable and returning it.
					half4 shadowCoord = ComputeScreenPos(i.pos);
				#else
					half4 shadowCoord = TransformWorldToShadowCoord(i.shadows);
				#endif
				
				Light mainLight = GetMainLight(shadowCoord);
				
				#ifdef _MAIN_LIGHT_SHADOWS
				shadow = mainLight.shadowAttenuation;
				#endif
				
				float4 baseColor = lerp(_BottomColor, _TopColor, saturate(i.uv.y)) * float4(i.diffuseColor, 1);

				// multiply with lighting color
				float4 litColor = (baseColor * float4(mainLight.color, 1));
				// multiply with vertex color, and shadows
				float4 final = litColor * shadow;
				// add in basecolor when lights turned down
				final += saturate((1 - shadow) * baseColor * 0.2);
				// fog
				float fogFactor = i.fogFactor;

				// Mix the pixel color with fogColor. 
				final.rgb = MixFog(final.rgb, fogFactor);
				// add in ambient color
				final += (unity_AmbientSky * _AmbientStrength);
				return final;
			}
			ENDHLSL
		}
		
		// shadow casting pass with empty fragment
		Pass
		{
			Name "ShadowCaster"
			Tags
			{
				"LightMode" = "ShadowCaster"
			}

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			#define SHADERPASS_SHADOWCASTER

			#pragma shader_feature_local _ DISTANCE_DETAIL

			half4 frag(g2f input) : SV_TARGET
			{
				return 0;
			}
			ENDHLSL
		}
	}
}