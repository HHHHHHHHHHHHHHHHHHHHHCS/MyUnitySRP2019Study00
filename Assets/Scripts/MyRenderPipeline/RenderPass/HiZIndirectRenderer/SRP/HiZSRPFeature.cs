using System;
using MyRenderPipeline.RenderPass.HiZIndirectRenderer.Builtin;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer.SRP
{
	public class HiZSRPFeature : ScriptableRendererFeature
	{
		public bool enabled = true;

		private HiZSRPRenderPass hizIndirectRenderPass;
		private HiZSRPDepthPass hizDepthPass;

		public HiZSRPInstanceDataScriptableObject hiZSRPInstanceDataScriptableObject;
		public HiZSRPRenderScriptableObject hiZSRPRenderScriptableObject;


		private bool lastIndirectRenderingEnabled = false;
		private bool lastIndirectDrawShadows = false;

		private GameObject normalInstancesParent;


		#region MonoBehaviour

		public override void Create()
		{
			if (!Application.isPlaying)
			{
				return;
			}

			hizIndirectRenderPass = new HiZSRPRenderPass();
			hizIndirectRenderPass.Init(hiZSRPRenderScriptableObject);
			hizIndirectRenderPass.renderPassEvent =
				RenderPassEvent.BeforeRenderingShadows;

			hizDepthPass = new HiZSRPDepthPass();
			hizDepthPass.renderPassEvent = RenderPassEvent.AfterRendering;

			HiZDataAwake();
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (!Application.isPlaying)
			{
				return;
			}

			if (!enabled)
			{
				return;
			}

			//TODO:
			// if (!renderingData.cameraData.camera.CompareTag("MainCamera"))
			// {
			// 	return;
			// }

			renderer.EnqueuePass(hizIndirectRenderPass);
			renderer.EnqueuePass(hizDepthPass);

			HiZDataUpdate();
		}

		#endregion

		#region Private Function

		private void HiZDataAwake()
		{
			if (!hiZSRPInstanceDataScriptableObject.AssertInstanceData())
			{
				enabled = false;
				return;
			}

			if (hiZSRPInstanceDataScriptableObject.createInstancesOnAwake)
			{
				hiZSRPInstanceDataScriptableObject.CreateInstancesData();
			}

			lastIndirectRenderingEnabled = hiZSRPInstanceDataScriptableObject.indirectRenderingEnabled;
			lastIndirectDrawShadows = hizIndirectRenderPass.data.drawInstanceShadows;

			if (hiZSRPInstanceDataScriptableObject.shouldInstantiatePrefabs)
			{
				InstantiateInstance();
			}

			hizIndirectRenderPass.Initialize(ref hiZSRPInstanceDataScriptableObject.instances);
			hizIndirectRenderPass.StartDrawing();
		}

		private void HiZDataUpdate()
		{
			if (lastIndirectRenderingEnabled != hiZSRPInstanceDataScriptableObject.indirectRenderingEnabled)
			{
				lastIndirectRenderingEnabled = hiZSRPInstanceDataScriptableObject.indirectRenderingEnabled;

				if (normalInstancesParent != null)
				{
					normalInstancesParent.SetActive(!hiZSRPInstanceDataScriptableObject.indirectRenderingEnabled);
				}

				if (hiZSRPInstanceDataScriptableObject.indirectRenderingEnabled)
				{
					hizIndirectRenderPass.Initialize(ref hiZSRPInstanceDataScriptableObject.instances);
					hizIndirectRenderPass.StartDrawing();
				}
				else
				{
					hizIndirectRenderPass.StopDrawing(true);
				}
			}

			if (lastIndirectDrawShadows != hizIndirectRenderPass.data.drawInstanceShadows)
			{
				lastIndirectDrawShadows = hizIndirectRenderPass.data.drawInstanceShadows;

				if (normalInstancesParent != null)
				{
					SetShadowCastingMode(lastIndirectDrawShadows ? ShadowCastingMode.On : ShadowCastingMode.Off);
				}
			}
		}

		private void InstantiateInstance()
		{
			Profiler.BeginSample("InstantiateInstance");

			//如果之前存在
			if (normalInstancesParent != null)
			{
				Destroy(normalInstancesParent);
			}

			normalInstancesParent = new GameObject("InstancesParent");
			normalInstancesParent.SetActive(!hiZSRPInstanceDataScriptableObject.indirectRenderingEnabled);

			Profiler.BeginSample("for instance.Count");
			for (int i = 0; i < hiZSRPInstanceDataScriptableObject.instances.Length; i++)
			{
				HiZSRPInstanceData instanceData = hiZSRPInstanceDataScriptableObject.instances[i];

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

		private void SetShadowCastingMode(ShadowCastingMode newMode)
		{
			Renderer[] rends = normalInstancesParent.GetComponentsInChildren<Renderer>();
			for (int i = 0; i < rends.Length; i++)
			{
				rends[i].shadowCastingMode = newMode;
			}
		}

		#endregion
	}
}