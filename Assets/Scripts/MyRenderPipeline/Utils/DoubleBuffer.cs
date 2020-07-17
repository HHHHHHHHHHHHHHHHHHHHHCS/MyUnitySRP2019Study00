using System;

namespace MyRenderPipeline.Utils
{
	public class DoubleBuffer<T>
	{
		public int Count { get; private set; }

		private T[] buffer;
		private int currentIdx;

		public T Current
		{
			get => buffer[currentIdx];
			set => buffer[currentIdx] = value;
		}

		public T Next
		{
			get => buffer[(currentIdx + 1) % Count];
			set => buffer[(currentIdx + 1) % Count] = value;
		}

		public DoubleBuffer(int capacity = 2)
		{
			buffer = new T[capacity];
			Count = capacity;
		}

		public DoubleBuffer(Func<int, T> initFunc, int capacity = 2) : this(capacity)
		{
			for (int i = 0; i < Count; i++)
			{
				buffer[i] = initFunc(i);
			}
		}

		public T Flip()
		{
			currentIdx = (currentIdx + 1) % Count;
			return buffer[currentIdx];
		}
	}
}