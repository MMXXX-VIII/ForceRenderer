﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CodeHelpers;
using CodeHelpers.Vectors;
using ForceRenderer.IO;
using ForceRenderer.Mathematics;
using ForceRenderer.Scenes;

namespace ForceRenderer.Renderers
{
	public class RenderEngine
	{
		public RenderEngine(Scene scene, int pixelSample)
		{
			this.scene = scene;
			this.pixelSample = pixelSample;

			threadRandom = new ThreadLocal<Random>(() => new Random(Thread.CurrentThread.ManagedThreadId));
			sampleSpiralOffsets = new Float2[pixelSample];

			//Create sample spiral offset positions
			for (int i = 0; i < pixelSample; i++)
			{
				float index = i + 0.5f;

				float length = (float)Math.Sqrt(index / pixelSample);
				float angle = Scalars.PI * (1f + (float)Math.Sqrt(5d)) * index;

				Float2 sample = new Float2((float)Math.Cos(angle), (float)Math.Sin(angle));
				sampleSpiralOffsets[i] = sample * length / 2f + Float2.half;
			}
		}

		public readonly Scene scene;
		public readonly int pixelSample; //Sample per pixel

		public int MaxBounce { get; set; } = 32;
		public float EnergyEpsilon { get; set; } = 1E-3f; //Epsilon lower bound value to determine when an energy is essentially zero

		readonly ThreadLocal<Random> threadRandom;
		readonly Float2[] sampleSpiralOffsets;

		PressedScene pressedScene;
		Thread renderThread;
		RenderProfile profile;

		volatile Texture _renderBuffer;
		long _currentState;

		public Texture RenderBuffer
		{
			get => _renderBuffer;
			set
			{
				if (CurrentState == State.rendering) throw new Exception("Cannot modify buffer when rendering!");
				Interlocked.Exchange(ref _renderBuffer, value);
			}
		}

		public State CurrentState
		{
			get => (State)Interlocked.Read(ref _currentState);
			private set => Interlocked.Exchange(ref _currentState, (long)value);
		}

		public bool Completed => CurrentState == State.completed;

		TileWorker primaryWorker;
		TileWorker secondaryWorker;

		public void Begin()
		{
			if (RenderBuffer == null) throw ExceptionHelper.Invalid(nameof(RenderBuffer), this, InvalidType.isNull);
			if (CurrentState != State.waiting) throw new Exception("Incorrect state! Must reset before rendering!");

			pressedScene = new PressedScene(scene);
			if (pressedScene.camera == null) throw new Exception("No camera in scene! Cannot render without a camera!");

			profile = new RenderProfile(scene, pressedScene, pixelSample, RenderBuffer.aspect, MaxBounce, EnergyEpsilon);
			renderThread = new Thread(RenderThread) {Priority = ThreadPriority.Highest, IsBackground = true};

			CurrentState = State.rendering;
			renderThread.Start();
		}

		public void WaitForRender() => renderThread.Join();
		public void Stop() => CurrentState = State.stopped;

		public void Reset()
		{
			CurrentState = State.waiting;
			primaryWorker = secondaryWorker = null;

			profile = default;
			pressedScene = null;
			renderThread = null;
		}

		void RenderThread()
		{
			primaryWorker = new TileWorker(pixelSample,);

			CurrentState = State.completed;
		}

		Shade RenderIndex(int index)
		{
			//Final buffer, need high precision
			double r = 0d;
			double g = 0d;
			double b = 0d;

			for (int i = 1; i <= pixelSample; i++)
			{
				Int2 position = profile.buffer.ToPosition(index); //Integer pixel position
				Float2 offset = sampleSpiralOffsets[i - 1];       //Multi sample offset; from zero to one; generated before render started
				Float3 single = RenderPixel(profile.buffer.ToUV(position + offset));

				//Combine colors by averaging all samples
				double multiplier = (i - 1d) / i;

				r = r * multiplier + (double)single.x / i;
				g = g * multiplier + (double)single.y / i;
				b = b * multiplier + (double)single.z / i;
			}

			//Instead of doing total / count, this averaging method avoid precision loss
			return new Shade((float)r, (float)g, (float)b);
		}

		/// <summary>
		/// Renders a single pixel and returns the result.
		/// </summary>
		/// <param name="uv">Zero to one normalized raw uv without any scaling.</param>
		Float3 RenderPixel(Float2 uv)
		{
			Float2 scaled = new Float2(uv.x - 0.5f, (uv.y - 0.5f) / RenderAspect);
			Ray ray = new Ray(pressedScene.camera.Position, pressedScene.camera.GetDirection(scaled));

			int bounce = 0;

			Float3 energy = Float3.one;
			Float3 color = Float3.zero;

			while (TryTrace(ray, out float distance, out int token) && bounce++ < profile.maxBounce)
			{
				ref PressedBundle bundle = ref pressedScene.GetPressedBundle(token);

				Float3 position = ray.GetPoint(distance);
				Float3 normal = pressedScene.GetNormal(position, token);

				//Lambert diffuse
				ray = new Ray(position, GetHemisphereDirection(normal), true);
				energy *= 2f * normal.Dot(ray.direction).Clamp(0f, 1f) * bundle.material.albedo;

				// if (pressedScene.directionalLight != null)
				// {
				// 	DirectionalLight light = pressedScene.directionalLight;
				// 	Ray lightRay = new Ray(position, -light.Direction, true);
				//
				// 	float coefficient = normal.Dot(lightRay.direction).Clamp(0f, 1f);
				// 	if (coefficient > 0f) coefficient *= TryTraceShadow(lightRay);
				//
				// 	color += coefficient * energy * bundle.material.albedo * light.Intensity;
				// }

				// ray = new Ray(position, ray.direction.Reflect(normal), true);
				// energy *= bundle.material.specular;

				if (energy.x <= profile.energyEpsilon && energy.y <= profile.energyEpsilon && energy.z <= profile.energyEpsilon) break;
			}

			//return (Float3)((float)bounce / profile.maxBounce);

			if (scene.Cubemap == null) return color;
			return color + energy * (Float3)scene.Cubemap.Sample(ray.direction) * 1.8f; //Sample skybox
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		bool TryTrace(in Ray ray, out float distance, out int token)
		{
			distance = pressedScene.GetIntersection(ray, out token);
			return distance < float.PositiveInfinity;
		}

		float TryTraceShadow(in Ray ray)
		{
			float distance = pressedScene.GetIntersection(ray);
			return distance < float.PositiveInfinity ? 0f : 1f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		Float3 GetHemisphereDirection(Float3 normal)
		{
			//Uniformly sample directions in a hemisphere
			float cos = RandomValue;
			float sin = (float)Math.Sqrt(Math.Max(0f, 1f - cos * cos));
			float phi = Scalars.TAU * RandomValue;

			float x = (float)Math.Cos(phi) * sin;
			float y = (float)Math.Sin(phi) * sin;
			float z = cos;

			//Transform local direction to world-space based on normal
			Float3 helper = Math.Abs(normal.x) >= 0.9f ? Float3.forward : Float3.right;

			Float3 tangent = Float3.Cross(normal, helper).Normalized;
			Float3 binormal = Float3.Cross(normal, tangent).Normalized;

			//Transforms using matrix multiplication. 3x3 matrix instead of 4x4 because direction only
			return new Float3
			(
				x * tangent.x + y * binormal.x + z * normal.x,
				x * tangent.y + y * binormal.y + z * normal.y,
				x * tangent.z + y * binormal.z + z * normal.z
			);
		}

		public enum State
		{
			waiting,
			rendering,
			completed,
			stopped
		}
	}
}