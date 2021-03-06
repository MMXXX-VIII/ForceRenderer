using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using CodeHelpers;
using CodeHelpers.Mathematics;
using CodeHelpers.Mathematics.Enumerables;
using EchoRenderer.Mathematics;
using EchoRenderer.Textures;

namespace EchoRenderer.IO
{
	public class Font
	{
		Font(string path)
		{
			texture = Texture2D.Load(path);

			Int2 size = texture.size;
			Int2 glyph = size / MapSize;

			if (size % MapSize != Int2.zero) throw new Exception($"Invalid font map texture size {size}.");

			glyphs = new Glyph[MapSize * MapSize];
			glyphAspect = (float)glyph.x / glyph.y;

			Parallel.For(0, glyphs.Length, ComputeSingleGlyph);

			void ComputeSingleGlyph(int index)
			{
				Int2 position = new Int2(index % MapSize, index / MapSize) * glyph;

				Float2 min = Float2.positiveInfinity;
				Float2 max = Float2.negativeInfinity;

				foreach (Int2 local in new EnumerableSpace2D(position, position + glyph - Int2.one))
				{
					float strength = texture[local].GetElement(0);
					texture[local] = Vector128.Create(strength);

					if (strength.AlmostEquals(0f)) continue;

					min = min.Min(local);
					max = max.Max(local);
				}

				max += Float2.one;

				min /= size;
				max /= size;

				Float2 origin = (position + glyph / 2f) / size;
				glyphs[index] = new Glyph(min, max, origin);
			}
		}

		readonly Texture2D texture;
		readonly Glyph[] glyphs;

		readonly float glyphAspect;

		const int MapSize = 8;

		static readonly Dictionary<string, Font> fonts = new();

		/// <summary>
		/// Draws <paramref name="character"/> to <paramref name="destination"/> at <see cref="center"/>
		/// with this <see cref="Font"/> and <paramref name="style"/>.
		/// </summary>
		public void Draw(Texture2D destination, char character, Float2 center, Style style)
		{
			Glyph glyph = glyphs[GetIndex(character)];
			float multiplier = MapSize * style.Height;

			Float2 min = (glyph.minUV - glyph.origin) * multiplier + center;
			Float2 max = (glyph.maxUV - glyph.origin) * multiplier + center;

			Float4 colorSource = Utilities.ToColor(style.Color);

			Vector128<float> color = Utilities.ToVector(colorSource);
			Vector128<float> alpha = Vector128.Create(style.Color.w);

			Int2 sampleSquare = (Int2)style.SampleSize;
			float inverse = 1f / (sampleSquare.x + 1f);

			Vector128<float> sizeInverse = Vector128.Create(1f / sampleSquare.Product);
			Parallel.ForEach(new EnumerableSpace2D(min.Floored, max.Ceiled), DrawPixel);

			void DrawPixel(Int2 position)
			{
				Vector128<float> total = Utilities.vector0;
				Vector128<float> source = destination[position];

				//Take multiple samples and calculate the average
				foreach (Int2 offset in new EnumerableSpace2D(Int2.one, sampleSquare))
				{
					Float2 point = (position + offset * inverse - center) / multiplier + glyph.origin;
					if (glyph.minUV <= point && point <= glyph.maxUV) total = Sse.Add(total, texture[point]);
				}

				//Assigns color based on alpha
				total = Sse.Multiply(total, sizeInverse);
				destination[position] = Utilities.Lerp(source, color, Sse.Multiply(alpha, total));
			}
		}

		/// <summary>
		/// Draws <paramref name="text"/> to <paramref name="destination"/> at <see cref="center"/>
		/// with this <see cref="Font"/> and <paramref name="style"/>.
		/// </summary>
		public void Draw(Texture2D destination, string text, Float2 center, Style style)
		{
			float offset = text.Length / 2f - 0.5f;
			float aspect = style.GlyphWidth * glyphAspect;

			for (int i = 0; i < text.Length; i++)
			{
				float x = (i - offset) * aspect * style.Height;
				Float2 position = center + new Float2(x, 0f);

				char character = text[i];

				if (char.IsWhiteSpace(character)) continue;
				Draw(destination, character, position, style);
			}
		}

		/// <summary>
		/// Calculates the drawing region width of string with <paramref name="length"/> and <paramref name="style"/>.
		/// </summary>
		public float GetWidth(int length, Style style)
		{
			float aspect = glyphAspect * style.GlyphWidth;
			return length * style.Height * aspect;
		}

		/// <summary>
		/// Finds or loads a <see cref="Font"/> from <paramref name="path"/>.
		/// </summary>
		public static Font Find(string path)
		{
			if (!fonts.TryGetValue(path, out Font font))
			{
				font = new Font(path);
				fonts.Add(path, font);
			}

			return font;
		}

		static int GetIndex(char character)
		{
			const int LetterCount = 'Z' - 'A' + 1;

			int order = character switch
						{
							>= 'A' and <= 'Z' => character - 'A',
							>= 'a' and <= 'z' => character - 'a' + LetterCount,
							>= '0' and <= '9' => character - '0' + LetterCount * 2,
							'.' => LetterCount * 2 + 10,
							',' => LetterCount * 2 + 11,
							_ => throw ExceptionHelper.Invalid(nameof(character), character, InvalidType.unexpected)
						};

			Int2 position = new Int2(order % MapSize, order / MapSize);
			position = new Int2(position.x, MapSize - position.y - 1);

			return position.x + position.y * MapSize;
		}

		public record Style
		{
			public Style(float height) : this(height, Float4.one) { }

			public Style(float height, Float4 color)
			{
				Height = height;
				Color = color;
			}

			public float Height { get; init; }
			public Float4 Color { get; init; }

			public float GlyphWidth { get; init; } = 0.6f; //How compact should the width of each glyph be
			public int SampleSize { get; init; } = 5;      //The square multisampling size for each pixel
		}

		readonly struct Glyph
		{
			public Glyph(Float2 minUV, Float2 maxUV, Float2 origin)
			{
				this.minUV = minUV;
				this.maxUV = maxUV;
				this.origin = origin;
			}

			public readonly Float2 minUV;
			public readonly Float2 maxUV;
			public readonly Float2 origin;
		}
	}
}