Shader "MyRP/GrassCulling/GrassCullingTrail"
{
	Properties { }
	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent" /*"RenderPipeline" = "UniversalRenderPipeline"*/ }
		
		Pass
		{
			Cull Off
			ZTest Always
			ZWrite Off
			Blend Zero SrcColor //这里会越叠加颜色越淡
			
			Tags { "LightMode" = "GrassBending" }
			
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			
			struct a2v
			{
				float4 vertex: POSITION;
				half4 color: coLOR;
			};
			
			struct v2f
			{
				float4 vertex: SV_POSITION;
				half4 color: COLOR;
			};
			
			v2f vert(a2v v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				return o;
			}
			
			float4 frag(v2f i): SV_TARGET
			{
				return i.color;
			}
			
			
			ENDCG
			
		}
	}
}
