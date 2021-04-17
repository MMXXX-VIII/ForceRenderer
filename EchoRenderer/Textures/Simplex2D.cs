﻿using System.Runtime.Intrinsics;
using CodeHelpers.Mathematics;
using EchoRenderer.Mathematics;

namespace EchoRenderer.Textures
{
	public class Simplex2D : Generative2D
	{
		public Simplex2D(Int2 size, int seed, int layer, float persistence = 0.5f) : base(size)
		{
			this.persistence = persistence;
			simplices = new Simplex[layer];

			float frequency = 1f;

			for (int i = 0; i < layer; i++)
			{
				simplices[i] = new Simplex(seed ^ i);

				inverseScale += frequency;
				frequency *= persistence;
			}

			inverseScale = 1f / inverseScale;
		}

		readonly float persistence;
		readonly float inverseScale;

		readonly Simplex[] simplices;

		protected override Vector128<float> Sample(Float2 position)
		{
			// Float2 q = new Float2(FractionalBrownianMotion(position), FractionalBrownianMotion(position + new Float2(5.2f, 1.3f)));
			// Float2 r = new Float2(FractionalBrownianMotion(position + 4f * q + new Float2(1.7f, 9.2f)), FractionalBrownianMotion(position + 4f * q + new Float2(8.3f, 2.8f)));
			//
			// float v = FractionalBrownianMotion(position + 4f * r);

			float v0 = FractionalBrownianMotion(position);

			float v1 = FractionalBrownianMotion(position + new Float2(v0 * 5.2f, v0 * 2.3f));
			float v2 = FractionalBrownianMotion(position + new Float2(v1 * 2.7f, v1 * 4.7f));

			return Utilities.ToVector(Utilities.ToColor(new Float3(v1, v2, 1f)));
		}

		float FractionalBrownianMotion(Float2 position)
		{
			float sum = 0f;
			float frequency = 1f;

			for (int i = 0; i < simplices.Length; i++)
			{
				sum += simplices[i].Sample(position / frequency) * frequency;
				frequency *= persistence;
			}

			return sum * inverseScale;
		}
	}
}