using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

//see this for ref: https://docs.unity3d.com/ScriptReference/Graphics.DrawMeshInstancedIndirect.html
[ExecuteAlways]
public class GrassRenderer : MonoBehaviour
{
	public static GrassRenderer instance;

	[Range(1, 1000000)] public int instanceCount = 50000;
	public float drawDistance = 125;
	public Material instanceMaterial;

	private int cachedInstanceCount = -1;
	private Mesh cachedGrassMesh;
	private ComputeBuffer transformBigBuffer;
	private ComputeBuffer argsBuffer;


	private void Awake()
	{
		instance = this;
	}

	private void LateUpdate()
	{
		if (instanceMaterial == null)
		{
			return;
		}

		UpdateBuffersIfNeeded();

		//是不会被剔除的
		Graphics.DrawMeshInstancedIndirect(GetGrassMeshCache(), 0, instanceMaterial,
			new Bounds(transform.position, transform.localScale * 2), argsBuffer);
	}

	private void OnDisable()
	{
		//release all compute buffers
		if (transformBigBuffer != null)
			transformBigBuffer.Release();
		transformBigBuffer = null;

		if (argsBuffer != null)
			argsBuffer.Release();
		argsBuffer = null;
	}

	private void OnGUI()
	{
		GUI.Label(new Rect(300, 50, 200, 30), "Instance Count: " + instanceCount.ToString());
		instanceCount = Mathf.Max(1,
			(int) (GUI.HorizontalSlider(new Rect(300, 100, 200, 30), instanceCount / 10000f, 0, 100)) * 10000);

		float scale = Mathf.Sqrt((instanceCount / 4)) / 2f;
		transform.localScale = new Vector3(scale, transform.localScale.y, scale);

		GUI.Label(new Rect(300, 150, 200, 30), "Draw Distance: " + drawDistance);
		drawDistance = Mathf.Max(1,
			(int) (GUI.HorizontalSlider(new Rect(300, 200, 200, 30), drawDistance / 25f, 1, 8)) * 25);
	}

	private Mesh GetGrassMeshCache()
	{
		if (!cachedGrassMesh)
		{
			cachedGrassMesh = new Mesh();

			Vector3[] verts = new Vector3[3];
			verts[0] = new Vector3(-0.25f, 0, 0);
			verts[1] = new Vector3(+0.25f, 0, 0);
			verts[2] = new Vector3(0.0f, 1, 0);

			int[] triangles = new int[3] {2, 1, 0};

			cachedGrassMesh.SetVertices(verts);
			cachedGrassMesh.SetTriangles(triangles, 0);
		}

		return cachedGrassMesh;
	}

	private void UpdateBuffersIfNeeded()
	{
		instanceMaterial.SetVector("_PivotPosWS", transform.position);
		instanceMaterial.SetVector("_BoundSize", new Vector2(transform.localScale.x, transform.localScale.z));
		instanceMaterial.SetFloat("_DrawDistance", drawDistance);

 
		if (cachedInstanceCount == instanceCount &&
		    argsBuffer != null &&
		    transformBigBuffer != null)
		{
			return;
		}

		//Transform buffer
		//----------------------------
		if (transformBigBuffer != null)
			transformBigBuffer.Release();
		Vector4[] positions = new Vector4[instanceCount];
		transformBigBuffer = new ComputeBuffer(positions.Length, sizeof(float) * 4);

		Random.InitState(123);

		for (int i = 0; i < instanceCount; i++)
		{
			Vector3 pos = Vector3.zero;

			//这里只是用随机的生成的
			pos.x = Random.Range(-1f, 1f);
			pos.z = Random.Range(-1f, 1f);

			//TODO:旋转  也可以允许物体的旋转影响草

			float size = Random.Range(2f, 5f);

			//y 是 0
			positions[i] = new Vector4(pos.x, pos.y, pos.z, size);
		}

		transformBigBuffer.SetData(positions);
		instanceMaterial.SetBuffer("_TransformBuffer", transformBigBuffer);

		//indirect args buffer
		//--------------------------
		if (argsBuffer != null)
			argsBuffer.Release();

		uint[] args = new uint[5] {0, 0, 0, 0, 0};
		argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

		var mesh = GetGrassMeshCache();

		args[0] = (uint) mesh.GetIndexCount(0);
		args[1] = (uint) instanceCount;
		args[2] = (uint) mesh.GetIndexStart(0);
		args[3] = (uint) mesh.GetBaseVertex(0);
		args[4] = (uint) 0;


		argsBuffer.SetData(args);

		cachedInstanceCount = instanceCount;
	}
}