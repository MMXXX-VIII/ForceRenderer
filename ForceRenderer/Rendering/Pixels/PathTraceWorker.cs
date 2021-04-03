﻿using CodeHelpers.Diagnostics;
using CodeHelpers.Mathematics;
using ForceRenderer.Mathematics;
using ForceRenderer.Mathematics.Intersections;
using ForceRenderer.Objects;
using ForceRenderer.Rendering.Materials;

namespace ForceRenderer.Rendering.Pixels
{
	public class PathTraceWorker : PixelWorker
	{
		public override Float3 Render(Float2 screenUV)
		{
			PressedScene scene = Profile.scene;
			Ray ray = scene.camera.GetRay(screenUV);

			Float3 energy = Float3.one; //AKA energy
			Float3 color = Float3.zero;

			ExtendedRandom random = Random;
			int bounce = 0;

			while (bounce < Profile.bounceLimit && scene.GetIntersection(ray, out CalculatedHit hit))
			{
				++bounce;

				Material material = hit.material;
				material.ApplyNormal(hit);

				Float3 emission = material.Emit(hit, random);
				Float3 bsdf = material.BidirectionalScatter(hit, random, out Float3 direction);

				color += energy * emission;
				energy *= bsdf;

				if (energy <= Profile.energyEpsilon) break;
				ray = new Ray(hit.position, direction, true);
			}

			var cubemap = scene.cubemap;
			var lights = scene.lights;

			if (bounce == 0) return color + cubemap?.Sample(ray.direction) ?? Float3.zero;
			if (cubemap != null) color += energy * cubemap.Sample(ray.direction);

			for (int i = 0; i < lights.Count; i++)
			{
				PressedLight light = lights[i];

				float weight = -light.direction.Dot(ray.direction);
				if (weight > light.threshold) color += energy * light.intensity * weight;
			}

			return color.Max(Float3.zero); //Do not clamp up, because emissive samples can go beyond 1f
		}
	}
}