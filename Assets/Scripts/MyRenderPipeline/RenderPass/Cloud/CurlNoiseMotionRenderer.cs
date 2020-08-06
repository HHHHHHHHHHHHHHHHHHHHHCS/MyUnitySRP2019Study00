using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.Cloud
{
	public class CurlNoiseMotionRenderer
	{
		public Texture curlNoise;
		public ComputeShader computeShader;
		public Vector3Int size;

		private ComputeBuffer[] buffers;
		private int currentUse = 0;

		private ComputeBuffer currentBuffer => buffers[currentUse % 2];
		private ComputeBuffer nextBuffer => buffers[(currentUse + 1) % 2];

		public CurlNoiseMotionRenderer(Texture _curlNoise, ComputeShader _computeShader, Vector3Int _size)
		{
			curlNoise = _curlNoise;
			computeShader = _computeShader;
			size = _size;
			buffers = new ComputeBuffer[2]
            {
                new ComputeBuffer(size.x*size.y*size.z, 3 * 4), // sizeof(float3)
                new ComputeBuffer(size.x*size.y*size.z, 3 * 4),
            };
			var arr = new Vector3[size.x * size.y * size.z];

			for (long i = 0; i < arr.LongLength; i++)
			{
				var z = i / (size.x * size.y);
				var k = i % (size.x * size.y);
				var y = k / size.x;
				var x = k % size.x;
				var pos = new Vector3((float) x / size.x, (float) y / size.y, (float) z / size.z); //[0,1]
				arr[i] = pos;
			}

			buffers[0].SetData(arr);
			buffers[1].SetData(arr);
		}

		public ComputeBuffer Update(CommandBuffer cmd)
		{
			currentUse++;
			cmd.SetComputeBufferParam(computeShader,0,"CurrentBuffer",currentBuffer);
			cmd.SetComputeBufferParam(computeShader,0,"NextBuffer", nextBuffer);
			return nextBuffer;
		}
	}
}