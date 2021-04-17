﻿using System;
using CodeHelpers.Mathematics;
using CodeHelpers.Mathematics.Enumerables;
using EchoRenderer.Textures;

namespace EchoRenderer.Rendering.PostProcessing
{
	public class Denoiser : PostProcessingWorker
	{
		public Denoiser(PostProcessingEngine engine, Texture albedoBuffer) : base(engine) => this.albedoBuffer = albedoBuffer;

		Texture sourceBuffer;
		Texture albedoBuffer;

		public override void Dispatch()
		{
			sourceBuffer = new Texture2D(renderBuffer.size);

			RunCopyPass(renderBuffer, sourceBuffer);
			RunPass(SaturationPass);
		}

		void SaturationPass(Int2 position)
		{
			Float3 averageSource = Float3.zero;
			Float3 averageAlbedo = Float3.zero;

			float count = 0f;
			int radius = 3;

			foreach (Int2 local in new EnumerableSpace2D((Int2)(-radius), (Int2)radius))
			{
				averageSource += sourceBuffer[position + local].XYZ;
				averageAlbedo += albedoBuffer[position + local].XYZ;

				count++;
			}

			averageSource /= count;
			averageAlbedo /= count;

			Float3 differenceSource = (averageSource - sourceBuffer[position].XYZ).Absoluted / averageSource;
			Float3 differenceAlbedo = (averageAlbedo - albedoBuffer[position].XYZ).Absoluted / averageAlbedo;

			float luminance = GetLuminance(differenceSource) - GetLuminance(differenceAlbedo);
			renderBuffer[position] = ToColor((Float3)Math.Abs(luminance));
		}

		static Float4 ToColor(in Float3 color) => new Float4(color.x, color.y, color.z, 1f);

		static float GetSaturation(in Float3 color)
		{
			float min = color.MinComponent;
			float max = color.MaxComponent;

			return Scalars.AlmostEquals(max, 0f) ? 0f : (max - min) / max;
		}

		static float GetLuminance(in Float3 color) => color.Dot(new Float3(0.2126f, 0.7152f, 0.0722f));
	}
}