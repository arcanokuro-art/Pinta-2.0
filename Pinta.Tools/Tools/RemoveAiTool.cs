// SPDX-License-Identifier: MIT
using System;
using Cairo;
using Pinta.Core;

namespace Pinta.Tools;

/// <summary>
/// Velyntora Remove AI selection tool.
/// The user paints a mask on Pinta's ToolLayer.  The mask is kept separate
/// from the artwork until MI-GAN returns a validated result.
/// </summary>
public sealed class RemoveAiTool : BaseBrushTool
{
	private PointI? last_point;
	private readonly IRemoveAiBackend backend = new MiganRemoveAiBackend ();

	public RemoveAiTool (IServiceProvider services) : base (services) { }

	public override string Name => Translations.GetString ("Remove AI");
	// Temporary stock icon while the final Velyntora Remove AI asset is designed.
	public override string Icon => Pinta.Resources.Icons.ToolEraser;
	public override string StatusBarText =>
		Translations.GetString ("Paint over the object to remove. The mask is processed locally by MI-GAN.");
	public override bool CursorChangesOnZoom => true;
	public override Gdk.Key ShortcutKey => new (Gdk.Constants.KEY_R);
	public override int Priority => 28;

	public override Gdk.Cursor DefaultCursor {
		get {
			var icon = GdkExtensions.CreateIconWithShape (
				"Cursor.Eraser.png",
				CursorShape.Ellipse,
				BrushWidth,
				8,
				22,
				out int x,
				out int y);
			return Gdk.Cursor.NewFromTexture (icon, x, y, null);
		}
	}

	protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
	{
		// Unlike a normal brush, never paint into CurrentUserLayer here.
		// ToolLayer is only a visual/binary mask until inference succeeds.
		document.Layers.ToolLayer.Clear ();
		document.Layers.ToolLayer.Hidden = false;
		last_point = null;
		base.OnMouseDown (document, e);
	}

	protected override void OnMouseMove (Document document, ToolMouseEventArgs e)
	{
		if (mouse_button != MouseButton.Left) {
			last_point = null;
			return;
		}

		PointI current = e.Point;
		if (!last_point.HasValue)
			last_point = current;

		if (!document.Workspace.PointInCanvas (e.PointDouble))
			return;

		using Context g = document.CreateClippedToolContext ();
		g.Antialias = Antialias.None;
		g.LineWidth = BrushWidth;
		g.LineJoin = LineJoin.Round;
		g.LineCap = LineCap.Round;

		// Visible mask overlay. White is also the binary foreground expected by
		// the future MI-GAN adapter.
		g.SetSourceRGBA (1.0, 1.0, 1.0, 0.70);
		g.MoveTo (last_point.Value.X, last_point.Value.Y);
		g.LineTo (current.X, current.Y);
		g.Stroke ();

		int padding = BrushWidth + 2;
		RectangleI dirty = RectangleI
			.FromPoints (last_point.Value, current)
			.Inflated (padding, padding);

		document.Workspace.Invalidate (document.ClampToImageSize (dirty));
		last_point = current;
	}

	protected override void OnMouseUp (Document document, ToolMouseEventArgs e)
	{
		// Do NOT call BaseBrushTool.OnMouseUp: the mask must not create an
		// artwork history item and must not alter the user's layer.
		mouse_button = MouseButton.None;
		last_point = null;

		// Keep the overlay visible for now.  Applying it is deliberately gated
		// on backend.IsAvailable; no fake success or destructive fallback.
		if (!backend.IsAvailable)
			return;
	}

	protected override void OnDeactivated (Document? document)
	{
		if (document is not null) {
			document.Layers.ToolLayer.Clear ();
			document.Layers.ToolLayer.Hidden = true;
			document.Workspace.Invalidate ();
		}
		base.OnDeactivated (document);
	}
}
