﻿using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using CodeHelpers.Collections;
using CodeHelpers.Mathematics;

namespace IntrinsicsSIMD
{
	public class BenchmarkRadixSort
	{
		public BenchmarkRadixSort()
		{
			sources = new float[100][];

			for (int i = 0; i < sources.Length; i++)
			{
				sources[i] = Enumerable.Range(0, 1 << 20).Select(value => ((float)value - (1 << 19)) * MathF.Sqrt(i + 1f)).ToArray();
				sources[i].Shuffle();
			}

			sources.Shuffle();
			radix = new RadixSort();
		}

		readonly float[][] sources;
		readonly RadixSort radix;

		float[][] targets;

		// | SeeSharpSort | 8.119 s 
		// |    RadixSort | 7.515 s 


		[IterationSetup]
		public void Setup()
		{
			targets ??= new float[sources.Length][];
			for (int i = 0; i < targets.Length; i++) targets[i] = (float[])sources[i].Clone();
		}

		[Benchmark]
		public void SeeSharpSort()
		{
			for (int i = 0; i < targets.Length; i++)
			{
				Array.Sort(targets[i]);
			}
		}

		[Benchmark]
		public void RadixSort()
		{
			for (int i = 0; i < targets.Length; i++)
			{
				radix.Sort(targets[i]);
			}
		}

		bool IsSorted<T>(IReadOnlyList<T> array)
		{
			Comparer<T> comparer = Comparer<T>.Default;

			for (int i = 1; i < array.Count; i++)
			{
				if (comparer.Compare(array[i - 1], array[i]) > 0) return false;
			}

			return true;
		}
	}
}