using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class InstanceCulling : MonoBehaviour
{
	public enum RendererType
	{
		Default,
		Instance,
		DrawMeshInstanced,
		DrawMeshInstancedIndirect,
	}

	public int count = 30000;

	public Mesh mesh;
	public Material defaultMaterial;
	public Material instanceMaterial;
	public Material indirectMaterial;

	public RendererType rendererType;

	private List<Matrix4x4> objMatrix;

	private ComputeBuffer matrixsBuffer;
	private ComputeBuffer argsBuffer;
	private Bounds bounds;

	private void Awake()
	{
		Init();
	}

	private void Init()
	{
		if (rendererType == RendererType.Default
		    || rendererType == RendererType.Instance)
		{
			// var camera = Camera.main;

			var parent = new GameObject("Parent");

			var mat = rendererType == RendererType.Default ? defaultMaterial : instanceMaterial;

			for (var i = 0; i < count; i++)
			{
				var pos = Random.insideUnitSphere * 50f;
				var go = new GameObject("instance:" + i);
				go.transform.SetParent(parent.transform);
				go.transform.localPosition = pos;
				// go.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
				go.AddComponent<MeshFilter>().mesh = mesh;
				go.AddComponent<MeshRenderer>().material = mat;
			}
		}
		else if (rendererType == RendererType.DrawMeshInstanced
		         || rendererType == RendererType.DrawMeshInstancedIndirect)
		{
			objMatrix = new List<Matrix4x4>(count);
			for (int i = 0; i < count; i++)
			{
				objMatrix.Add(Matrix4x4.Translate(Random.insideUnitSphere * 50f));
			}


			if (rendererType == RendererType.DrawMeshInstancedIndirect)
			{
				//https://docs.unity3d.com/ScriptReference/Graphics.DrawMeshInstancedIndirect.html
				//argsBuffer GPU缓冲区包含要绘制多少网格实例的参数。
				//请使用此函数。网格不会被视图视锥体或烘焙遮挡器进一步剔除，也不会为透明度或z效率进行排序

				uint[] args = new uint[5];
				argsBuffer = new ComputeBuffer(count, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

				args[0] = (uint) mesh.GetIndexCount(0);
				args[1] = (uint) count;
				args[2] = (uint) mesh.GetIndexStart(0);
				args[3] = (uint) mesh.GetBaseVertex(0);
				args[4] = 0; //开始实例的偏移

				argsBuffer.SetData(args);


				matrixsBuffer = new ComputeBuffer(count, sizeof(float) * 16);
				matrixsBuffer.SetData(objMatrix);
				indirectMaterial.SetBuffer("_MatrixsBuffer", matrixsBuffer);
				indirectMaterial.EnableKeyword("_Indirect");

				//aabb
				bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
			}
		}
	}

	private void Update()
	{
		if (objMatrix == null)
		{
			return;
		}


		if (rendererType == RendererType.DrawMeshInstanced)
		{
			//1023是instance 的限制
			//然后根据平台instance buffer限制再通过多个drawcall去细分
			//但是不会再有culling
			for (int i = 0; i < count / 1023; i++)
			{
				Graphics.DrawMeshInstanced(mesh, 0, instanceMaterial, objMatrix.GetRange(i * 1023, 1023));
			}

			int c = count % 1023;
			if (c > 0)
			{
				Graphics.DrawMeshInstanced(mesh, 0, instanceMaterial, objMatrix.GetRange(objMatrix.Count - c, c));
			}
		}
		else if (rendererType == RendererType.DrawMeshInstancedIndirect && argsBuffer != null)
		{
			//但是不会再有culling
			Graphics.DrawMeshInstancedIndirect(mesh, 0, indirectMaterial, bounds, argsBuffer);
		}
	}
}