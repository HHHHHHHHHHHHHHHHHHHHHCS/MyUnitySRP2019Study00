//copyby github.com/ColinLeung-NiloCat/UnityURPUnlitScreenSpaceDecalShader
Shader "MyRP/Other/ScreenSpaceDecal"
{
	Properties
	{
		[Header(Basic)]
		_MainTex ("Texture", 2D) = "white" { }
		[HDR]_Color ("_Color (default = 1,1,1,1)", color) = (1, 1, 1, 1)
		
		[Header(Blending)]
		//https://docs.unity3d.com/ScriptReference/Rendering.BlendMode.html
		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend ("_SrcBlend (default = SrcAlpha)", Float) = 5 //5 = SrcAlpha
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend ("_DstBlend (default = OneMinusSrcAlpha)", Float) = 10 //10 = OneMinusSrcAlpha
		
		[Header(Alpha remap(extra alpha control))]
		_AlphaRemap ("_AlphaRemap (default = 1,0,0,0) _____alpha will first mul x, then add y    (zw unused)", vector) = (1, 0, 0, 0)
		
		[Header(Prevent Side Stretching(Compare projection direction with scene normal and Discard if needed))]
		[Toggle(_ProjectionAngleDiscardEnable)] _ProjectionAngleDiscardEnable ("_ProjectionAngleDiscardEnable (default = off)", float) = 0
		_ProjectionAngleDiscardThreshold ("_ProjectionAngleDiscardThreshold (default = 0)", range(-1, 1)) = 0
		
		[Header(Mul alpha to rgb)]
		[Toggle]_MulAlphaToRGB ("_MulAlphaToRGB (default = off)", Float) = 0
		
		[Header(Ignore texture wrap mode setting)]
		[Toggle(_FracUVEnable)] _FracUVEnable ("_FracUVEnable (default = off)", Float) = 0
		
		//====================================== below = usually can ignore in normal use case =====================================================================
		[Header(Stencil Masking)]
		//https://docs.unity3d.com/ScriptReference/Rendering.CompareFunction.html
		_StencilRef ("_StencilRef", Float) = 0
		[Enum(UnityEngine.Rendering.CompareFunction)]_StencilComp ("_StencilComp (default = Disable) _____Set to NotEqual if you want to mask by specific _StencilRef value, else set to Disable", Float) = 0 //0 = disable
		
		[Header(ZTest)]
		//https://docs.unity3d.com/ScriptReference/Rendering.CompareFunction.html
		//default need to be Disable, because we need to make sure decal render correctly even if camera goes into decal cube volume, although disable ZTest by default will prevent EarlyZ (bad for GPU performance)
		[Enum(UnityEngine.Rendering.CompareFunction)]_ZTest ("_ZTest (default = Disable) _____to improve GPU performance, Set to LessEqual if camera never goes into cube volume, else set to Disable", Float) = 0 //0 = disable
		
		[Header(Cull)]
		//https://docs.unity3d.com/ScriptReference/Rendering.CullMode.html
		//default need to be Front, because we need to make sure decal render correctly even if camera goes into decal cube
		[Enum(UnityEngine.Rendering.CullMode)]_Cull ("_Cull (default = Front) _____to improve GPU performance, Set to Back if camera never goes into cube volume, else set to Front", Float) = 1 //1 = Front
		
		[Header(Unity Fog)]
		[Toggle(_UnityFogEnable)] _UnityFogEnable ("_UnityFogEnable (default = on)", Float) = 1
	}
	SubShader
	{
		//需要在opaque 之后  又要再透明之前  而且合批会出错 所以不能合批
		Tags { "RenderType" = "Overlay" "Queue" = "Transparent-499" "DisableBatching" = "True" }
		
		Pass
		{
			Stencil
			{
				Ref [_StencilRef]
				Comp [_StencilComp]
			}
			
			Cull [_Cull]
			ZTest [_ZTest]
			
			ZWrite Off
			Blend[_SrcBlend][_DstBlend]
			
			HLSLPROGRAM
			
			//ddx & ddy 需要
			#pragma target 3.0
			
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile_fog
			
			//shader_feature_local 可能会在打包剔除  local 为了突破全部shader关键字255限制
			//https://docs.unity3d.com/Manual/SL-MultipleProgramVariants.html
			#pragma shader_feature_local _ProjectionAngleDiscardEnable
			#pragma shader_feature_local _UnityFogEnable
			#pragma shader_feature_local _FracUVEnable
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			struct a2v
			{
				float4 vertex: POSITION;
			};
			
			struct v2f
			{
				float4 vertex: SV_POSITION;
				float4 screenUV: TEXCOORD0;
				float4 viewRayOS: TEXCOORD1;
				float4 cameraPosOSAndFogFactor: TEXCOORD2;
			};
			
			sampler2D _MainTex;
			CBUFFER_START(UnityPerMaterial)
			float4 _MainTex_ST;
			float _ProjectionAngleDiscardThreshold;
			half4 _Color;
			half2 _AlphaRemap;
			half _MulAlphaToRGB;
			CBUFFER_END
			
			sampler2D _CameraDepthTexture;
			
			v2f vert(a2v v)
			{
				v2f o;
				
				o.vertex = TransformObjectToHClip(v.vertex.xyz);
				
				#if _UnityFogEnable
					o.cameraPosOSAndFogFactor.a = ComputeFogFactor(o.vertex.z);
				#else
					o.cameraPosOSAndFogFactor.a = 0;
				#endif
				
				o.screenUV = ComputeScreenPos(o.vertex);
				
				float3 viewRay = TransformWorldToView(TransformObjectToWorld(v.vertex.xyz));
				
				//齐次坐标除法 必须在frag中执行  而不能是vertex阶段 (因为光栅化校正会错误)
				//o.w 即 near far 深度
				o.viewRayOS.w = viewRay.z;
				
				viewRay *= -1;// unity camera 左右手坐标互换  主要是反转z
				
				//matrix view->object
				//矩阵计算非常的昂贵   但是 但是因为是Cube  所以计算量非常少
				float4x4 viewToObjectMatrix = mul(unity_WorldToObject, UNITY_MATRIX_I_V);
				
				//这样可以跳过在frag 矩阵计算 节省性能
				o.viewRayOS.xyz = mul((float3x3)viewToObjectMatrix, viewRay);
				//object  空间的 摄像机位置
				o.cameraPosOSAndFogFactor.xyz = mul(viewToObjectMatrix, float4(0, 0, 0, 1));
				
				return o;
			}
			
			half4 frag(v2f i): SV_TARGET
			{
				i.viewRayOS /= i.viewRayOS.w;
				
				//[0,1] -> [near,far]
				float sceneCameraSpaceDepth = LinearEyeDepth(tex2Dproj(_CameraDepthTexture, i.screenUV).r, _ZBufferParams);
				
				//scene depth in any space = rayStartPos + rayDir * rayLength
				float3 decalSpaceScenePos = i.cameraPosOSAndFogFactor.xyz + i.viewRayOS.xyz * sceneCameraSpaceDepth;
				//[-0.5,0.5] -> [0,1]
				float2 decalSpaceUV = decalSpaceScenePos.xy + 0.5;
				
				//TODO:
				return 0;
			}
			
			ENDHLSL
			
		}
	}
}
