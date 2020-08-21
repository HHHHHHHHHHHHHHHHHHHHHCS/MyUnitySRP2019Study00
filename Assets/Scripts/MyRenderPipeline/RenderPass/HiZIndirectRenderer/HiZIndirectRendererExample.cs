using System;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer
{
	public class HiZIndirectRendererExample : MonoBehaviour
	{
		[System.Serializable]
		public enum NumberOfInstances
		{
			_2048 = 2048,
			_4096 = 4096,
			_8192 = 8192,
			_16384 = 16384,
			_32768 = 32768,
			_65536 = 65536,
			_131072 = 131072,
			_262144 = 262144
		}

		#region Variables

		//public
		//-----------------------
		public bool indirectRenderingEnabled = true;

		/// <summary>
		/// 初始化的时候 就初始化数据
		/// </summary>
		public bool createInstancesOnAwake = false;

		/// <summary>
		/// 是否实例化
		/// </summary>
		public bool shouldInstantiatePrefabs = false;

		/// <summary>
		/// 区域大小
		/// </summary>
		public float areaSize = 5000f;

		/// <summary>
		/// 生成多少
		/// </summary>
		public NumberOfInstances numberOfInstances;

		public HiZIndirectRenderer indirectRenderer;
		
		/// <summary>
		/// 实例化的数据
		/// </summary>
		public HiZIndirectInstanceData[] instances;


		private bool lastIndirectRenderingEnabled = false;
		private bool lastIndirectDrawShadows = false;
		private GameObject normalInstancesParent;

		#endregion

		#region MonoBehaviour

		private void Awake()
		{
			if (!AssertInstanceData())
			{
				enabled = false;
				return;
			}

			if (createInstancesOnAwake)
			{
				CreateInstancesData();
			}

			lastIndirectRenderingEnabled = indirectRenderingEnabled;
			lastIndirectDrawShadows = indirectRenderer.drawInstanceShadows;

			if (shouldInstantiatePrefabs)
			{
				InstantiateInstance();
			}
			
			indirectRenderer.Initialize(ref instances);
			indirectRenderer.StartDrawing();
		}

		private void Update()
		{
			if (lastIndirectRenderingEnabled != indirectRenderingEnabled)
			{
				lastIndirectRenderingEnabled = indirectRenderingEnabled;

				if (normalInstancesParent != null)
				{
					normalInstancesParent.SetActive(!indirectRenderingEnabled);
				}

				if (indirectRenderingEnabled)
				{
					indirectRenderer.Initialize(ref instances);
					indirectRenderer.StartDrawing();
				}
				else
				{
					indirectRenderer.StopDrawing(true);
				}
			}
			
			if (lastIndirectDrawShadows != indirectRenderer.drawInstanceShadows)
			{
				lastIndirectDrawShadows = indirectRenderer.drawInstanceShadows;
            
				if (normalInstancesParent != null)
				{
					SetShadowCastingMode(lastIndirectDrawShadows ? ShadowCastingMode.On : ShadowCastingMode.Off);
				}
			}
		}

		#endregion


		#region Private Function

		
		[ContextMenu("CreateInstancesData()")]
		private void CreateInstancesData()
		{
			if (instances.Length == 0)
			{
				Debug.LogError("Instances list is empty!", this);
				return;
			}


			int numOfInstancesPerType = ((int) numberOfInstances) / instances.Length;
			int instanceCounter = 0;
			for (int i = 0; i < instances.Length; i++)
			{
				var inst = instances[i];

				inst.positions = new Vector3[numOfInstancesPerType];
				inst.rotations = new Vector3[numOfInstancesPerType];
				inst.scales = new Vector3[numOfInstancesPerType];


				Vector2 L = Vector2.one * i;
				for (int k = 0; k < numOfInstancesPerType; k++)
				{
					Vector3 rotation = Vector3.zero;
					Vector3 scale = Vector3.one
					                * UnityEngine.Random.Range(inst.scaleRange.x, inst.scaleRange.y);
					Vector3 pos = NoisePos(L, instanceCounter++) * areaSize;
					pos = new Vector3(pos.x - areaSize * 0.5f, 0f, pos.y - areaSize * 0.5f) + inst.positionOffset;

					inst.positions[k] = pos;
					inst.rotations[k] = rotation;
					inst.scales[k] = scale;
				}
			}
		}

		private void InstantiateInstance()
		{
			Profiler.BeginSample("InstantiateInstance");

			normalInstancesParent = new GameObject("InstancesParent");
			normalInstancesParent.SetActive(!indirectRenderingEnabled);

			Profiler.BeginSample("for instance.Count");
			for (int i = 0; i < instances.Length; i++)
			{
				HiZIndirectInstanceData instanceData = instances[i];

				GameObject parentObj = new GameObject(instanceData.prefab.name);
				parentObj.transform.parent = normalInstancesParent.transform;

				Profiler.BeginSample("for instance.positions.Length ...");

				for (int j = 0; j < instanceData.positions.Length; j++)
				{
					GameObject obj = Instantiate(instanceData.prefab, parentObj.transform, true);
					obj.transform.position = instanceData.positions[j];
					obj.transform.localScale = instanceData.scales[j];
					obj.transform.rotation = Quaternion.Euler(instanceData.rotations[i]);
				}

				Profiler.EndSample();
			}

			Profiler.EndSample();

			Profiler.EndSample();
		}
		
		private bool AssertInstanceData()
		{
			for (int i = 0; i < instances.Length; i++)
			{
				if (instances[i].prefab == null)
				{
					Debug.LogError("Missing Prefab on instance at index: " + i + "! Aborting.");
					return false;
				}
				
				if (instances[i].indirectMaterial == null)
				{
					Debug.LogError("Missing indirectMaterial on instance at index: " + i + "! Aborting.");
					return false;
				}

				if (instances[i].lod00Mesh == null)
				{
					Debug.LogError("Missing lod00Mesh on instance at index: " + i + "! Aborting.");
					return false;
				}

				if (instances[i].lod01Mesh == null)
				{
					Debug.LogError("Missing lod01Mesh on instance at index: " + i + "! Aborting.");
					return false;
				}

				if (instances[i].lod02Mesh == null)
				{
					Debug.LogError("Missing lod02Mesh on instance at index: " + i + "! Aborting.");
					return false;
				}
			}

			return true;
		}

		private void SetShadowCastingMode(ShadowCastingMode newMode)
		{
			Renderer[] rends = normalInstancesParent.GetComponentsInChildren<Renderer>();
			for (int i = 0; i < rends.Length; i++)
			{
				rends[i].shadowCastingMode = newMode;
			}
		}

		// Taken from:
		// http://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/
		// https://www.shadertoy.com/view/4dtBWH
		private Vector2 NoisePos(Vector2 p0, float n)
		{
			Vector2 res = p0 + n * new Vector2(0.754877669f, 0.569840296f);
			res.x %= 1;
			res.y %= 1;
			return res;
		}

		#endregion
	}
}