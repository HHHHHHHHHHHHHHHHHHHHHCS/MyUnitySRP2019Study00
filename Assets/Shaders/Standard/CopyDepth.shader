Shader "MyRP/CopyDepth"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" { }
	}
	SubShader
	{
		LOD 100
		
		Tags { "RenderType" = "Opaque" }
		
		Cull Off ZWrite On ZTest Always
		
		Pass
		{
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			
			struct appdata
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			struct v2f
			{
				float2 uv: TEXCOORD0;
				float4 vertex: SV_POSITION;
			};
			
			sampler2D _MainTex;
			
			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			float4 frag(v2f i, out float depth: SV_DEPTH): SV_Target
			{
				float4 col = SAMPLE_DEPTH_TEXTURE(_MainTex, i.uv);
				depth = col.x;
				return col;
			}
			ENDCG
			
		}
	}
}
