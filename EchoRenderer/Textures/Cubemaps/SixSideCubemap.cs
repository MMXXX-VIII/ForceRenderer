﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CodeHelpers.Mathematics;
using EchoRenderer.Mathematics;

namespace EchoRenderer.Textures.Cubemaps
{
	public class SixSideCubemap : Cubemap
	{
		public SixSideCubemap(string path) : this(path, Float3.one) { }

		public SixSideCubemap(string path, Float3 multiplier)
		{
			var names = IndividualTextureNames;
			Texture[] sources = new Texture[names.Count];

			Exception error = null;

			Parallel.For(0, names.Count, Load);
			if (error != null) throw error;

			textures = new ReadOnlyCollection<Texture>(sources);
			this.multiplier = multiplier;

			void Load(int index, ParallelLoopState state)
			{
				try
				{
					sources[index] = Texture2D.Load(Path.Combine(path, names[index]));
				}
				catch (FileNotFoundException exception)
				{
					error = exception;
					state.Stop();
				}
			}
		}

		readonly ReadOnlyCollection<Texture> textures;
		readonly Float3 multiplier;

		public static readonly ReadOnlyCollection<string> IndividualTextureNames = new ReadOnlyCollection<string>(new[] {"px", "py", "pz", "nx", "ny", "nz"});

		public override Float3 Sample(in Float3 direction)
		{
			int index = direction.Absoluted.MaxIndex;

			float component = direction[index];
			if (direction[index] < 0f) index += 3;

			Float2 uv = index switch
						{
							0 => new Float2(-direction.z, direction.y),
							1 => new Float2(direction.x, -direction.z),
							2 => direction.XY,
							3 => direction.ZY,
							4 => direction.XZ,
							_ => new Float2(-direction.x, direction.y)
						};

			component = 0.5f / Math.Abs(component);
			return Sample(index, uv * component);
		}

		/// <summary>
		/// Samples a specific bitmap at <paramref name="uv"/>.
		/// <paramref name="uv"/> is between -0.5 to 0.5 with zero in the middle.
		/// </summary>
		Float3 Sample(int index, Float2 uv) => Utilities.ToFloat4(textures[index][uv + Float2.half]).XYZ * multiplier;
	}
}