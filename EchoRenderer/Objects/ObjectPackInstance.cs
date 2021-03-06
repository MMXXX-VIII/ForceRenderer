﻿using System;
using CodeHelpers.Mathematics;
using EchoRenderer.Objects.Scenes;

namespace EchoRenderer.Objects
{
	public class ObjectPackInstance : Object
	{
		public ObjectPackInstance(ObjectPack objectPack = null) => ObjectPack = objectPack;

		public override Float3 Scale
		{
			get => base.Scale;
			set
			{
				if (value.x.AlmostEquals(value.y) && value.x.AlmostEquals(value.z)) base.Scale = (Float3)value.Average;
				else throw new Exception($"Cannot use none uniformed scale of '{value}' for {nameof(ObjectPackInstance)}!");
			}
		}

		public ObjectPack ObjectPack { get; set; }
		public MaterialMapper Mapper { get; set; }
	}
}