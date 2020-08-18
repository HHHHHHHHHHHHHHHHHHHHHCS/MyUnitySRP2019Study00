using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.UI;
using ShadowQuality = UnityEngine.ShadowQuality;

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

		/// <summary>
		/// 实例化go的center和extends
		/// </summary>
		[Header("Data")] [ReadOnly]
		public List<IndirectInstanceCSInput> instancesInputData = new List<IndirectInstanceCSInput>();

		/// <summary>
		/// 要画的数据种类
		/// </summary>
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

		/// <summary>
		/// instanceCount * 15  3个lod * |vertexCount,instanceCount,startIndex,baseVertexIndex,startInstance|
		/// </summary>
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
		private static readonly int _Data_ID = Shader.PropertyToID("_Data");
		private static readonly int _Input_ID = Shader.PropertyToID("_Input");
		private static readonly int _ShouldFrustumCull_ID = Shader.PropertyToID("_ShouldFrustumCull");
		private static readonly int _ShouldOcclusionCull_ID = Shader.PropertyToID("_ShouldOcclusionCull");
		private static readonly int _ShouldLOD_ID = Shader.PropertyToID("_ShouldLOD");
		private static readonly int _ShouldDetailCull_ID = Shader.PropertyToID("_ShouldDetailCull");
		private static readonly int _ShouldOnlyUseLOD02Shadows_ID = Shader.PropertyToID("_ShouldOnlyUseLOD02Shadows");
		private static readonly int _UNITY_MATRIX_MVP_ID = Shader.PropertyToID("_UNITY_MATRIX_MVP");
		private static readonly int _CamPosition_ID = Shader.PropertyToID("_CamPosition");
		private static readonly int _HiZTextureSize_ID = Shader.PropertyToID("_HiZTextureSize");
		private static readonly int _Level_ID = Shader.PropertyToID("_Level");
		private static readonly int _LevelMask_ID = Shader.PropertyToID("_LevelMask");
		private static readonly int _Width_ID = Shader.PropertyToID("_Width");
		private static readonly int _Height_ID = Shader.PropertyToID("_Height");
		private static readonly int _ShadowDistance_ID = Shader.PropertyToID("_ShadowDistance");

		private static readonly int _DetailCullingScreenPercentage_ID =
			Shader.PropertyToID("_DetailCullingScreenPercentage");

		private static readonly int _HiZMap_ID = Shader.PropertyToID("_HiZMap");
		private static readonly int _NumOfGroups_ID = Shader.PropertyToID("_NumOfGroups");
		private static readonly int _NumOfDrawcalls_ID = Shader.PropertyToID("_NumOfDrawcalls");
		private static readonly int _ArgsOffset_ID = Shader.PropertyToID("_ArgsOffset");
		private static readonly int _Positions_ID = Shader.PropertyToID("_Positions");
		private static readonly int _Scales_ID = Shader.PropertyToID("_Scales");
		private static readonly int _Rotations_ID = Shader.PropertyToID("_Rotations");
		private static readonly int _ArgsBuffer_ID = Shader.PropertyToID("_ArgsBuffer");
		private static readonly int _ShadowArgsBuffer_ID = Shader.PropertyToID("_ShadowArgsBuffer");
		private static readonly int _IsVisibleBuffer_ID = Shader.PropertyToID("_IsVisibleBuffer");
		private static readonly int _ShadowIsVisibleBuffer_ID = Shader.PropertyToID("_ShadowIsVisibleBuffer");
		private static readonly int _GroupSumArray_ID = Shader.PropertyToID("_GroupSumArray");
		private static readonly int _ScannedInstancePredicates_ID = Shader.PropertyToID("_ScannedInstancePredicates");
		private static readonly int _GroupSumArrayIn_ID = Shader.PropertyToID("_GroupSumArrayIn");
		private static readonly int _GroupSumArrayOut_ID = Shader.PropertyToID("_GroupSumArrayOut");
		private static readonly int _DrawcallDataOut_ID = Shader.PropertyToID("_DrawcallDataOut");
		private static readonly int _SortingData_ID = Shader.PropertyToID("_SortingData");
		private static readonly int _InstanceDataBuffer_ID = Shader.PropertyToID("_InstanceDataBuffer");
		private static readonly int _InstancePredicatesIn_ID = Shader.PropertyToID("_InstancePredicatesIn");
		private static readonly int _InstancesDrawMatrixRows01_ID = Shader.PropertyToID("_InstancesDrawMatrixRows01");
		private static readonly int _InstancesDrawMatrixRows23_ID = Shader.PropertyToID("_InstancesDrawMatrixRows23");
		private static readonly int _InstancesDrawMatrixRows45_ID = Shader.PropertyToID("_InstancesDrawMatrixRows45");

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

			if (drawInstances)
			{
				Profiler.BeginSample("DrawInstances()");
				DrawInstances();
				Profiler.EndSample();
			}

			if (drawInstanceShadows)
			{
				Profiler.BeginSample("DrawInstanceShadows()");
				DrawInstanceShadows();
				Profiler.EndSample();
			}

			if (debugDrawHiZ)
			{
				Vector3 pos = transform.position;
				pos.y = debugCamera.transform.position.y;
				debugCamera.transform.position = pos;
				debugCamera.Render();
			}
		}

		private void OnDestroy()
		{
			ReleaseBuffers();

			if (debugDrawLOD)
			{
				for (int i = 0; i < indirectMeshes.Length; i++)
				{
					indirectMeshes[i].material.DisableKeyword(DEBUG_SHADER_LOD_KEYWORD);
				}
			}
		}

		private void OnDrawGizmos()
		{
			if (!Application.isPlaying)
			{
				return;
			}

			if (debugDrawBoundsInSceneView)
			{
				Gizmos.color = new Color(1f, 0f, 0f, 0.333f);
				for (int i = 0; i < instancesInputData.Count; i++)
				{
					Gizmos.DrawWireCube(instancesInputData[i].boundsCenter, instancesInputData[i].boundsExtents * 2f);
				}
			}
		}

		#endregion


		#region Public Functions

		public void Initialize(ref HiZIndirectInstanceData[] instances)
		{
			if (!m_isInitialized)
			{
				m_isInitialized = InitializeRenderer(ref instances);
			}
		}

		public void StartDrawing()
		{
			if (!m_isInitialized)
			{
				Debug.LogError("IndirectRenderer: Unable to start drawing because it's not initialized");
				return;
			}

			m_isEnabled = true;
		}

		public void StopDrawing(bool shouldReleaseBuffers = false)
		{
			m_isEnabled = false;

			if (shouldReleaseBuffers)
			{
				ReleaseBuffers();
				m_isInitialized = false;
				hiZBuffer.enabled = false;
			}
		}

		#endregion

		#region Private Functions

		private static bool TryGetKernel(string kernelName, ref ComputeShader cs, ref int kernelID)
		{
			if (!cs.HasKernel(kernelName))
			{
				Debug.LogError(kernelName + " kernel not found in " + cs.name + "!");
				return false;
			}

			kernelID = cs.FindKernel(kernelName);
			return true;
		}

		private static void ReleaseCommandBuffer(ref CommandBuffer _buffer)
		{
			if (_buffer == null)
			{
				return;
			}

			_buffer.Release();
			_buffer = null;
		}

		private static void ReleaseComputeBuffer(ref ComputeBuffer _buffer)
		{
			if (_buffer == null)
			{
				return;
			}

			_buffer.Release();
			_buffer = null;
		}

		/// <summary>
		/// 根据传入的 要绘制的数据  进行初始化
		/// </summary>
		/// <param name="_instances"></param>
		/// <returns></returns>
		private bool InitializeRenderer(ref HiZIndirectInstanceData[] _instances)
		{
			if (!TryGetKernels())
			{
				return false;
			}

			ReleaseBuffers();
			instancesInputData.Clear();
			m_numberOfInstanceTypes = _instances.Length;
			m_numberOfInstances = 0;
			m_camPosition = mainCamera.transform.position;
			m_bounds.center = m_camPosition;
			m_bounds.extents = Vector3.one * 10000;
			hiZBuffer.enabled = true;
			//TODO:InitializeTexture
			//hiZBuffer.InitializeTexture();
			indirectMeshes = new IndirectRenderingMesh[m_numberOfInstanceTypes];
			m_args = new uint[m_numberOfInstanceTypes * NUMBER_OF_ARGS_PER_INSTANCE_TYPE];

			List<Vector3> positions = new List<Vector3>();
			List<Vector3> scales = new List<Vector3>();
			List<Vector3> rotations = new List<Vector3>();
			List<SortingData> sortingData = new List<SortingData>();

			for (int i = 0; i < m_numberOfInstanceTypes; i++)
			{
				IndirectRenderingMesh irm = new IndirectRenderingMesh();
				HiZIndirectInstanceData iid = _instances[i];

				irm.numOfVerticesLod00 = (uint) iid.lod00Mesh.vertexCount;
				irm.numOfVerticesLod01 = (uint) iid.lod01Mesh.vertexCount;
				irm.numOfVerticesLod02 = (uint) iid.lod02Mesh.vertexCount;
				irm.numOfIndicesLod00 = iid.lod00Mesh.GetIndexCount(0);
				irm.numOfIndicesLod01 = iid.lod01Mesh.GetIndexCount(0);
				irm.numOfIndicesLod02 = iid.lod02Mesh.GetIndexCount(0);


				irm.mesh = new Mesh();
				irm.mesh.name = iid.prefab.name;
				irm.mesh.CombineMeshes(
					new CombineInstance[]
					{
						new CombineInstance() {mesh = iid.lod00Mesh},
						new CombineInstance() {mesh = iid.lod01Mesh},
						new CombineInstance() {mesh = iid.lod02Mesh},
					},
					true, //merge subMeshes
					false, //use matrices
					false //has lightmap data
				);

				// Arguments
				int argsIndex = i * NUMBER_OF_ARGS_PER_INSTANCE_TYPE;

				// Buffer with arguments has to have five integer numbers
				// LOD00
				m_args[argsIndex + 0] = irm.numOfIndicesLod00; // 0 - index count per instance, 
				m_args[argsIndex + 1] = 0; // 1 - instance count
				m_args[argsIndex + 2] = 0; // 2 - start index location
				m_args[argsIndex + 3] = 0; // 3 - base vertex location
				m_args[argsIndex + 4] = 0; // 4 - start instance location

				// LOD01
				m_args[argsIndex + 5] = irm.numOfIndicesLod01; // 0 - index count per instance, 
				m_args[argsIndex + 6] = 0; // 1 - instance count
				m_args[argsIndex + 7] = m_args[argsIndex + 0] + m_args[argsIndex + 2]; // 2 - start index location
				m_args[argsIndex + 8] = 0; // 3 - base vertex location
				m_args[argsIndex + 9] = 0; // 4 - start instance location

				// LOD02
				m_args[argsIndex + 10] = irm.numOfIndicesLod02; // 0 - index count per instance, 
				m_args[argsIndex + 11] = 0; // 1 - instance count
				m_args[argsIndex + 12] = m_args[argsIndex + 5] + m_args[argsIndex + 7]; // 2 - start index location
				m_args[argsIndex + 13] = 0; // 3 - base vertex location
				m_args[argsIndex + 14] = 0; // 4 - start instance location

				irm.material = iid.indirectMaterial;
				Bounds originalBounds = CalculateBounds(iid.prefab);

				// Add the instance data (positions, rotations, scaling, bounds...)
				for (int j = 0; j < iid.positions.Length; j++)
				{
					positions.Add(iid.positions[j]);
					rotations.Add(iid.rotations[j]);
					scales.Add(iid.scales[j]);

					sortingData.Add(new SortingData()
					{
						//drawCallInstanceIndex = 大组index*15 + 实例化index
						drawCallInstanceIndex = ((((uint) i * NUMBER_OF_ARGS_PER_INSTANCE_TYPE) << 16) +
						                         ((uint) m_numberOfInstances)),
						distanceToCam = Vector3.Distance(iid.positions[j], m_camPosition)
					});

					Bounds b = new Bounds();
					b.center = iid.positions[j];
					Vector3 s = originalBounds.size;
					s.Scale(iid.scales[j]);
					b.size = s;

					instancesInputData.Add(new IndirectInstanceCSInput()
					{
						boundsCenter = b.center,
						boundsExtents = b.extents,
					});

					m_numberOfInstances++;
				}

				indirectMeshes[i] = irm;
			}

			//Marshal.SizeOf 可以sizeof(自定义的引用类型)
			//类需要 内存排序  就是加属性 [StructLayout(LayoutKind.Sequential)]
			//Marshal.SizeOf完全不支持泛型
			int computeShaderInputSize = Marshal.SizeOf(typeof(IndirectInstanceCSInput));
			int computeShaderDrawMatrixSize = Marshal.SizeOf(typeof(Indirect2x2Matrix));
			int computeSortingDataSize = Marshal.SizeOf(typeof(SortingData));


			m_instancesArgsBuffer = new ComputeBuffer(m_numberOfInstanceTypes * NUMBER_OF_ARGS_PER_INSTANCE_TYPE,
				sizeof(uint), ComputeBufferType.IndirectArguments);

			m_instanceDataBuffer =
				new ComputeBuffer(m_numberOfInstances, computeShaderInputSize, ComputeBufferType.Default);
			m_instancesSortingData =
				new ComputeBuffer(m_numberOfInstances, computeSortingDataSize, ComputeBufferType.Default);
			m_instancesSortingDataTemp =
				new ComputeBuffer(m_numberOfInstances, computeSortingDataSize, ComputeBufferType.Default);
			m_instancesMatrixRows01 = new ComputeBuffer(m_numberOfInstances, computeShaderDrawMatrixSize,
				ComputeBufferType.Default);
			m_instancesMatrixRows23 = new ComputeBuffer(m_numberOfInstances, computeShaderDrawMatrixSize,
				ComputeBufferType.Default);
			m_instancesMatrixRows45 = new ComputeBuffer(m_numberOfInstances, computeShaderDrawMatrixSize,
				ComputeBufferType.Default);
			m_instancesCulledMatrixRows01 = new ComputeBuffer(m_numberOfInstances, computeShaderDrawMatrixSize,
				ComputeBufferType.Default);
			m_instancesCulledMatrixRows23 = new ComputeBuffer(m_numberOfInstances, computeShaderDrawMatrixSize,
				ComputeBufferType.Default);
			m_instancesCulledMatrixRows45 = new ComputeBuffer(m_numberOfInstances, computeShaderDrawMatrixSize,
				ComputeBufferType.Default);
			m_instancesIsVisibleBuffer =
				new ComputeBuffer(m_numberOfInstances, sizeof(uint), ComputeBufferType.Default);
			m_instancesScannedPredicates =
				new ComputeBuffer(m_numberOfInstances, sizeof(uint), ComputeBufferType.Default);
			m_instancesGroupSumArrayBuffer =
				new ComputeBuffer(m_numberOfInstances, sizeof(uint), ComputeBufferType.Default);
			m_instancesScannedGroupSumBuffer =
				new ComputeBuffer(m_numberOfInstances, sizeof(uint), ComputeBufferType.Default);

			m_shadowArgsBuffer = new ComputeBuffer(m_numberOfInstanceTypes * NUMBER_OF_ARGS_PER_INSTANCE_TYPE,
				sizeof(uint), ComputeBufferType.IndirectArguments);
			m_shadowCulledMatrixRows01 = new ComputeBuffer(m_numberOfInstances, computeShaderDrawMatrixSize,
				ComputeBufferType.Default);
			m_shadowCulledMatrixRows23 = new ComputeBuffer(m_numberOfInstances, computeShaderDrawMatrixSize,
				ComputeBufferType.Default);
			m_shadowCulledMatrixRows45 = new ComputeBuffer(m_numberOfInstances, computeShaderDrawMatrixSize,
				ComputeBufferType.Default);
			m_shadowsIsVisibleBuffer = new ComputeBuffer(m_numberOfInstances, sizeof(uint), ComputeBufferType.Default);
			m_shadowScannedInstancePredicates =
				new ComputeBuffer(m_numberOfInstances, sizeof(uint), ComputeBufferType.Default);
			m_shadowGroupSumArrayBuffer =
				new ComputeBuffer(m_numberOfInstances, sizeof(uint), ComputeBufferType.Default);
			m_shadowsScannedGroupSumBuffer =
				new ComputeBuffer(m_numberOfInstances, sizeof(uint), ComputeBufferType.Default);

			m_instancesArgsBuffer.SetData(m_args);
			m_shadowArgsBuffer.SetData(m_args);
			m_instancesSortingData.SetData(sortingData);
			m_instancesSortingDataTemp.SetData(sortingData);
			m_instanceDataBuffer.SetData(instancesInputData);

			for (int i = 0; i < indirectMeshes.Length; i++)
			{
				IndirectRenderingMesh irm = indirectMeshes[i];
				int argsIndex = i * NUMBER_OF_ARGS_PER_INSTANCE_TYPE;

				irm.lod00MatPropBlock = new MaterialPropertyBlock();
				irm.lod01MatPropBlock = new MaterialPropertyBlock();
				irm.lod02MatPropBlock = new MaterialPropertyBlock();
				irm.shadowLod00MatPropBlock = new MaterialPropertyBlock();
				irm.shadowLod01MatPropBlock = new MaterialPropertyBlock();
				irm.shadowLod02MatPropBlock = new MaterialPropertyBlock();

				irm.lod00MatPropBlock.SetInt(_ArgsOffset_ID, argsIndex + 4);
				irm.lod01MatPropBlock.SetInt(_ArgsOffset_ID, argsIndex + 9);
				irm.lod02MatPropBlock.SetInt(_ArgsOffset_ID, argsIndex + 14);

				irm.shadowLod00MatPropBlock.SetInt(_ArgsOffset_ID, argsIndex + 4);
				irm.shadowLod01MatPropBlock.SetInt(_ArgsOffset_ID, argsIndex + 9);
				irm.shadowLod02MatPropBlock.SetInt(_ArgsOffset_ID, argsIndex + 14);

				irm.lod00MatPropBlock.SetBuffer(_ArgsBuffer_ID, m_instancesArgsBuffer);
				irm.lod01MatPropBlock.SetBuffer(_ArgsBuffer_ID, m_instancesArgsBuffer);
				irm.lod02MatPropBlock.SetBuffer(_ArgsBuffer_ID, m_instancesArgsBuffer);

				irm.shadowLod00MatPropBlock.SetBuffer(_ArgsBuffer_ID, m_shadowArgsBuffer);
				irm.shadowLod01MatPropBlock.SetBuffer(_ArgsBuffer_ID, m_shadowArgsBuffer);
				irm.shadowLod02MatPropBlock.SetBuffer(_ArgsBuffer_ID, m_shadowArgsBuffer);

				irm.lod00MatPropBlock.SetBuffer(_InstancesDrawMatrixRows01_ID, m_instancesCulledMatrixRows01);
				irm.lod01MatPropBlock.SetBuffer(_InstancesDrawMatrixRows01_ID, m_instancesCulledMatrixRows01);
				irm.lod02MatPropBlock.SetBuffer(_InstancesDrawMatrixRows01_ID, m_instancesCulledMatrixRows01);

				irm.lod00MatPropBlock.SetBuffer(_InstancesDrawMatrixRows23_ID, m_instancesCulledMatrixRows23);
				irm.lod01MatPropBlock.SetBuffer(_InstancesDrawMatrixRows23_ID, m_instancesCulledMatrixRows23);
				irm.lod02MatPropBlock.SetBuffer(_InstancesDrawMatrixRows23_ID, m_instancesCulledMatrixRows23);

				irm.lod00MatPropBlock.SetBuffer(_InstancesDrawMatrixRows45_ID, m_instancesCulledMatrixRows45);
				irm.lod01MatPropBlock.SetBuffer(_InstancesDrawMatrixRows45_ID, m_instancesCulledMatrixRows45);
				irm.lod02MatPropBlock.SetBuffer(_InstancesDrawMatrixRows45_ID, m_instancesCulledMatrixRows45);

				irm.shadowLod00MatPropBlock.SetBuffer(_InstancesDrawMatrixRows01_ID, m_shadowCulledMatrixRows01);
				irm.shadowLod01MatPropBlock.SetBuffer(_InstancesDrawMatrixRows01_ID, m_shadowCulledMatrixRows01);
				irm.shadowLod02MatPropBlock.SetBuffer(_InstancesDrawMatrixRows01_ID, m_shadowCulledMatrixRows01);

				irm.shadowLod00MatPropBlock.SetBuffer(_InstancesDrawMatrixRows23_ID, m_shadowCulledMatrixRows23);
				irm.shadowLod01MatPropBlock.SetBuffer(_InstancesDrawMatrixRows23_ID, m_shadowCulledMatrixRows23);
				irm.shadowLod02MatPropBlock.SetBuffer(_InstancesDrawMatrixRows23_ID, m_shadowCulledMatrixRows23);

				irm.shadowLod00MatPropBlock.SetBuffer(_InstancesDrawMatrixRows45_ID, m_shadowCulledMatrixRows45);
				irm.shadowLod01MatPropBlock.SetBuffer(_InstancesDrawMatrixRows45_ID, m_shadowCulledMatrixRows45);
				irm.shadowLod02MatPropBlock.SetBuffer(_InstancesDrawMatrixRows45_ID, m_shadowCulledMatrixRows45);
			}

			//-----------------------------------
			// InitializeDrawData
			//-----------------------------------

			ComputeBuffer positionsBuffer = new ComputeBuffer(m_numberOfInstances, Marshal.SizeOf(typeof(Vector3)),
				ComputeBufferType.Default);
			ComputeBuffer scaleBuffer = new ComputeBuffer(m_numberOfInstances, Marshal.SizeOf(typeof(Vector3)),
				ComputeBufferType.Default);
			ComputeBuffer rotationBuffer = new ComputeBuffer(m_numberOfInstances, Marshal.SizeOf(typeof(Vector3)),
				ComputeBufferType.Default);

			positionsBuffer.SetData(positions);
			scaleBuffer.SetData(scales);
			rotationBuffer.SetData(rotations);

			createDrawDataBufferCS.SetBuffer(m_createDrawDataBufferKernelID, _Positions_ID, positionsBuffer);
			createDrawDataBufferCS.SetBuffer(m_createDrawDataBufferKernelID, _Scales_ID, scaleBuffer);
			createDrawDataBufferCS.SetBuffer(m_createDrawDataBufferKernelID, _Rotations_ID, rotationBuffer);
			createDrawDataBufferCS.SetBuffer(m_createDrawDataBufferKernelID, _InstancesDrawMatrixRows01_ID,
				m_instancesMatrixRows01);
			createDrawDataBufferCS.SetBuffer(m_createDrawDataBufferKernelID, _InstancesDrawMatrixRows23_ID,
				m_instancesMatrixRows23);
			createDrawDataBufferCS.SetBuffer(m_createDrawDataBufferKernelID, _InstancesDrawMatrixRows45_ID,
				m_instancesMatrixRows45);
			int groupX = Mathf.Max(1, m_numberOfInstances / (2 * SCAN_THREAD_GROUP_SIZE));
			createDrawDataBufferCS.Dispatch(m_createDrawDataBufferKernelID, groupX, 1, 1);

			ReleaseComputeBuffer(ref positionsBuffer);
			ReleaseComputeBuffer(ref scaleBuffer);
			ReleaseComputeBuffer(ref rotationBuffer);


			//-----------------------------------
			// InitConstantComputeVariables
			//-----------------------------------

			m_occlusionGroupX = Mathf.Max(1, m_numberOfInstances / 64);
			m_scanInstancesGroupX = Mathf.Max(1, m_numberOfInstances / (2 * SCAN_THREAD_GROUP_SIZE));
			m_scanThreadGroupsGroupX = 1;
			m_copyInstanceDataGroupX = Mathf.Max(1, m_numberOfInstances / (2 * SCAN_THREAD_GROUP_SIZE));

			occlusionCS.SetInt(_ShouldFrustumCull_ID, enableFrustumCulling ? 1 : 0);
			occlusionCS.SetInt(_ShouldOcclusionCull_ID, enableOcclusionCulling ? 1 : 0);
			occlusionCS.SetInt(_ShouldDetailCull_ID, enableDetailCulling ? 1 : 0);
			occlusionCS.SetInt(_ShouldLOD_ID, enableLOD ? 1 : 0);
			occlusionCS.SetInt(_ShouldOnlyUseLOD02Shadows_ID, enableOnlyLOD02Shadows ? 1 : 0);
			occlusionCS.SetFloat(_ShadowDistance_ID, QualitySettings.shadowDistance);
			occlusionCS.SetFloat(_DetailCullingScreenPercentage_ID, detailCullingPercentage);
			occlusionCS.SetVector(_HiZTextureSize_ID, hiZBuffer.TextureSize);
			occlusionCS.SetTexture(m_occlusionKernelID, _HiZMap_ID, hiZBuffer.Texture);
			occlusionCS.SetBuffer(m_occlusionKernelID, _InstanceDataBuffer_ID, m_instanceDataBuffer);
			occlusionCS.SetBuffer(m_occlusionKernelID, _ArgsBuffer_ID, m_instancesArgsBuffer);
			occlusionCS.SetBuffer(m_occlusionKernelID, _ShadowArgsBuffer_ID, m_shadowArgsBuffer);
			occlusionCS.SetBuffer(m_occlusionKernelID, _IsVisibleBuffer_ID, m_instancesIsVisibleBuffer);
			occlusionCS.SetBuffer(m_occlusionKernelID, _ShadowIsVisibleBuffer_ID, m_shadowsIsVisibleBuffer);
			occlusionCS.SetBuffer(m_occlusionKernelID, _SortingData_ID, m_instancesSortingData);

			scanGroupSumsCS.SetInt(_NumOfGroups_ID, m_numberOfInstances / (2 * SCAN_THREAD_GROUP_SIZE));

			copyInstanceDataCS.SetInt(_NumOfDrawcalls_ID, m_numberOfInstanceTypes * NUMBER_OF_DRAW_CALLS);
			copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstanceDataBuffer_ID, m_instanceDataBuffer);
			copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancesDrawMatrixRows01_ID,
				m_instancesMatrixRows01);
			copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancesDrawMatrixRows23_ID,
				m_instancesMatrixRows23);
			copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _InstancesDrawMatrixRows45_ID,
				m_instancesMatrixRows45);
			copyInstanceDataCS.SetBuffer(m_copyInstanceDataKernelID, _SortingData_ID, m_instancesSortingData);

			CreateCommandBuffers();

			return true;
		}

		private void ReleaseBuffers()
		{
			ReleaseCommandBuffer(ref m_sortingCommandBuffer);
			//ReleaseCommandBuffer(ref visibleInstancesCB);

			ReleaseComputeBuffer(ref m_instancesIsVisibleBuffer);
			ReleaseComputeBuffer(ref m_instancesGroupSumArrayBuffer);
			ReleaseComputeBuffer(ref m_instancesScannedGroupSumBuffer);
			ReleaseComputeBuffer(ref m_instancesScannedPredicates);
			ReleaseComputeBuffer(ref m_instanceDataBuffer);
			ReleaseComputeBuffer(ref m_instancesSortingData);
			ReleaseComputeBuffer(ref m_instancesSortingDataTemp);
			ReleaseComputeBuffer(ref m_instancesMatrixRows01);
			ReleaseComputeBuffer(ref m_instancesMatrixRows23);
			ReleaseComputeBuffer(ref m_instancesMatrixRows45);
			ReleaseComputeBuffer(ref m_instancesCulledMatrixRows01);
			ReleaseComputeBuffer(ref m_instancesCulledMatrixRows23);
			ReleaseComputeBuffer(ref m_instancesCulledMatrixRows45);
			ReleaseComputeBuffer(ref m_instancesArgsBuffer);

			ReleaseComputeBuffer(ref m_shadowArgsBuffer);
			ReleaseComputeBuffer(ref m_shadowsIsVisibleBuffer);
			ReleaseComputeBuffer(ref m_shadowGroupSumArrayBuffer);
			ReleaseComputeBuffer(ref m_shadowsScannedGroupSumBuffer);
			ReleaseComputeBuffer(ref m_shadowScannedInstancePredicates);
			ReleaseComputeBuffer(ref m_shadowCulledMatrixRows01);
			ReleaseComputeBuffer(ref m_shadowCulledMatrixRows23);
			ReleaseComputeBuffer(ref m_shadowCulledMatrixRows45);
		}

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

		private void DrawInstances()
		{
			for (int i = 0; i < indirectMeshes.Length; i++)
			{
				// 3*lod * 5 * sizeof(uint) => 3 * 5 * 4(32Bit)
				int argsIndex = i * ARGS_BYTE_SIZE_PER_INSTANCE_TYPE;
				IndirectRenderingMesh irm = indirectMeshes[i];

				if (enableLOD)
				{
					Graphics.DrawMeshInstancedIndirect(irm.mesh, 0, irm.material, m_bounds, m_instancesArgsBuffer,
						argsIndex + ARGS_BYTE_SIZE_PER_DRAW_CALL * 0, irm.lod00MatPropBlock, ShadowCastingMode.Off);
					Graphics.DrawMeshInstancedIndirect(irm.mesh, 0, irm.material, m_bounds, m_instancesArgsBuffer,
						argsIndex + ARGS_BYTE_SIZE_PER_DRAW_CALL * 1, irm.lod01MatPropBlock, ShadowCastingMode.Off);
				}

				Graphics.DrawMeshInstancedIndirect(irm.mesh, 0, irm.material, m_bounds, m_instancesArgsBuffer,
					argsIndex + ARGS_BYTE_SIZE_PER_DRAW_CALL * 2, irm.lod02MatPropBlock, ShadowCastingMode.Off);
			}
		}

		private void DrawInstanceShadows()
		{
			for (int i = 0; i < indirectMeshes.Length; i++)
			{
				int argsIndex = i * ARGS_BYTE_SIZE_PER_INSTANCE_TYPE;
				IndirectRenderingMesh irm = indirectMeshes[i];

				if (!enableOnlyLOD02Shadows)
				{
					Graphics.DrawMeshInstancedIndirect(irm.mesh, 0, irm.material, m_bounds, m_shadowArgsBuffer,
						argsIndex + ARGS_BYTE_SIZE_PER_DRAW_CALL * 0, irm.shadowLod00MatPropBlock,
						ShadowCastingMode.ShadowsOnly);
					Graphics.DrawMeshInstancedIndirect(irm.mesh, 0, irm.material, m_bounds, m_shadowArgsBuffer,
						argsIndex + ARGS_BYTE_SIZE_PER_DRAW_CALL * 1, irm.shadowLod01MatPropBlock,
						ShadowCastingMode.ShadowsOnly);
				}

				Graphics.DrawMeshInstancedIndirect(irm.mesh, 0, irm.material, m_bounds, m_shadowArgsBuffer,
					argsIndex + ARGS_BYTE_SIZE_PER_DRAW_CALL * 2, irm.shadowLod02MatPropBlock,
					ShadowCastingMode.ShadowsOnly);
			}
		}

		/// <summary>
		/// 创建cmd 和 commandBuffer
		/// </summary>
		private void CreateCommandBuffers()
		{
			CreateSortingCommandBuffer();
		}

		/// <summary>
		/// 创建排序的cmd   并且对 sortData 进行排序
		/// </summary>
		private void CreateSortingCommandBuffer()
		{
			uint BITONIC_BLOCK_SIZE = 256;
			uint TRANSPOSE_BLOCK_SIZE = 8;

			// Determine parameters.
			uint NUM_ELEMENTS = (uint) m_numberOfInstances;
			uint MATRIX_WIDTH = BITONIC_BLOCK_SIZE;
			uint MATRIX_HEIGHT = (uint) NUM_ELEMENTS / BITONIC_BLOCK_SIZE;

			m_sortingCommandBuffer = new CommandBuffer {name = "AsyncGPUSorting"};

			//对数据进行排序
			//首先将<=级别的行 block size 排序
			for (uint level = 2; level <= BITONIC_BLOCK_SIZE; level <<= 1)
			{
				SetGPUSortConstants(ref m_sortingCommandBuffer, ref sortingCS, ref level, ref level, ref MATRIX_HEIGHT,
					ref MATRIX_WIDTH);

				// 排序行数据
				m_sortingCommandBuffer.SetComputeBufferParam(sortingCS, m_sortingCSKernelID, _Data_ID,
					m_instancesSortingData);
				m_sortingCommandBuffer.DispatchCompute(sortingCS, m_sortingCSKernelID,
					(int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);
			}

			// 然后按照每行的 block size  对行内数据进行排序
			// 转置 排序列     再转置 排序行
			for (uint level = (BITONIC_BLOCK_SIZE << 1); level <= NUM_ELEMENTS; level <<= 1)
			{
				//把 sortData 转换到 sortDataTemp
				uint l = (level / BITONIC_BLOCK_SIZE);
				uint lm = (level & ~NUM_ELEMENTS) / BITONIC_BLOCK_SIZE;
				SetGPUSortConstants(ref m_sortingCommandBuffer, ref sortingCS, ref l, ref lm, ref MATRIX_WIDTH,
					ref MATRIX_HEIGHT);
				m_sortingCommandBuffer.SetComputeBufferParam(sortingCS, m_sortingTransposeKernelID, _Input_ID,
					m_instancesSortingData);
				m_sortingCommandBuffer.SetComputeBufferParam(sortingCS, m_sortingTransposeKernelID, _Data_ID,
					m_instancesSortingDataTemp);
				m_sortingCommandBuffer.DispatchCompute(sortingCS, m_sortingTransposeKernelID,
					(int) (MATRIX_WIDTH / TRANSPOSE_BLOCK_SIZE), (int) (MATRIX_HEIGHT / TRANSPOSE_BLOCK_SIZE), 1);

				// 排序 转换的 sortDataTemp 的列数据
				m_sortingCommandBuffer.SetComputeBufferParam(sortingCS, m_sortingCSKernelID, _Data_ID,
					m_instancesSortingDataTemp);
				m_sortingCommandBuffer.DispatchCompute(sortingCS, m_sortingCSKernelID,
					(int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);

				// 再把 sortDataTemp 转换为 sortData
				SetGPUSortConstants(ref m_sortingCommandBuffer, ref sortingCS, ref BITONIC_BLOCK_SIZE, ref level,
					ref MATRIX_HEIGHT, ref MATRIX_WIDTH);
				m_sortingCommandBuffer.SetComputeBufferParam(sortingCS, m_sortingTransposeKernelID, _Input_ID,
					m_instancesSortingDataTemp);
				m_sortingCommandBuffer.SetComputeBufferParam(sortingCS, m_sortingTransposeKernelID, _Data_ID,
					m_instancesSortingData);
				m_sortingCommandBuffer.DispatchCompute(sortingCS, m_sortingTransposeKernelID,
					(int) (MATRIX_HEIGHT / TRANSPOSE_BLOCK_SIZE), (int) (MATRIX_WIDTH / TRANSPOSE_BLOCK_SIZE), 1);

				// 再把行数据排序
				m_sortingCommandBuffer.SetComputeBufferParam(sortingCS, m_sortingCSKernelID, _Data_ID,
					m_instancesSortingData);
				m_sortingCommandBuffer.DispatchCompute(sortingCS, m_sortingCSKernelID,
					(int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);
			}
		}

		/// <summary>
		/// 设置排序的变量
		/// </summary>
		private void SetGPUSortConstants(ref CommandBuffer commandBuffer, ref ComputeShader cs, ref uint level,
			ref uint levelMask, ref uint width, ref uint height)
		{
			commandBuffer.SetComputeIntParam(cs, _Level_ID, (int) level);
			commandBuffer.SetComputeIntParam(cs, _LevelMask_ID, (int) levelMask);
			commandBuffer.SetComputeIntParam(cs, _Width_ID, (int) width);
			commandBuffer.SetComputeIntParam(cs, _Height_ID, (int) height);
		}

		private bool TryGetKernels()
		{
			return TryGetKernel("CSMain", ref createDrawDataBufferCS, ref m_createDrawDataBufferKernelID)
			       && TryGetKernel("BitonicSort", ref sortingCS, ref m_sortingCSKernelID)
			       && TryGetKernel("MatrixTranspose", ref sortingCS, ref m_sortingTransposeKernelID)
			       && TryGetKernel("CSMain", ref occlusionCS, ref m_occlusionKernelID)
			       && TryGetKernel("CSMain", ref scanInstancesCS, ref m_scanInstancesKernelID)
			       && TryGetKernel("CSMain", ref scanGroupSumsCS, ref m_scanGroupSumsKernelID)
			       && TryGetKernel("CSMain", ref copyInstanceDataCS, ref m_copyInstanceDataKernelID);
		}

		private Bounds CalculateBounds(GameObject _prefab)
		{
			GameObject obj = Instantiate(_prefab);
			obj.transform.position = Vector3.zero;
			obj.transform.rotation = Quaternion.Euler(Vector3.zero);
			obj.transform.localScale = Vector3.one;
			Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
			Bounds b = new Bounds();
			if (rends.Length > 0)
			{
				b = new Bounds(rends[0].bounds.center, rends[0].bounds.size);
				for (int r = 1; r < rends.Length; r++)
				{
					//update min max
					b.Encapsulate(rends[r].bounds);
				}
			}

			b.center = Vector3.zero; //只要extends  不要中心位置
			DestroyImmediate(obj);

			return b;
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
			else if (m_debugGPUArgsRequest.done && m_debugGPUShadowArgsRequest.done)
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

					uint lod00Vertices = irm.numOfVerticesLod00 * normalMultiplier;
					uint lod01Vertices = irm.numOfVerticesLod01 * normalMultiplier;
					uint lod02Vertices = irm.numOfVerticesLod02 * normalMultiplier;

					uint shadowLod00Vertices = irm.numOfVerticesLod00 * shadowMultiplier;
					uint shadowLod01Vertices = irm.numOfVerticesLod01 * shadowMultiplier;
					uint shadowLod02Vertices = irm.numOfVerticesLod02 * shadowMultiplier;

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
			if (!string.IsNullOrEmpty(prefix))
			{
				sb.AppendLine(prefix);
			}

			uint lastDrawCallIndex = 0;
			for (int i = 0; i < sortingData.Length; i++)
			{
				uint drawCallIndex = (sortingData[i].drawCallInstanceIndex >> 16);
				uint instanceIndex = (sortingData[i].drawCallInstanceIndex) & 0xFFFF;
				if (i == 0)
				{
					lastDrawCallIndex = drawCallIndex;
				}

				sb.AppendLine("(" + drawCallIndex + ") --> " + sortingData[i].distanceToCam + " instanceIndex:" +
				              instanceIndex);

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