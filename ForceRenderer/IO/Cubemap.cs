using CodeHelpers;
using CodeHelpers.Vectors;

namespace ForceRenderer.IO
{
	public abstract class Cubemap
	{
		/// <summary>
		/// Samples the <see cref="Cubemap"/> at a particular <paramref name="direction"/>.
		/// NOTE: <paramref name="direction"/> should be normalized.
		/// </summary>
		public abstract Shade Sample(Float3 direction);
	}
}