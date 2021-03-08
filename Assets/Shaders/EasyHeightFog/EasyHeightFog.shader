﻿Shader "EasyHeightFog/EasyHeightFog"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" { }
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		
		Pass
		{
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			#include "FogCommon.cginc"
			
			struct appdata
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			struct v2f
			{
				float4 vertex: SV_POSITION;
				float2 uv: TEXCOORD0;
				My_FOG_COORDS(1)
			};
			
			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				My_TRANSFER_FOG(o, o.vertex);
				return o;
			}
			
			half4 frag(v2f i): SV_Target
			{
				half4 col = tex2D(_MainTex, i.uv);
				My_APPLY_FOG(i, col);
				return col;
			}
			ENDCG
			
		}
	}
}
