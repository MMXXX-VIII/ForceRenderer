﻿using EchoRenderer.UI.Core.Interactions;
using SFML.Graphics;

namespace EchoRenderer.UI.Core.Areas
{
	public abstract class PressableUI : AreaUI, IHoverable
	{
		public PressableUI() => base.FillColor = Theme.PanelColor;

		public bool IsHovering { get; private set; }
		public bool IsPressing { get; private set; }

		Color HoverColor => IsHovering ? Theme.HoverColor : Theme.PanelColor;

		public virtual void OnMouseHovered(MouseHover mouse)
		{
			IsHovering = mouse.type != MouseHover.Type.exit;
			if (!IsPressing) FillColor = HoverColor;
		}

		public virtual void OnMousePressed(MousePress mouse)
		{
			IsPressing = mouse.type == MousePress.Type.down;
			FillColor = IsPressing ? Theme.PressColor : HoverColor;

			if (IsPressing) OnMousePressed();
		}

		protected abstract void OnMousePressed();
	}
}