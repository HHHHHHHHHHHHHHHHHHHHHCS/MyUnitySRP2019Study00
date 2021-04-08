using System.Linq;
using MyRenderPipeline.RenderPass.Common;
using MyRenderPipeline.Utility;
using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace MyRenderPipeline.RenderPass.Boid
{
	[ImageEffectAllowedInSceneView]
	public class BoidPass : MyUserPass
	{
		struct EntityData
		{
			//一组的长度
			public static int Size => sizeof(float) * 3 * 3 + sizeof(float) * 4 * 4;

			public Vector3 position;
			public Vector3 velocity;
			public Vector3 up;
			public Matrix4x4 rotation;
		}

		public const int ComputeThreads = 1024;
		public const int KernelBoid = 0;

		public ComputeShader computeShader;
		public Material material;
		public Mesh mesh;

		public Transform spawnPoint;
		public Transform targetPoint;

		[Delayed] public int count = 1024;
		public float distributeRadius = 10;
		public float maxSpeed = 5;
		public float minSpeed = 1;
		public Vector3 angularLimit = new Vector3(.1f, .1f, .1f);
		public float accelerationLimit = .5f;
		public bool forceTarget = false;

		/// <summary>
		/// 感知半径
		/// </summary>
		public float sensoryRadius = 3;

		/// <summary>
		/// 结盟
		/// </summary>
		[Range(0, 10)] public float alignment = 1;

		/// <summary>
		/// 分离
		/// </summary>
		[Range(0, 10)] public float seperation = 1;

		/// <summary>
		/// 凝聚
		/// </summary>
		[Range(0, 10)] public float cohesion = 1;


		private bool needUpdate = true;


		private DoubleBuffer<ComputeBuffer> boidBuffer;
		private ComputeBuffer argsBuffer;
		private uint[] args = new uint[5];

		#if UNITY_EDITOR
		[EditorButton]
		public void Reload()
		{
			FindObjectsOfType<BoidPass>().ForEach(renderer => { renderer.needUpdate = true; });
			UnityEditor.SceneView.GetAllSceneCameras().Select(camera => camera.GetComponent<BoidPass>())
				.ForEach(renderer => { renderer.needUpdate = true; });
		}
		#endif
		
		public override void Setup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			if (needUpdate)
			{
				if (!computeShader || !material || !mesh)
				{
					return;
				}

				if (boidBuffer != null)
				{
					boidBuffer.Current.Release();
					boidBuffer.Next.Release();
				}

				if (argsBuffer != null)
				{
					argsBuffer.Release();
				}

				boidBuffer = new DoubleBuffer<ComputeBuffer>((i) => new ComputeBuffer(count, EntityData.Size));
				var data = new EntityData[count];
				for (var i = 0; i < count; i++)
				{
					data[i] = new EntityData()
					{
						position = Random.insideUnitSphere * distributeRadius + spawnPoint.position,
						velocity = Random.insideUnitSphere,
					};
					data[i].velocity = data[i].velocity.normalized *
					                   (data[i].velocity.magnitude * (maxSpeed - minSpeed) + minSpeed);
					var up = Random.onUnitSphere;
					var right = Vector3.Cross(data[i].velocity, up);
					if (Mathf.Approximately(right.magnitude, 0))
					{
						var v = data[i].velocity;
						float x = Mathf.Abs(v.x);
						float y = Mathf.Abs(v.y);
						float z = Mathf.Abs(v.z);

						if (x < y && x < z)
						{
							right = Vector3.right;
						}
						else if (y < x && y < z)
						{
							right = Vector3.up;
						}
						else
						{
							right = Vector3.forward;
						}
					}

					up = Vector3.Cross(right, data[i].velocity);
					data[i].up = up.normalized;
				}

				boidBuffer.Current.SetData(data);
				boidBuffer.Next.SetData(data);

				//要绘制多少个实例的参数来自bufferWithArgs
				//Buffer with arguments, bufferWithArgs, has to have five integer numbers at given argsOffset offset: index count per instance, instance count, start index location, base vertex location, start instance location.
				//2是 ebo   3 是 vbo
				//带参数的bufferWithArgs，在给定的argsOffset偏移量处必须有五个整数：每个实例的索引计数、实例计数、起始索引位置、基顶点位置、开始实例的偏移。
				args[0] = mesh.GetIndexCount(0);
				args[1] = (uint) count;
				args[2] = mesh.GetIndexStart(0);
				args[3] = mesh.GetBaseVertex(0);
				argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
				argsBuffer.SetData(args);

				needUpdate = false;
			}
		}

		public override void Render(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			if (boidBuffer == null)
			{
				needUpdate = true;
			}

			if (needUpdate)
			{
				return;
			}

			boidBuffer.Flip();

			var cmd = CommandBufferPool.Get("Boid");

			using (new ProfilingSample(cmd, "Boid"))
			{
				cmd.BeginSample("Boid Compute");
				cmd.SetComputeIntParam(computeShader, "TotalSize", count);
				cmd.SetComputeFloatParam(computeShader, "SensoryRadius", sensoryRadius);
				cmd.SetComputeFloatParam(computeShader, "AlignmentFactor", alignment);
				cmd.SetComputeFloatParam(computeShader, "SeprationFactor", seperation);
				cmd.SetComputeFloatParam(computeShader, "CohesionFactor", cohesion);
				cmd.SetComputeFloatParam(computeShader, "DeltaTime", Time.deltaTime);
				cmd.SetComputeVectorParam(computeShader, "SpeedLimit", new Vector2(minSpeed, maxSpeed));
				cmd.SetComputeVectorParam(computeShader, "AngularSpeedLimit", angularLimit);
				cmd.SetComputeFloatParam(computeShader, "AccelerationLimit", accelerationLimit);
				cmd.SetComputeVectorParam(computeShader, "Target",
					targetPoint.transform.position.ToVector4(forceTarget ? 1 : 0));
				cmd.SetComputeBufferParam(computeShader, KernelBoid, "InputBuffer", boidBuffer.Current);
				cmd.SetComputeBufferParam(computeShader, KernelBoid, "OutputBuffer", boidBuffer.Next);
				cmd.DispatchCompute(computeShader, KernelBoid, Mathf.CeilToInt(count / ComputeThreads), 1, 1);
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();
				cmd.EndSample("Boid Compute");

				cmd.BeginSample("Boid Rendering");
				var light = GetMainLight(renderingData);
				if (light.light)
				{
					cmd.SetGlobalVector("_MainLightPosition", light.light.transform.forward.ToVector4(0.0f));
					cmd.SetGlobalColor("_MainLightColor", light.finalColor);
				}

				cmd.SetGlobalColor("_AmbientLight", RenderSettings.ambientLight);
				cmd.SetGlobalVector("_WorldCameraPos", renderingData.camera.transform.position);
				cmd.SetGlobalBuffer("boidBuffer", boidBuffer.Next);
				//https://docs.unity3d.com/ScriptReference/Graphics.DrawMeshInstancedIndirect.html
				//argsBuffer GPU缓冲区包含要绘制多少网格实例的参数。
				//请使用此函数。网格不会被视图视锥体或烘焙遮挡器进一步剔除，也不会为透明度或z效率进行排序
				cmd.DrawMeshInstancedIndirect(mesh, 0, material, 0, argsBuffer);
				
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();
				cmd.EndSample("Boid Rendering");
			}
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}

		private VisibleLight GetMainLight(MyRenderingData renderingData)
		{
			var lights = renderingData.cullResults.visibleLights;
			var sun = RenderSettings.sun;
			if (sun == null)
			{
				return default;
			}

			for (var i = 0; i < lights.Length; i++)
			{
				if (lights[i].light == sun)
					return lights[i];
			}

			return default;
		}
	}
}