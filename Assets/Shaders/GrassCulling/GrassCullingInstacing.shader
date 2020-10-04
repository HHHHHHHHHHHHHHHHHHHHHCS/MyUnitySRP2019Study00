Shader "MyRP/GrassCulling/GrassCullingInstancing"
{
	Properties
	{
		[MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
		_GroundColor ("Ground Color", Color) = (0.5, 0.5, 0.5)
		
		[Header(Grass Shape)]
		_GrassWidth ("Grass Width", Float) = 1
		_GrassHeight ("Grass Height", FLoat) = 1
		
		[Header(Wind)]
		_WindAIntensity ("Wind A Intensity", Float) = 1.77
		_WindAFrequency ("Wind A Frequency", Float) = 4
		_WindATiling ("Wind A Tiling", Vector) = (0.1, 0.1, 0)
		_WindAWrap ("Wind A Wrap", Vector) = (0.5, 0.5, 0)
		
		_WindBIntensity ("Wind B Intensity", Float) = 0.25
		_WindBFrequency ("Wind B Frequency", Float) = 7.7
		_WindBTiling ("Wind B Tiling", Vector) = (0.37, 3, 0)
		_WindBWrap ("Wind B Wrap", Vector) = (0.5, 0.5, 0)
		
		_WindCIntensity ("Wind C Intensity", Float) = 0.125
		_WindCFrequency ("Wind C Frequency", Float) = 11.7
		_WindCTiling ("Wind C Tiling", Vector) = (0.77, 3, 0)
		_WindCWrap ("Wind C Wrap", Vector) = (0.5, 0.5, 0)
		
		[Header(Lighting)]
		_RandomNormal ("Random Normal", Float) = 0.15
		
		//make SRP batcher happy
		[HideInInspector]_PivotPosWS ("_PivotPosWS", Vector) = (0, 0, 0, 0)
		[HideInInspector]_BoundSize ("_BoundSize", Vector) = (1, 1, 0)
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }
		
		Pass
		{
			Cull Back //使用默认的culling 因为这个shader 是 面片
			ZTest Less
			Tags { "LightMode" = "UniversalForward" }
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			//URP keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile _ _SHADOWS_SOFT
			// Unity defined keywords
			#pragma multi_compile_fog
			
			
			ENDHLSL
			
		}
	}
}
