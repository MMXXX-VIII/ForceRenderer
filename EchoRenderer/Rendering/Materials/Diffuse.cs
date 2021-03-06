﻿using CodeHelpers.Mathematics;
using EchoRenderer.Mathematics;
using EchoRenderer.Mathematics.Intersections;

namespace EchoRenderer.Rendering.Materials
{
	public class Diffuse : Material
	{
		public override Float3 BidirectionalScatter(in CalculatedHit hit, ExtendedRandom random, out Float3 direction)
		{
			if (CullBackface(hit) || AlphaTest(hit, out Float3 color))
			{
				direction = hit.direction;
				return Float3.one;
			}

			direction = (hit.normal + random.NextOnSphere()).Normalized;
			return color;
		}
	}
}