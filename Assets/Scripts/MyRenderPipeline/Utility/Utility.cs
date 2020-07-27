using System;
using System.Collections.Generic;

namespace MyRenderPipeline.Utility
{
	public static class Utility
	{
		public static void ForEach<T>(this IEnumerable<T> ts, Action<T> callback)
		{
			foreach (var item in ts)
				callback(item);
		}

		public static TResult MaxOf<T, TCompare, TResult>(this IEnumerable<T> collection,
			Func<T, TCompare> comparerSelector, Func<T, TResult> resultSelector)
			where TCompare : IComparable
		{
			bool hasFirstValue = false;
			var minValue = default(TCompare);
			TResult result = default(TResult);
			foreach (var element in collection)
			{
				var value = comparerSelector(element);
				if (!hasFirstValue)
				{
					minValue = value;
					result = resultSelector(element);
					hasFirstValue = true;
				}
				else if (minValue.CompareTo(value) < 0)
				{
					minValue = value;
					result = resultSelector(element);
				}
			}

			return result;
		}
	}
}