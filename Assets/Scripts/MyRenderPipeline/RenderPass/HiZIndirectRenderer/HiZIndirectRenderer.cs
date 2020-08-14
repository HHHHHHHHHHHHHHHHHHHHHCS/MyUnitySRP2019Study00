using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer
{
	//StructLayout(LayoutKind.Sequential)  按成员顺序 在内存依次排序
	// Preferrably want to have all buffer structs in power of 2...
	// 6 * 4 bytes = 24 bytes
	[System.Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct IndirectInstanceCSInput
	{
		public Vector3 boundsCenter; // 3
		public Vector3 boundsExtents; // 6
	}

	// 8 * 4 bytes = 32 bytes
	[StructLayout(LayoutKind.Sequential)]
	public struct Indirect2x2Matrix
	{
		public Vector4 row0; // 4
		public Vector4 row1; // 8
	};

	// 2 * 4 bytes = 8 bytes
	[StructLayout(LayoutKind.Sequential)]
	public struct SortingData
	{
		public uint drawCallInstanceIndex; // 1
		public float distanceToCam; // 2
	};

	[System.Serializable]
	public class IndirectRenderingMesh
	{
		public Mesh mesh;
		public Material material;
		public MaterialPropertyBlock lod00MatPropBlock;
		public MaterialPropertyBlock lod01MatPropBlock;
		public MaterialPropertyBlock lod02MatPropBlock;
		public MaterialPropertyBlock shadowLod00MatPropBlock;
		public MaterialPropertyBlock shadowLod01MatPropBlock;
		public MaterialPropertyBlock shadowLod02MatPropBlock;
		public uint numOfVerticesLod00;
		public uint numOfVerticesLod01;
		public uint numOfVerticesLod02;
		public uint numOfIndicesLod00;
		public uint numOfIndicesLod01;
		public uint numOfIndicesLod02;
	}

	public class HiZIndirectRenderer : MonoBehaviour
	{
		#region Variables

		[Header("Settings")] public bool runCompute = true;
		public bool drawInstances = true;
		public bool drawInstanceShadows = true;
		public bool enableFrustumCulling = true;
		public bool enableOcclusionCulling = true;
		public bool enableDetailCulling = true;
		public bool enableLOD = true;
		public bool enableOnlyLOD02Shadows = true;
		[Range(00.00f, 00.02f)] public float detailCullingPercentage = 0.005f;

		// Debugging Variables
		[Header("Debug")] public bool debugShowUI;
		public bool debugDrawLOD;
		public bool debugDrawBoundsInSceneView;
		public bool debugDrawHiZ;
		[Range(0, 10)] public int debugHiZLOD;
		public GameObject debugUIPrefab;

		[Header("Data")] [ReadOnly]
		public List<IndirectInstanceCSInput> instancesInputData = new List<IndirectInstanceCSInput>();

		[ReadOnly] public IndirectRenderingMesh[] indirectMeshes;

		[Header("Logging")] public bool logInstanceDrawMatrices = false;
		public bool logArgumentsAfterReset = false;
		public bool logSortingData = false;
		public bool logArgumentsAfterOcclusion = false;
		public bool logInstancesIsVisibleBuffer = false;
		public bool logScannedPredicates = false;
		public bool logGroupSumArrayBuffer = false;
		public bool logScannedGroupSumsBuffer = false;
		public bool logArgsBufferAfterCopy = false;
		public bool logCulledInstancesDrawMatrices = false;

		[Header("References")] public ComputeShader createDrawDataBufferCS;
		public ComputeShader sortingCS;
		public ComputeShader occlusionCS;
		public ComputeShader scanInstancesCS;
		public ComputeShader scanGroupSumsCS;
		public ComputeShader copyInstanceDataCS;
		//TODO:
		//public HiZBuffer hiZBuffer;
		public Camera mainCamera;
		public Camera debugCamera;

		// Compute Buffers
		private ComputeBuffer m_instancesIsVisibleBuffer;
		private ComputeBuffer m_instancesGroupSumArrayBuffer;
		private ComputeBuffer m_instancesScannedGroupSumBuffer;
		private ComputeBuffer m_instancesScannedPredicates;
		private ComputeBuffer m_instanceDataBuffer;
		private ComputeBuffer m_instancesSortingData;
		private ComputeBuffer m_instancesSortingDataTemp;
		private ComputeBuffer m_instancesMatrixRows01;
		private ComputeBuffer m_instancesMatrixRows23;
		private ComputeBuffer m_instancesMatrixRows45;
		private ComputeBuffer m_instancesCulledMatrixRows01;
		private ComputeBuffer m_instancesCulledMatrixRows23;
		private ComputeBuffer m_instancesCulledMatrixRows45;
		private ComputeBuffer m_instancesArgsBuffer;
		private ComputeBuffer m_shadowArgsBuffer;
		private ComputeBuffer m_shadowsIsVisibleBuffer;
		private ComputeBuffer m_shadowGroupSumArrayBuffer;
		private ComputeBuffer m_shadowsScannedGroupSumBuffer;
		private ComputeBuffer m_shadowScannedInstancePredicates;
		private ComputeBuffer m_shadowCulledMatrixRows01;
		private ComputeBuffer m_shadowCulledMatrixRows23;
		private ComputeBuffer m_shadowCulledMatrixRows45;

		// Command Buffers
		private CommandBuffer m_sortingCommandBuffer;

		// Kernel ID's
		private int m_createDrawDataBufferKernelID;
		private int m_sortingCSKernelID;
		private int m_sortingTransposeKernelID;
		private int m_occlusionKernelID;
		private int m_scanInstancesKernelID;
		private int m_scanGroupSumsKernelID;
		private int m_copyInstanceDataKernelID;
		private bool m_isInitialized;

		// Other
		private int m_numberOfInstanceTypes;
		private int m_numberOfInstances;
		private int m_occlusionGroupX;
		private int m_scanInstancesGroupX;
		private int m_scanThreadGroupsGroupX;
		private int m_copyInstanceDataGroupX;
		private bool m_debugLastDrawLOD = false;
		private bool m_isEnabled;
		private uint[] m_args;
		private Bounds m_bounds;
		private Vector3 m_camPosition = Vector3.zero;
		private Vector3 m_lastCamPosition = Vector3.zero;
		private Matrix4x4 m_MVP;

		// Debug
		private AsyncGPUReadbackRequest m_debugGPUArgsRequest;
		private AsyncGPUReadbackRequest m_debugGPUShadowArgsRequest;
		private StringBuilder m_debugUIText = new StringBuilder(1000);
		private Text m_uiText;
		private GameObject m_uiObj;

		// Constants
		private const int NUMBER_OF_DRAW_CALLS = 3; // (LOD00 + LOD01 + LOD02)

		private const int
			NUMBER_OF_ARGS_PER_DRAW = 5; // (indexCount, instanceCount, startIndex, baseVertex, startInstance)

		private const int
			NUMBER_OF_ARGS_PER_INSTANCE_TYPE =
				NUMBER_OF_DRAW_CALLS * NUMBER_OF_ARGS_PER_DRAW; // 3draws * 5args = 15args

		private const int
			ARGS_BYTE_SIZE_PER_DRAW_CALL = NUMBER_OF_ARGS_PER_DRAW * sizeof(uint); // 5args * 4bytes = 20 bytes

		private const int
			ARGS_BYTE_SIZE_PER_INSTANCE_TYPE =
				NUMBER_OF_ARGS_PER_INSTANCE_TYPE * sizeof(uint); // 15args * 4bytes = 60bytes

		private const int SCAN_THREAD_GROUP_SIZE = 64;
		private const string DEBUG_UI_RED_COLOR = "<color=#ff6666>";
		private const string DEBUG_UI_WHITE_COLOR = "<color=#ffffff>";
		private const string DEBUG_SHADER_LOD_KEYWORD = "INDIRECT_DEBUG_LOD";

		// Shader Property ID's
		private static readonly int _Data = Shader.PropertyToID("_Data");
		private static readonly int _Input = Shader.PropertyToID("_Input");
		private static readonly int _ShouldFrustumCull = Shader.PropertyToID("_ShouldFrustumCull");
		private static readonly int _ShouldOcclusionCull = Shader.PropertyToID("_ShouldOcclusionCull");
		private static readonly int _ShouldLOD = Shader.PropertyToID("_ShouldLOD");
		private static readonly int _ShouldDetailCull = Shader.PropertyToID("_ShouldDetailCull");
		private static readonly int _ShouldOnlyUseLOD02Shadows = Shader.PropertyToID("_ShouldOnlyUseLOD02Shadows");
		private static readonly int _UNITY_MATRIX_MVP = Shader.PropertyToID("_UNITY_MATRIX_MVP");
		private static readonly int _CamPosition = Shader.PropertyToID("_CamPosition");
		private static readonly int _HiZTextureSize = Shader.PropertyToID("_HiZTextureSize");
		private static readonly int _Level = Shader.PropertyToID("_Level");
		private static readonly int _LevelMask = Shader.PropertyToID("_LevelMask");
		private static readonly int _Width = Shader.PropertyToID("_Width");
		private static readonly int _Height = Shader.PropertyToID("_Height");
		private static readonly int _ShadowDistance = Shader.PropertyToID("_ShadowDistance");

		private static readonly int _DetailCullingScreenPercentage =
			Shader.PropertyToID("_DetailCullingScreenPercentage");

		private static readonly int _HiZMap = Shader.PropertyToID("_HiZMap");
		private static readonly int _NumOfGroups = Shader.PropertyToID("_NumOfGroups");
		private static readonly int _NumOfDrawcalls = Shader.PropertyToID("_NumOfDrawcalls");
		private static readonly int _ArgsOffset = Shader.PropertyToID("_ArgsOffset");
		private static readonly int _Positions = Shader.PropertyToID("_Positions");
		private static readonly int _Scales = Shader.PropertyToID("_Scales");
		private static readonly int _Rotations = Shader.PropertyToID("_Rotations");
		private static readonly int _ArgsBuffer = Shader.PropertyToID("_ArgsBuffer");
		private static readonly int _ShadowArgsBuffer = Shader.PropertyToID("_ShadowArgsBuffer");
		private static readonly int _IsVisibleBuffer = Shader.PropertyToID("_IsVisibleBuffer");
		private static readonly int _ShadowIsVisibleBuffer = Shader.PropertyToID("_ShadowIsVisibleBuffer");
		private static readonly int _GroupSumArray = Shader.PropertyToID("_GroupSumArray");
		private static readonly int _ScannedInstancePredicates = Shader.PropertyToID("_ScannedInstancePredicates");
		private static readonly int _GroupSumArrayIn = Shader.PropertyToID("_GroupSumArrayIn");
		private static readonly int _GroupSumArrayOut = Shader.PropertyToID("_GroupSumArrayOut");
		private static readonly int _DrawcallDataOut = Shader.PropertyToID("_DrawcallDataOut");
		private static readonly int _SortingData = Shader.PropertyToID("_SortingData");
		private static readonly int _InstanceDataBuffer = Shader.PropertyToID("_InstanceDataBuffer");
		private static readonly int _InstancePredicatesIn = Shader.PropertyToID("_InstancePredicatesIn");
		private static readonly int _InstancesDrawMatrixRows01 = Shader.PropertyToID("_InstancesDrawMatrixRows01");
		private static readonly int _InstancesDrawMatrixRows23 = Shader.PropertyToID("_InstancesDrawMatrixRows23");
		private static readonly int _InstancesDrawMatrixRows45 = Shader.PropertyToID("_InstancesDrawMatrixRows45");
		private static readonly int _InstancesCulledMatrixRows01 = Shader.PropertyToID("_InstancesCulledMatrixRows01");
		private static readonly int _InstancesCulledMatrixRows23 = Shader.PropertyToID("_InstancesCulledMatrixRows23");
		private static readonly int _InstancesCulledMatrixRows45 = Shader.PropertyToID("_InstancesCulledMatrixRows45");

		#endregion


		#region MonoBehaviour

		private void Update()
		{
			if (m_isEnabled)
			{
				UpdateDebug();
			}
		}

		#endregion


		#region Debug & Logging

		private void UpdateDebug()
		{
			if (!Application.isPlaying)
			{
				return;
			}

			//occlusionCS.SetInt();
		}

		#endregion
	}
}