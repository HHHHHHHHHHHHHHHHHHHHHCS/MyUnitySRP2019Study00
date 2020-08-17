using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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

		public HiZBuffer hiZBuffer;
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
		private static readonly int _ShouldFrustumCull_ID = Shader.PropertyToID("_ShouldFrustumCull");
		private static readonly int _ShouldOcclusionCull_ID = Shader.PropertyToID("_ShouldOcclusionCull");
		private static readonly int _ShouldLOD_ID = Shader.PropertyToID("_ShouldLOD");
		private static readonly int _ShouldDetailCull_ID = Shader.PropertyToID("_ShouldDetailCull");
		private static readonly int _ShouldOnlyUseLOD02Shadows_ID = Shader.PropertyToID("_ShouldOnlyUseLOD02Shadows");
		private static readonly int _UNITY_MATRIX_MVP_ID = Shader.PropertyToID("_UNITY_MATRIX_MVP");
		private static readonly int _CamPosition_ID = Shader.PropertyToID("_CamPosition");
		private static readonly int _HiZTextureSize = Shader.PropertyToID("_HiZTextureSize");
		private static readonly int _Level = Shader.PropertyToID("_Level");
		private static readonly int _LevelMask = Shader.PropertyToID("_LevelMask");
		private static readonly int _Width = Shader.PropertyToID("_Width");
		private static readonly int _Height = Shader.PropertyToID("_Height");
		private static readonly int _ShadowDistance_ID = Shader.PropertyToID("_ShadowDistance");

		private static readonly int _DetailCullingScreenPercentage_ID =
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
		private static readonly int _GroupSumArray_ID = Shader.PropertyToID("_GroupSumArray");
		private static readonly int _ScannedInstancePredicates_ID = Shader.PropertyToID("_ScannedInstancePredicates");
		private static readonly int _GroupSumArrayIn_ID = Shader.PropertyToID("_GroupSumArrayIn");
		private static readonly int _GroupSumArrayOut_ID = Shader.PropertyToID("_GroupSumArrayOut");
		private static readonly int _DrawcallDataOut_ID = Shader.PropertyToID("_DrawcallDataOut");
		private static readonly int _SortingData = Shader.PropertyToID("_SortingData");
		private static readonly int _InstanceDataBuffer = Shader.PropertyToID("_InstanceDataBuffer");
		private static readonly int _InstancePredicatesIn_ID = Shader.PropertyToID("_InstancePredicatesIn");
		private static readonly int _InstancesDrawMatrixRows01 = Shader.PropertyToID("_InstancesDrawMatrixRows01");
		private static readonly int _InstancesDrawMatrixRows23 = Shader.PropertyToID("_InstancesDrawMatrixRows23");
		private static readonly int _InstancesDrawMatrixRows45 = Shader.PropertyToID("_InstancesDrawMatrixRows45");

		private static readonly int _InstancesCulledMatrixRows01_ID =
			Shader.PropertyToID("_InstancesCulledMatrixRows01");

		private static readonly int _InstancesCulledMatrixRows23_ID =
			Shader.PropertyToID("_InstancesCulledMatrixRows23");

		private static readonly int _InstancesCulledMatrixRows45_ID =
			Shader.PropertyToID("_InstancesCulledMatrixRows45");

		#endregion


		#region MonoBehaviour

		private void Update()
		{
			if (m_isEnabled)
			{
				UpdateDebug();
			}
		}

		private void OnPreCull()
		{
			if (!m_isEnabled
			    || indirectMeshes == null
			    || indirectMeshes.Length == 0
			    || hiZBuffer.Texture == null)
			{
				return;
			}

			UpdateDebug();

			if (runCompute)
			{
				Profiler.BeginSample("CalculateVisibleInstances()");
				CalculateVisibleInstances();
				Profiler.EndSample();
			}
			
			//TODO:
		}

		#endregion


		#region Private Functions

		private void CalculateVisibleInstances()
		{
			//global data
			m_camPosition = mainCamera.transform.position;
			m_bounds.center = m_camPosition;

			//Matrix4x4 m = mainCamera.transform.localToWorldMatrix;
			Matrix4x4 v = mainCamera.worldToCameraMatrix;
			Matrix4x4 p = mainCamera.projectionMatrix;
			m_MVP = p * v; //*m;

			if (logInstanceDrawMatrices)
			{
				logInstanceDrawMatrices = false;
				LogInstanceDrawMatrices("LogInstanceDrawMatrices()");
			}

			Profiler.BeginSample("Resetting args buffer");
			{
				m_instancesArgsBuffer.SetData(m_args);
				m_shadowArgsBuffer.SetData(m_args);

				if (logArgumentsAfterReset)
				{
					logArgumentsAfterReset = false;
					LogArgsBuffers("LogArgsBuffers() - Instances After Reset",
						"LogArgsBuffers() - Shadows After Reset");
				}
			}
			Profiler.EndSample();

			Profiler.BeginSample("02 Occlusion");
			{
				occlusionCS.SetFloat(_ShadowDistance_ID, QualitySettings.shadowDistance);
				occlusionCS.SetMatrix(_UNITY_MATRIX_MVP_ID, m_MVP);
				occlusionCS.SetVector(_CamPosition_ID, m_camPosition);

				occlusionCS.Dispatch(m_occlusionKernelID, m_occlusionGroupX, 1, 1);

				if (logArgumentsAfterOcclusion)
				{
					logArgumentsAfterOcclusion = false;
					LogArgsBuffers("LogArgsBuffers() - Instances After Occlusion",
						"LogArgsBuffers() - Shadows After Occlusion");
				}

				if (logInstancesIsVisibleBuffer)
				{
					logInstancesIsVisibleBuffer = false;
					LogInstancesIsVisibleBuffers("LogInstancesIsVisibleBuffers() - Instances",
						"LogInstancesIsVisibleBuffers() - Shadows");
				}
			}
			Profiler.EndSample();

			Profiler.BeginSample("03 Scan Instances");
			{
				//Normal
				scanInstancesCS.SetBuffer(m_scanInstancesKernelID, _InstancePredicatesIn_ID,
					m_instancesIsVisibleBuffer);
				scanInstancesCS.SetBuffer(m_scanInstancesKernelID, _GroupSumArray_ID,
					m_instancesGroupSumArrayBuffer);
				scanInstancesCS.SetBuffer(m_scanInstancesKernelID, _ScannedInstancePredicates_ID,
					m_instancesScannedPredicates);
				scanInstancesCS.Dispatch(m_scanInstancesKernelID, m_scanInstancesGroupX, 1, 1);

				//Shadows
				scanInstancesCS.SetBuffer(m_scanInstancesKernelID, _InstancePredicatesIn_ID,
					m_shadowsIsVisibleBuffer);
				scanInstancesCS.SetBuffer(m_scanInstancesKernelID, _GroupSumArray_ID,
					m_shadowGroupSumArrayBuffer);
				scanInstancesCS.SetBuffer(m_scanInstancesKernelID, _ScannedInstancePredicates_ID,
					m_shadowScannedInstancePredicates);
				scanInstancesCS.Dispatch(m_scanInstancesKernelID, m_scanInstancesGroupX, 1, 1);

				if (logGroupSumArrayBuffer)
				{
					logGroupSumArrayBuffer = false;
					LogGroupSumArrayBuffer("LogGroupSumArrayBuffer() - Instances",
						"LogGroupSumArrayBuffer() - Shadows");
				}

				if (logScannedPredicates)
				{
					logScannedPredicates = false;
					LogScannedPredicates("LogScannedPredicates() - Instances", "LogScannedPredicates() - Shadows");
				}
			}
			Profiler.EndSample();


			Profiler.BeginSample("Scan Thread Groups");
			{
				// Normal
				scanGroupSumsCS.SetBuffer(m_scanGroupSumsKernelID, _GroupSumArrayIn_ID, m_instancesGroupSumArrayBuffer);
				scanGroupSumsCS.SetBuffer(m_scanGroupSumsKernelID, _GroupSumArrayOut_ID,
					m_instancesScannedGroupSumBuffer);
				scanGroupSumsCS.Dispatch(m_scanGroupSumsKernelID, m_scanThreadGroupsGroupX, 1, 1);

				// Shadows
				scanGroupSumsCS.SetBuffer(m_scanGroupSumsKernelID, _GroupSumArrayIn_ID, m_shadowGroupSumArrayBuffer);
				scanGroupSumsCS.SetBuffer(m_scanGroupSumsKernelID, _GroupSumArrayOut_ID,
					m_shadowsScannedGroupSumBuffer);
				scanGroupSumsCS.Dispatch(m_scanGroupSumsKernelID, m_scanThreadGroupsGroupX, 1, 1);

				if (logScannedGroupSumsBuffer)
				{
					logScannedGroupSumsBuffer = false;
					LogScannedGroupSumBuffer("LogScannedGroupSumBuffer() - Instances",
						"LogScannedGroupSumBuffer() - Shadows");
				}
			}
			Profiler.EndSample();

			//拷贝数据 并且计算偏移打包
			Profiler.BeginSample("Copy Instance Data");
			{
				// Normal
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancePredicatesIn_ID,
					m_instancesIsVisibleBuffer);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _GroupSumArray_ID,
					m_instancesScannedGroupSumBuffer);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _ScannedInstancePredicates_ID,
					m_instancesScannedPredicates);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancesCulledMatrixRows01_ID,
					m_instancesCulledMatrixRows01);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancesCulledMatrixRows23_ID,
					m_instancesCulledMatrixRows23);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancesCulledMatrixRows45_ID,
					m_instancesCulledMatrixRows45);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _DrawcallDataOut_ID, m_instancesArgsBuffer);
				copyInstanceDataCS.Dispatch(m_copyInstanceDataKernelID, m_copyInstanceDataGroupX, 1, 1);

				// Shadows
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancePredicatesIn_ID,
					m_shadowsIsVisibleBuffer);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _GroupSumArray_ID,
					m_shadowsScannedGroupSumBuffer);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _ScannedInstancePredicates_ID,
					m_shadowScannedInstancePredicates);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancesCulledMatrixRows01_ID,
					m_shadowCulledMatrixRows01);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancesCulledMatrixRows23_ID,
					m_shadowCulledMatrixRows23);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancesCulledMatrixRows45_ID,
					m_shadowCulledMatrixRows45);
				copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _DrawcallDataOut_ID, m_shadowArgsBuffer);
				copyInstanceDataCS.Dispatch(m_copyInstanceDataKernelID, m_copyInstanceDataGroupX, 1, 1);

				if (logCulledInstancesDrawMatrices)
				{
					logCulledInstancesDrawMatrices = false;
					LogCulledInstancesDrawMatrices("LogCulledInstancesDrawMatrices() - Instances",
						"LogCulledInstancesDrawMatrices() - Shadows");
				}

				if (logArgsBufferAfterCopy)
				{
					logArgsBufferAfterCopy = false;
					LogArgsBuffers("LogArgsBuffers() - Instances After Copy", "LogArgsBuffers() - Shadows After Copy");
				}
			}
			Profiler.EndSample();
			
			
			Profiler.BeginSample("LOD Sorting");
			{
				m_lastCamPosition = m_camPosition;
				//在异步队列上执行CommandBuffer
				Graphics.ExecuteCommandBufferAsync(m_sortingCommandBuffer, ComputeQueueType.Background);
			}
			Profiler.EndSample();
        
			if (logSortingData)
			{
				logSortingData = false;
				LogSortingData("LogSortingData())");
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

			occlusionCS.SetInt(_ShouldFrustumCull_ID, enableFrustumCulling ? 1 : 0);
			occlusionCS.SetInt(_ShouldOcclusionCull_ID, enableOcclusionCulling ? 1 : 0);
			occlusionCS.SetInt(_ShouldDetailCull_ID, enableDetailCulling ? 1 : 0);
			occlusionCS.SetInt(_ShouldLOD_ID, enableLOD ? 1 : 0);
			occlusionCS.SetInt(_ShouldOnlyUseLOD02Shadows_ID, enableOnlyLOD02Shadows ? 1 : 0);
			occlusionCS.SetFloat(_DetailCullingScreenPercentage_ID, detailCullingPercentage);

			if (debugDrawLOD != m_debugLastDrawLOD)
			{
				m_debugLastDrawLOD = debugDrawLOD;

				if (debugDrawLOD)
				{
					for (int i = 0; i < indirectMeshes.Length; i++)
					{
						indirectMeshes[i].material.EnableKeyword(DEBUG_SHADER_LOD_KEYWORD);
					}
				}
				else
				{
					for (int i = 0; i < indirectMeshes.Length; i++)
					{
						indirectMeshes[i].material.DisableKeyword(DEBUG_SHADER_LOD_KEYWORD);
					}
				}
			}

			UpdateDebugUI();
		}

		private void UpdateDebugUI()
		{
			if (!debugShowUI)
			{
				if (m_uiObj != null)
				{
					Destroy(m_uiObj);
				}

				return;
			}

			if (m_uiObj == null)
			{
				m_uiObj = Instantiate(debugUIPrefab, transform, true);
				m_uiText = m_uiObj.transform.GetComponentInChildren<Text>();
			}

			if (m_debugGPUArgsRequest.hasError || m_debugGPUShadowArgsRequest.hasError)
			{
				m_debugGPUArgsRequest = AsyncGPUReadback.Request(m_instancesArgsBuffer);
				m_debugGPUShadowArgsRequest = AsyncGPUReadback.Request(m_shadowArgsBuffer);
			}
			else if (m_debugGPUArgsRequest.done || m_debugGPUShadowArgsRequest.done)
			{
				NativeArray<uint> argsBuffer = m_debugGPUArgsRequest.GetData<uint>();
				NativeArray<uint> shadowArgsBuffer = m_debugGPUShadowArgsRequest.GetData<uint>();

				m_debugUIText.Length = 0;

				uint totalCount = 0;
				uint totalLod00Count = 0;
				uint totalLod01Count = 0;
				uint totalLod02Count = 0;

				uint totalShadowCount = 0;
				uint totalShadowLod00Count = 0;
				uint totalShadowLod01Count = 0;
				uint totalShadowLod02Count = 0;

				uint totalIndices = 0;
				uint totalLod00Indices = 0;
				uint totalLod01Indices = 0;
				uint totalLod02Indices = 0;

				uint totalShadowIndices = 0;
				uint totalShadowLod00Indices = 0;
				uint totalShadowLod01Indices = 0;
				uint totalShadowLod02Indices = 0;

				uint totalVertices = 0;
				uint totalLod00Vertices = 0;
				uint totalLod01Vertices = 0;
				uint totalLod02Vertices = 0;

				uint totalShadowVertices = 0;
				uint totalShadowLod00Vertices = 0;
				uint totalShadowLod01Vertices = 0;
				uint totalShadowLod02Vertices = 0;

				int instanceIndex = 0;
				uint normalMultiplier = (uint) (drawInstances ? 1 : 0);
				uint shadowMultiplier =
					(uint) (drawInstanceShadows && QualitySettings.shadows != ShadowQuality.Disable ? 1 : 0);
				int cascades = QualitySettings.shadowCascades;

				m_debugUIText.AppendLine(
					$"<color=#ffffff>Name".PadRight(32) //.Substring(0, 58)
					+ $"Instances".PadRight(25) //.Substring(0, 25)
					+ $"Shadow Instances".PadRight(25) //.Substring(0, 25)
					+ $"Vertices".PadRight(31) //.Substring(0, 25)
					+ $"Indices</color>"
				);

				for (int i = 0; i < argsBuffer.Length; i = i + NUMBER_OF_ARGS_PER_INSTANCE_TYPE)
				{
					IndirectRenderingMesh irm = indirectMeshes[instanceIndex];

					uint lod00Count = argsBuffer[i + 1] * normalMultiplier;
					uint lod01Count = argsBuffer[i + 6] * normalMultiplier;
					uint lod02Count = argsBuffer[i + 11] * normalMultiplier;

					uint lod00ShadowCount = shadowArgsBuffer[i + 1] * shadowMultiplier;
					uint lod01ShadowCount = shadowArgsBuffer[i + 6] * shadowMultiplier;
					uint lod02ShadowCount = shadowArgsBuffer[i + 11] * shadowMultiplier;

					uint lod00Indices = argsBuffer[i + 0] * normalMultiplier;
					uint lod01Indices = argsBuffer[i + 5] * normalMultiplier;
					uint lod02Indices = argsBuffer[i + 10] * normalMultiplier;

					uint shadowLod00Indices = shadowArgsBuffer[i + 0] * shadowMultiplier;
					uint shadowLod01Indices = shadowArgsBuffer[i + 5] * shadowMultiplier;
					uint shadowLod02Indices = shadowArgsBuffer[i + 10] * shadowMultiplier;

					uint lod00Vertices = irm.numOfIndicesLod00 * normalMultiplier;
					uint lod01Vertices = irm.numOfIndicesLod01 * normalMultiplier;
					uint lod02Vertices = irm.numOfIndicesLod02 * normalMultiplier;

					uint shadowLod00Vertices = irm.numOfIndicesLod00 * shadowMultiplier;
					uint shadowLod01Vertices = irm.numOfIndicesLod01 * shadowMultiplier;
					uint shadowLod02Vertices = irm.numOfIndicesLod02 * shadowMultiplier;

					// Output...
					string lod00VertColor = (lod00Vertices > 10000 ? DEBUG_UI_RED_COLOR : DEBUG_UI_WHITE_COLOR);
					string lod01VertColor = (lod01Vertices > 5000 ? DEBUG_UI_RED_COLOR : DEBUG_UI_WHITE_COLOR);
					string lod02VertColor = (lod02Vertices > 1000 ? DEBUG_UI_RED_COLOR : DEBUG_UI_WHITE_COLOR);

					string lod00IndicesColor = (lod00Indices > (lod00Vertices * 3.33f)
						? DEBUG_UI_RED_COLOR
						: DEBUG_UI_WHITE_COLOR);
					string lod01IndicesColor = (lod01Indices > (lod01Vertices * 3.33f)
						? DEBUG_UI_RED_COLOR
						: DEBUG_UI_WHITE_COLOR);
					string lod02IndicesColor = (lod02Indices > (lod02Vertices * 3.33f)
						? DEBUG_UI_RED_COLOR
						: DEBUG_UI_WHITE_COLOR);

					m_debugUIText.AppendLine(
						$"<b><color=#809fff>{instanceIndex}. {irm.mesh.name}".PadRight(200)
							.Substring(0, 35) + "</color></b>"
							                  + $"({lod00Count}, {lod01Count}, {lod02Count})"
								                  .PadRight(200).Substring(0, 25)
							                  + $"({lod00ShadowCount},{lod01ShadowCount}, {lod02ShadowCount})"
								                  .PadRight(200).Substring(0, 25)
							                  + $"({lod00VertColor}{lod00Vertices,5}</color>, {lod01VertColor}{lod01Vertices,5}</color>, {lod02VertColor}{lod02Vertices,5})</color>"
								                  .PadRight(200).Substring(0, 100)
							                  + $"({lod00IndicesColor}{lod00Indices,5}</color>, {lod01IndicesColor}{lod01Indices,5}</color>, {lod02IndicesColor}{lod02Indices,5})</color>"
								                  .PadRight(5)
					);


					// Total
					uint sumCount = lod00Count + lod01Count + lod02Count;
					uint sumShadowCount = lod00ShadowCount + lod01ShadowCount + lod02ShadowCount;

					uint sumLod00Indices = lod00Count * lod00Indices;
					uint sumLod01Indices = lod01Count * lod01Indices;
					uint sumLod02Indices = lod02Count * lod02Indices;
					uint sumIndices = sumLod00Indices + sumLod01Indices + sumLod02Indices;

					uint sumShadowLod00Indices = lod00ShadowCount * shadowLod00Indices;
					uint sumShadowLod01Indices = lod01ShadowCount * shadowLod01Indices;
					uint sumShadowLod02Indices = lod02ShadowCount * shadowLod02Indices;
					uint sumShadowIndices = sumShadowLod00Indices + sumShadowLod01Indices + sumShadowLod02Indices;

					uint sumLod00Vertices = lod00Count * lod00Vertices;
					uint sumLod01Vertices = lod01Count * lod01Vertices;
					uint sumLod02Vertices = lod02Count * lod02Vertices;
					uint sumVertices = sumLod00Vertices + sumLod01Vertices + sumLod02Vertices;

					uint sumShadowLod00Vertices = lod00ShadowCount * shadowLod00Vertices;
					uint sumShadowLod01Vertices = lod01ShadowCount * shadowLod01Vertices;
					uint sumShadowLod02Vertices = lod02ShadowCount * shadowLod02Vertices;
					uint sumShadowVertices = sumShadowLod00Vertices + sumShadowLod01Vertices + sumShadowLod02Vertices;

					totalCount += sumCount;
					totalLod00Count += lod00Count;
					totalLod01Count += lod01Count;
					totalLod02Count += lod02Count;

					totalShadowCount += sumShadowCount;
					totalShadowLod00Count += lod00ShadowCount;
					totalShadowLod01Count += lod01ShadowCount;
					totalShadowLod02Count += lod02ShadowCount;

					totalIndices += sumIndices;
					totalLod00Indices += sumLod00Indices;
					totalLod01Indices += sumLod01Indices;
					totalLod02Indices += sumLod02Indices;

					totalShadowIndices += sumShadowIndices;
					totalShadowLod00Indices += sumShadowLod00Indices;
					totalShadowLod01Indices += sumShadowLod01Indices;
					totalShadowLod02Indices += sumShadowLod02Indices;

					totalVertices += sumVertices;
					totalLod00Vertices += sumLod00Vertices;
					totalLod01Vertices += sumLod01Vertices;
					totalLod02Vertices += sumLod02Vertices;

					totalShadowVertices += sumShadowVertices;
					totalShadowLod00Vertices += sumShadowLod00Vertices;
					totalShadowLod01Vertices += sumShadowLod01Vertices;
					totalShadowLod02Vertices += sumShadowLod02Vertices;


					instanceIndex++;
				}


				m_debugUIText.AppendLine();
				m_debugUIText.AppendLine("<b>Total</b>");
				m_debugUIText.AppendLine(
					string.Format(
						"Instances:".PadRight(10).Substring(0, 10) + " {0, 8} ({1, 8}, {2, 8}, {3, 8})",
						totalCount,
						totalLod00Count,
						totalLod01Count,
						totalLod02Count,
						totalShadowCount
					)
				);
				m_debugUIText.AppendLine(
					string.Format(
						"Vertices:".PadRight(10).Substring(0, 10) + " {0, 8} ({1, 8}, {2, 8}, {3, 8})",
						totalVertices,
						totalLod00Vertices,
						totalLod01Vertices,
						totalLod02Vertices
					)
				);
				m_debugUIText.AppendLine(
					string.Format(
						"Indices:".PadRight(10).Substring(0, 10) + " {0, 8} ({1, 8}, {2, 8}, {3, 8})",
						totalIndices,
						totalLod00Indices,
						totalLod01Indices,
						totalLod02Indices
					)
				);

				m_debugUIText.AppendLine();
				m_debugUIText.AppendLine("<b>Shadow</b>");
				m_debugUIText.AppendLine(
					string.Format(
						"Instances:".PadRight(10).Substring(0, 10) + " {0, 8} ({1, 8}, {2, 8}, {3, 8}) * " + cascades +
						" Cascades"
						+ " ==> {4, 8} ({5, 8}, {6, 8}, {7, 8})",
						totalShadowCount,
						totalShadowLod00Count,
						totalShadowLod01Count,
						totalShadowLod02Count,
						totalShadowCount * cascades,
						totalShadowLod00Count * cascades,
						totalShadowLod01Count * cascades,
						totalShadowLod02Count * cascades
					)
				);

				m_debugUIText.AppendLine(
					string.Format(
						"Vertices:".PadRight(10).Substring(0, 10) + " {0, 8} ({1, 8}, {2, 8}, {3, 8}) * " + cascades +
						" Cascades"
						+ " ==> {4, 8} ({5, 8}, {6, 8}, {7, 8})",
						totalShadowVertices,
						totalShadowLod00Vertices,
						totalShadowLod01Vertices,
						totalShadowLod02Vertices,
						totalShadowVertices * cascades,
						totalShadowLod00Vertices * cascades,
						totalShadowLod01Vertices * cascades,
						totalShadowLod02Vertices * cascades
					)
				);
				m_debugUIText.AppendLine(
					string.Format(
						"Indices:".PadRight(10).Substring(0, 10) + " {0, 8} ({1, 8}, {2, 8}, {3, 8}) * " + cascades +
						" Cascades"
						+ " ==> {4, 8} ({5, 8}, {6, 8}, {7, 8})",
						totalShadowIndices,
						totalShadowLod00Indices,
						totalShadowLod01Indices,
						totalShadowLod02Indices,
						totalShadowIndices * cascades,
						totalShadowLod00Indices * cascades,
						totalShadowLod01Indices * cascades,
						totalShadowLod02Indices * cascades
					)
				);

				m_uiText.text = m_debugUIText.ToString();
				m_debugGPUArgsRequest = AsyncGPUReadback.Request(m_instancesArgsBuffer);
				m_debugGPUShadowArgsRequest = AsyncGPUReadback.Request(m_shadowArgsBuffer);
			}
		}

		private void LogArgsBuffers(string instancePrefix = "", string shadowPrefix = "")
		{
			uint[] instancesArgs = new uint[m_numberOfInstanceTypes * NUMBER_OF_ARGS_PER_INSTANCE_TYPE];
			uint[] shadowArgs = new uint[m_numberOfInstanceTypes * NUMBER_OF_ARGS_PER_INSTANCE_TYPE];
			m_instancesArgsBuffer.GetData(instancesArgs);
			m_shadowArgsBuffer.GetData(shadowArgs);

			StringBuilder instancesSB = new StringBuilder();
			StringBuilder shadowsSB = new StringBuilder();

			if (!string.IsNullOrEmpty(instancePrefix))
			{
				instancesSB.AppendLine(instancePrefix);
			}

			if (!string.IsNullOrEmpty(shadowPrefix))
			{
				shadowsSB.AppendLine(shadowPrefix);
			}

			instancesSB.AppendLine("");
			shadowsSB.AppendLine("");

			instancesSB.AppendLine(
				"IndexCountPerInstance InstanceCount StartIndexLocation BaseVertexLocation StartInstanceLocation");
			shadowsSB.AppendLine(
				"IndexCountPerInstance InstanceCount StartIndexLocation BaseVertexLocation StartInstanceLocation");

			int counter = 0;
			instancesSB.AppendLine(indirectMeshes[counter].mesh.name);
			shadowsSB.AppendLine(indirectMeshes[counter].mesh.name);

			for (int i = 0; i < instancesArgs.Length; i++)
			{
				instancesSB.Append(instancesArgs[i] + " ");
				shadowsSB.Append(shadowArgs[i] + " ");

				if ((i + 1) % 5 == 0)
				{
					instancesSB.AppendLine("");
					shadowsSB.AppendLine("");

					if ((i + 1) < instancesArgs.Length
					    && (i + 1) % NUMBER_OF_ARGS_PER_INSTANCE_TYPE == 0)
					{
						instancesSB.AppendLine("");
						shadowsSB.AppendLine("");

						counter++;
						IndirectRenderingMesh irm = indirectMeshes[counter];
						Mesh m = irm.mesh;
						instancesSB.AppendLine(m.name);
						shadowsSB.AppendLine(m.name);
					}
				}
			}


			Debug.Log(instancesSB.ToString());
			Debug.Log(shadowsSB.ToString());
		}

		private void LogInstanceDrawMatrices(string prefix = "")
		{
			Indirect2x2Matrix[] matrix1 = new Indirect2x2Matrix[m_numberOfInstances];
			Indirect2x2Matrix[] matrix2 = new Indirect2x2Matrix[m_numberOfInstances];
			Indirect2x2Matrix[] matrix3 = new Indirect2x2Matrix[m_numberOfInstances];
			m_instancesMatrixRows01.GetData(matrix1);
			m_instancesMatrixRows23.GetData(matrix2);
			m_instancesMatrixRows45.GetData(matrix3);

			StringBuilder sb = new StringBuilder();
			if (!string.IsNullOrEmpty(prefix))
			{
				sb.AppendLine(prefix);
			}

			for (int i = 0; i < matrix1.Length; i++)
			{
				sb.AppendLine(
					i + "\n"
					  + matrix1[i].row0 + "\n"
					  + matrix1[i].row1 + "\n"
					  + matrix2[i].row0 + "\n"
					  + "\n\n"
					  + matrix2[i].row1 + "\n"
					  + matrix3[i].row0 + "\n"
					  + matrix3[i].row1 + "\n"
					  + "\n"
				);
			}

			Debug.Log(sb.ToString());
		}

		private void LogInstancesIsVisibleBuffers(string instancePrefix = "", string shadowPrefix = "")
		{
			uint[] instancesIsVisible = new uint[m_numberOfInstances];
			uint[] shadowsIsVisible = new uint[m_numberOfInstances];
			m_instancesIsVisibleBuffer.GetData(instancesIsVisible);
			m_shadowsIsVisibleBuffer.GetData(shadowsIsVisible);

			StringBuilder instancesSB = new StringBuilder();
			StringBuilder shadowsSB = new StringBuilder();

			if (!string.IsNullOrEmpty(instancePrefix))
			{
				instancesSB.AppendLine(instancePrefix);
			}

			if (!string.IsNullOrEmpty(shadowPrefix))
			{
				shadowsSB.AppendLine(shadowPrefix);
			}

			for (int i = 0; i < instancesIsVisible.Length; i++)
			{
				instancesSB.AppendLine(i + ": " + instancesIsVisible[i]);
				shadowsSB.AppendLine(i + ": " + shadowsIsVisible[i]);
			}

			Debug.Log(instancesSB.ToString());
			Debug.Log(shadowsSB.ToString());
		}

		private void LogGroupSumArrayBuffer(string instancePrefix = "", string shadowPrefix = "")
		{
			uint[] instancesScannedData = new uint[m_numberOfInstances];
			uint[] shadowsScannedData = new uint[m_numberOfInstances];
			m_instancesGroupSumArrayBuffer.GetData(instancesScannedData);
			m_shadowsScannedGroupSumBuffer.GetData(shadowsScannedData);

			StringBuilder instancesSB = new StringBuilder();
			StringBuilder shadowsSB = new StringBuilder();

			if (!string.IsNullOrEmpty(instancePrefix))
			{
				instancesSB.AppendLine(instancePrefix);
			}

			if (!string.IsNullOrEmpty(shadowPrefix))
			{
				shadowsSB.AppendLine(shadowPrefix);
			}

			for (int i = 0; i < instancesScannedData.Length; i++)
			{
				instancesSB.AppendLine(i + ": " + instancesScannedData[i]);
				shadowsSB.AppendLine(i + ": " + shadowsScannedData[i]);
			}

			Debug.Log(instancesSB.ToString());
			Debug.Log(shadowsSB.ToString());
		}

		private void LogScannedPredicates(string instancePrefix = "", string shadowPrefix = "")
		{
			uint[] instancesScannedData = new uint[m_numberOfInstances];
			uint[] shadowsScannedData = new uint[m_numberOfInstances];
			m_instancesScannedPredicates.GetData(instancesScannedData);
			m_shadowScannedInstancePredicates.GetData(shadowsScannedData);

			StringBuilder instancesSB = new StringBuilder();
			StringBuilder shadowsSB = new StringBuilder();

			if (!string.IsNullOrEmpty(instancePrefix))
			{
				instancesSB.AppendLine(instancePrefix);
			}

			if (!string.IsNullOrEmpty(shadowPrefix))
			{
				shadowsSB.AppendLine(shadowPrefix);
			}

			for (int i = 0; i < instancesScannedData.Length; i++)
			{
				instancesSB.AppendLine(i + ": " + instancesScannedData[i]);
				shadowsSB.AppendLine(i + ": " + shadowsScannedData[i]);
			}

			Debug.Log(instancesSB.ToString());
			Debug.Log(shadowsSB.ToString());
		}

		private void LogScannedGroupSumBuffer(string instancePrefix = "", string shadowPrefix = "")
		{
			uint[] instancesScannedData = new uint[m_numberOfInstances];
			uint[] shadowsScannedData = new uint[m_numberOfInstances];
			m_instancesScannedPredicates.GetData(instancesScannedData);
			m_shadowScannedInstancePredicates.GetData(shadowsScannedData);

			StringBuilder instancesSB = new StringBuilder();
			StringBuilder shadowsSB = new StringBuilder();

			if (!string.IsNullOrEmpty(instancePrefix))
			{
				instancesSB.AppendLine(instancePrefix);
			}

			if (!string.IsNullOrEmpty(shadowPrefix))
			{
				shadowsSB.AppendLine(shadowPrefix);
			}

			for (int i = 0; i < instancesScannedData.Length; i++)
			{
				instancesSB.AppendLine(i + ": " + instancesScannedData[i]);
				shadowsSB.AppendLine(i + ": " + shadowsScannedData[i]);
			}

			Debug.Log(instancesSB.ToString());
			Debug.Log(shadowsSB.ToString());
		}

		private void LogCulledInstancesDrawMatrices(string instancePrefix = "", string shadowPrefix = "")
		{
			Indirect2x2Matrix[] instancesMatrix1 = new Indirect2x2Matrix[m_numberOfInstances];
			Indirect2x2Matrix[] instancesMatrix2 = new Indirect2x2Matrix[m_numberOfInstances];
			Indirect2x2Matrix[] instancesMatrix3 = new Indirect2x2Matrix[m_numberOfInstances];
			m_instancesCulledMatrixRows01.GetData(instancesMatrix1);
			m_instancesCulledMatrixRows23.GetData(instancesMatrix2);
			m_instancesCulledMatrixRows45.GetData(instancesMatrix3);

			Indirect2x2Matrix[] shadowsMatrix1 = new Indirect2x2Matrix[m_numberOfInstances];
			Indirect2x2Matrix[] shadowsMatrix2 = new Indirect2x2Matrix[m_numberOfInstances];
			Indirect2x2Matrix[] shadowsMatrix3 = new Indirect2x2Matrix[m_numberOfInstances];
			m_shadowCulledMatrixRows01.GetData(shadowsMatrix1);
			m_shadowCulledMatrixRows23.GetData(shadowsMatrix2);
			m_shadowCulledMatrixRows45.GetData(shadowsMatrix3);

			StringBuilder instancesSB = new StringBuilder();
			StringBuilder shadowsSB = new StringBuilder();

			if (!string.IsNullOrEmpty(instancePrefix))
			{
				instancesSB.AppendLine(instancePrefix);
			}

			if (!string.IsNullOrEmpty(shadowPrefix))
			{
				shadowsSB.AppendLine(shadowPrefix);
			}

			for (int i = 0; i < instancesMatrix1.Length; i++)
			{
				instancesSB.AppendLine(
					i + "\n"
					  + instancesMatrix1[i].row0 + "\n"
					  + instancesMatrix1[i].row1 + "\n"
					  + instancesMatrix2[i].row0 + "\n"
					  + "\n\n"
					  + instancesMatrix2[i].row1 + "\n"
					  + instancesMatrix3[i].row0 + "\n"
					  + instancesMatrix3[i].row1 + "\n"
					  + "\n"
				);

				shadowsSB.AppendLine(
					i + "\n"
					  + shadowsMatrix1[i].row0 + "\n"
					  + shadowsMatrix1[i].row1 + "\n"
					  + shadowsMatrix2[i].row0 + "\n"
					  + "\n\n"
					  + shadowsMatrix2[i].row1 + "\n"
					  + shadowsMatrix3[i].row0 + "\n"
					  + shadowsMatrix3[i].row1 + "\n"
					  + "\n"
				);
			}

			Debug.Log(instancesSB.ToString());
			Debug.Log(shadowsSB.ToString());
		}

		private void LogSortingData(string prefix = "")
		{
			SortingData[] sortingData = new SortingData[m_numberOfInstances];
			m_instancesSortingData.GetData(sortingData);
        
			StringBuilder sb = new StringBuilder();
			if (!string.IsNullOrEmpty(prefix)) { sb.AppendLine(prefix); }
        
			uint lastDrawCallIndex = 0;
			for (int i = 0; i < sortingData.Length; i++)
			{
				uint drawCallIndex = (sortingData[i].drawCallInstanceIndex >> 16);
				uint instanceIndex = (sortingData[i].drawCallInstanceIndex) & 0xFFFF;
				if (i == 0) { lastDrawCallIndex = drawCallIndex; }
				sb.AppendLine("(" + drawCallIndex + ") --> " + sortingData[i].distanceToCam + " instanceIndex:" + instanceIndex);
            
				if (lastDrawCallIndex != drawCallIndex)
				{
					Debug.Log(sb.ToString());
					sb.Clear();
					lastDrawCallIndex = drawCallIndex;
				}
			}

			Debug.Log(sb.ToString());
		}
		
		#endregion
	}
}