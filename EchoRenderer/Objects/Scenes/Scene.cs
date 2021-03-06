﻿using CodeHelpers.Mathematics;
using EchoRenderer.Mathematics;
using EchoRenderer.Objects.GeometryObjects;
using EchoRenderer.Rendering.Materials;
using EchoRenderer.Textures.Cubemaps;

namespace EchoRenderer.Objects.Scenes
{
	public class Scene : ObjectPack
	{
		public Cubemap Cubemap { get; set; }
	}

	public class StandardScene : Scene
	{
		public StandardScene(Material ground = null)
		{
			Cubemap = new SixSideCubemap("Assets/Cubemaps/OutsideSea");

			children.Add(new PlaneObject(ground ?? new Diffuse {Albedo = (Float3)0.75f}, new Float2(32f, 24f)));
			children.Add(new Light {Intensity = Utilities.ToColor("#c9e2ff").XYZ, Rotation = new Float3(60f, 60f, 0f)});

			children.Add(new Camera(110f) {Position = new Float3(0f, 3f, -6f), Rotation = new Float3(30f, 0f, 0f)});
		}
	}
}