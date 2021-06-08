﻿using BenchmarkDotNet.Running;

namespace IntrinsicsSIMD
{
	public class Program
	{
		static void Main()
		{
			// BenchmarkRunner.Run<TestSIMD>();
			// BenchmarkRunner.Run<BenchmarkAABB>();
			// BenchmarkRunner.Run<BenchmarkBVH>();
			// BenchmarkRunner.Run<BenchmarkTexture>();
			// BenchmarkRunner.Run<BenchmarkRadixSort>();
			BenchmarkRunner.Run<BenchmarkLoop>();
		}
	}
}