using System.Collections.Generic;
using UnityEngine;

namespace MyRenderPipeline.Utils
{
	public static class Sampler
	{
		public static double RadicalInverse(int baseNumber, long n)
		{
			if (n == 0)
				return 0;

			long inversed = 0;
			double inverseBase = 1.0 / baseNumber;
			double inveseExponent = 1;

			//注意这里n是整数
			while (n != 0)
			{
				inversed = inversed * baseNumber + (n % baseNumber);
				n /= baseNumber;
				inveseExponent *= inverseBase;
			}

			return inversed * inveseExponent;
		}
		
		//linq 配合 迭代器 可以拿到指定数量的
		public static IEnumerable<Vector2> HaltonSequence2(int baseX, int baseY)
		{
			for (long n = 0; ; n++)
				yield return new Vector2((float)RadicalInverse(baseX, n), (float)RadicalInverse(baseY, n));
		}
	}
}