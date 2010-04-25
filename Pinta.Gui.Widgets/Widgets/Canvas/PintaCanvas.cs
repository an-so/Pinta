﻿// 
// PintaCanvas.cs
//  
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Jonathan Pobst
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Gdk;
using Gtk;
using Pinta.Core;

namespace Pinta.Gui.Widgets
{
	public class PintaCanvas : DrawingArea
	{
		Cairo.ImageSurface canvas;
		CanvasRenderer cr;
		GridRenderer gr;

		public PintaCanvas ()
		{
			cr = new CanvasRenderer ();
			gr = new GridRenderer (cr);
			
			// Keep the widget the same size as the canvas
			PintaCore.Workspace.CanvasSizeChanged += delegate (object sender, EventArgs e) {
				SetRequisition (PintaCore.Workspace.CanvasSize);
			};

			// Update the canvas when the image changes
			PintaCore.Workspace.CanvasInvalidated += delegate (object sender, CanvasInvalidatedEventArgs e) {
				if (e.EntireSurface)
					GdkWindow.Invalidate ();
				else
					GdkWindow.InvalidateRect (e.Rectangle, false);
			};
		}
		
		private void SetRequisition (Size size)
		{
			Requisition req = new Requisition ();
			req.Width = size.Width;
			req.Height = size.Height;
			Requisition = req;

			QueueResize ();
		}

		protected override bool OnExposeEvent (EventExpose e)
		{
			base.OnExposeEvent (e);

			double scale = PintaCore.Workspace.Scale;

			int x = (int)PintaCore.Workspace.Offset.X;
			int y = (int)PintaCore.Workspace.Offset.Y;

			// Translate our expose area for the whole drawingarea to just our canvas
			Rectangle canvas_bounds = new Rectangle (x, y, PintaCore.Workspace.CanvasSize.Width, PintaCore.Workspace.CanvasSize.Height);
			canvas_bounds.Intersect (e.Area);

			if (canvas_bounds.IsEmpty)
				return true;

			canvas_bounds.X -= x;
			canvas_bounds.Y -= y;

			// Resize our offscreen surface to a surface the size of our drawing area
			if (canvas == null || canvas.Width != canvas_bounds.Width || canvas.Height != canvas_bounds.Height) {
				if (canvas != null)
					(canvas as IDisposable).Dispose ();

				canvas = new Cairo.ImageSurface (Cairo.Format.Argb32, canvas_bounds.Width, canvas_bounds.Height);
			}

			cr.Initialize (PintaCore.Workspace.ImageSize, PintaCore.Workspace.CanvasSize);

			using (Cairo.Context g = CairoHelper.Create (GdkWindow)) {
				// Draw our 1 px black border
				g.DrawRectangle (new Cairo.Rectangle (x, y, PintaCore.Workspace.CanvasSize.Width + 1, PintaCore.Workspace.CanvasSize.Height + 1), new Cairo.Color (0, 0, 0), 1);

				// Set up our clip rectangle
				g.Rectangle (new Cairo.Rectangle (x, y, PintaCore.Workspace.CanvasSize.Width, PintaCore.Workspace.CanvasSize.Height));
				g.Clip ();

				g.Translate (x, y);

				bool checker = true;

				// Resize each layer and paint it to the screen
				foreach (Layer layer in PintaCore.Layers.GetLayersToPaint ()) {
					cr.Render (layer.Surface, canvas, canvas_bounds.Location, checker);
					g.SetSourceSurface (canvas, canvas_bounds.X, canvas_bounds.Y);
					g.PaintWithAlpha (layer.Opacity);

					if (layer == PintaCore.Layers.CurrentLayer && PintaCore.LivePreview.IsEnabled) {
						cr.Render (PintaCore.LivePreview.LivePreviewSurface, canvas, canvas_bounds.Location, checker);

						g.Save ();
						g.Scale (scale, scale);
						g.AppendPath (PintaCore.Layers.SelectionPath);
						g.Clip ();

						g.Scale (1 / scale, 1 / scale);
						g.SetSourceSurface (canvas, canvas_bounds.X, canvas_bounds.Y);
						g.PaintWithAlpha (layer.Opacity);

						g.Restore ();
					}

					checker = false;
				}

				// If we are at least 200% and grid is requested, draw it
				if (PintaCore.Actions.View.PixelGrid.Active && cr.ScaleFactor.Ratio <= 0.5d) {
					gr.Render (canvas, canvas_bounds.Location);
					g.SetSourceSurface (canvas, canvas_bounds.X, canvas_bounds.Y);
					g.Paint ();
				}

				// Selection outline
				if (PintaCore.Layers.ShowSelection) {
					g.Save ();
					g.Translate (0.5, 0.5);
					g.Scale (scale, scale);

					g.AppendPath (PintaCore.Layers.SelectionPath);

					if (PintaCore.Tools.CurrentTool.Name.Contains ("Select") && !PintaCore.Tools.CurrentTool.Name.Contains ("Selected")) {
						g.Color = new Cairo.Color (0.7, 0.8, 0.9, 0.2);
						g.FillRule = Cairo.FillRule.EvenOdd;
						g.FillPreserve ();
					}

					g.SetDash (new double[] { 2 / scale, 4 / scale }, 0);
					g.LineWidth = 1 / scale;
					g.Color = new Cairo.Color (0, 0, 0);

					g.Stroke ();
					g.Restore ();
				}
			}			
			
			return true;
		}
	}
}