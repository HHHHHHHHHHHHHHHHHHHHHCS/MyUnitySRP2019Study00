#ifndef __HIZ_INSTANCE_INCLUDE__
	#define __HIZ_INSTANCE_INCLUDE__

	#include "ShaderInclude_IndirectStructs.cginc"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

	struct a2v_depth
	{
		float4 vertex: POSITION;
		//UNITY_VERTEX_INPUT_INSTANCE_ID
	};
	
	struct v2f_depth
	{
		float4 pos: SV_POSITION;
	};
	

	
	uint _ArgsOffset;
	StructuredBuffer<uint> _ArgsBuffer;
	StructuredBuffer<Indirect2x2Matrix> _InstancesDrawMatrixRows01;
	StructuredBuffer<Indirect2x2Matrix> _InstancesDrawMatrixRows23;
	StructuredBuffer<Indirect2x2Matrix> _InstancesDrawMatrixRows45;


	
	uint SetupMatrix(uint instanceID)
	{
		uint index = 0;
		#if defined(SHADER_API_METAL)
			index = instanceID;
		#else
			index = instanceID + _ArgsBuffer[_ArgsOffset];
		#endif

		
		Indirect2x2Matrix rows01 = _InstancesDrawMatrixRows01[index];
		Indirect2x2Matrix rows23 = _InstancesDrawMatrixRows23[index];
		Indirect2x2Matrix rows45 = _InstancesDrawMatrixRows45[index];
		
		unity_ObjectToWorld = float4x4(rows01.row0, rows01.row1, rows23.row0, float4(0, 0, 0, 1));
		unity_WorldToObject = float4x4(rows23.row1, rows45.row0, rows45.row1, float4(0, 0, 0, 1));
		return index;
	}
	
	
	v2f_depth vert_depth(a2v_depth input, uint instanceID: SV_INSTANCEID)
	{
		//UNITY_SETUP_INSTANCE_ID(input);
		SetupMatrix(instanceID);
		v2f_depth o;
		o.pos = mul(UNITY_MATRIX_VP, mul(UNITY_MATRIX_M, input.vertex));
		return o;
	}
	
	float frag_depth(v2f_depth i): SV_TARGET
	{
		return i.pos.z;
	}

#endif
