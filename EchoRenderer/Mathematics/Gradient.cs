﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;
using CodeHelpers.Collections;
using CodeHelpers.Mathematics;
using EchoRenderer.Textures;

namespace EchoRenderer.Mathematics
{
	public class Gradient : IEnumerable<float>
	{
		readonly List<Anchor> anchors = new List<Anchor>();

		public Float4 this[float percent] => Utilities.ToFloat4(SampleVector(percent));

		public void Add(float percent, Float4 color)
		{
			int index = anchors.BinarySearch(percent, Comparer.instance);
			Anchor anchor = new Anchor(percent, color);

			if (index >= 0) anchors[index] = anchor;
			else anchors.Insert(~index, anchor);
		}

		public bool Remove(float percent)
		{
			int index = anchors.BinarySearch(percent, Comparer.instance);
			if (index < 0) return false;

			anchors.RemoveAt(index);
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Vector128<float> SampleVector(float percent)
		{
			if (anchors.Count == 0) throw new Exception("Cannot sample with zero anchor!");
			int index = anchors.BinarySearch(percent, Comparer.instance);

			if (index < 0) index = ~index;
			else return anchors[index].vector;

			Anchor head = index == 0 ? anchors[index] : anchors[index - 1];
			Anchor tail = index == anchors.Count ? anchors[index - 1] : anchors[index];

			float time = Scalars.InverseLerp(head.percent, tail.percent, percent).Clamp(0f, 1f);
			return Utilities.Lerp(head.vector, tail.vector, Vector128.Create(time));
		}

		public void Apply(Texture texture, Float2 point0, Float2 point1)
		{
			Segment2 segment = new Segment2(point0, point1);
			Parallel.For(0, texture.size.Product, SamplePixel);

			void SamplePixel(int index)
			{
				Float2 position = texture.ToPosition(index) + Float2.half;
				float percent = segment.ClosestInverseLerpUnclamped(position);

				texture[index] = SampleVector(percent);
			}
		}

		public IEnumerator<float> GetEnumerator() => anchors.Select(anchor => anchor.percent).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		class Comparer : IDoubleComparer<Anchor, float>
		{
			public static readonly Comparer instance = new Comparer();

			public int CompareTo(Anchor first, float second)
			{
				if (Scalars.AlmostEquals(first.percent, second)) return 0;
				return first.percent.CompareTo(second);
			}
		}

		readonly struct Anchor
		{
			public Anchor(float percent, Float4 color)
			{
				this.percent = percent;
				vector = Unsafe.As<Float4, Vector128<float>>(ref color);
			}

			public readonly float percent;
			public readonly Vector128<float> vector;
		}
	}
}