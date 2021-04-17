﻿using CodeHelpers.Mathematics;

namespace EchoRenderer.Textures
{
	public class SolidCubemap : Cubemap
	{
		public SolidCubemap(Float3 ambient) => this.ambient = ambient;
		public SolidCubemap(float ambient) : this((Float3)ambient) { }

		public readonly Float3 ambient;

		public override Float3 Sample(Float3 direction) => ambient;
	}
}