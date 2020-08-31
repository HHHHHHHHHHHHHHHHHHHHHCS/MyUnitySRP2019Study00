using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer.SRP
{
	[CreateAssetMenu(menuName="MyRP/HiZ/Create HiZSRPRenderScriptableObject")]
	public class HiZSRPRenderScriptableObject : ScriptableObject
	{
		[Header("Settings")] public bool runCompute = true;
		public bool drawInstances = true;
		public bool drawInstanceShadows = true;
		public bool enableFrustumCulling = true;
		public bool enableOcclusionCulling = true;
		public bool enableDetailCulling = true;
		public bool enableLOD = true;
		public bool enableOnlyLOD02Shadows = true;
		[Range(00.00f, 00.02f)] public float detailCullingPercentage = 0.005f;
		public bool usePreCulling = true;
		
		// Debugging Variables
		[Header("Debug")] public bool debugShowUI;
		public bool debugDrawLOD;
		public bool debugDrawBoundsInSceneView;
		public bool debugDrawHiZ;
		[Range(0, 10)] public int debugHiZLOD;
		public GameObject debugUIPrefab;


		[Header("Logging")] public bool logInstanceDrawMatrices = false;
		public bool logArgumentsAfterReset = false;
		public bool logSortingData = false;
		public bool logPreCullingArgumentsAfterOcclusion = false;
		public bool logPreCullingInstancesIsVisibleBuffer = false;
		public bool logArgumentsAfterOcclusion = false;
		public bool logInstancesIsVisibleBuffer = false;
		public bool logScannedPredicates = false;
		public bool logGroupSumArrayBuffer = false;
		public bool logScannedGroupSumsBuffer = false;
		public bool logArgsBufferAfterCopy = false;
		public bool logCulledInstancesDrawMatrices = false;

		[Header("References")] public ComputeShader createDrawDataBufferCS;
		public ComputeShader sortingCS;
		public ComputeShader preCullingCS;
		public ComputeShader occlusionCS;
		public ComputeShader scanInstancesCS;
		public ComputeShader scanGroupSumsCS;
		public ComputeShader copyInstanceDataCS;
	}
}