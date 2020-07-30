Shader "MyRP/SkyBox/SkyBox"
{
	Properties
	{
		_ColorTop ("_ColorTop", Color) = (1, 1, 1)
		_ColorMiddle ("_ColorMiddle", Color) = (0, 0, 0)
		_FogHeight ("_FogHeight", range(0, 1)) = 0.1
	}
	SubShader
	{
		//如果RenderPipeline标记未设置，则它将匹配任何渲染管道
		Tags { "RenderType" = "Background" "Queue"="Background" "PreviewType" = "Skybox" /*"RenderPipeline" = "UniversalRenderPipeline"*/}
		
		Pass
		{
			Name "Skybox"
			
			ZWrite Off
			
			HLSLPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			
			CBUFFER_START(UnityPerMaterial)
			half3 _ColorTop;
			half3 _ColorMiddle;
			half _FogHeight;
			CBUFFER_END
			
			struct a2v
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			struct v2f
			{
				float2 uv: TEXCOORD0;
				float3 color: COLOR;
				float4 vertex: SV_POSITION;
			};
			
			v2f vert(a2v v)
			{
				v2f o;
				o.vertex = TransformObjectToHClip(v.vertex.xyz);
				o.uv = v.uv;
				
				float height = v.uv.y;//top = 1 , middle = 0 ,bottom = -1
				height = saturate(height);//bottom = 0 toooo
				half3 col = lerp(_ColorMiddle, _ColorTop, height);
				
				col = lerp(unity_FogColor.rgb, col, smoothstep(0, _FogHeight, height));
				
				o.color = col;
				
				return o;
			}
			
			half4 frag(v2f i): SV_TARGET
			{
				return half4(i.color, 1);
			}
			
			ENDHLSL
			
		}
	}
}
