
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using CodeImp.DoomBuilder.Interface;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Controls;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	[EditMode(SwitchAction = "verticesmode",	// Action name used to switch to this mode
			  ButtonDesc = "Vertices Mode",		// Description on the button in toolbar/menu
		      ButtonImage = "VerticesMode.png",	// Image resource name for the button
			  ButtonOrder = int.MinValue + 0)]	// Position of the button (lower is more to the left)

	public class VerticesMode : ClassicMode
	{
		#region ================== Constants

		public const float VERTEX_HIGHLIGHT_RANGE = 20f;
		public const float LINEDEF_SPLIT_RANGE = 8f;

		#endregion

		#region ================== Variables

		// Highlighted item
		protected Vertex highlighted;

		// Interface
		private bool editpressed;

		#endregion

		#region ================== Properties

		#endregion

		#region ================== Constructor / Disposer

		// Constructor
		public VerticesMode()
		{
		}

		// Disposer
		public override void Dispose()
		{
			// Not already disposed?
			if(!isdisposed)
			{
				// Clean up

				// Dispose base
				base.Dispose();
			}
		}

		#endregion

		#region ================== Methods

		// Cancel mode
		public override void OnCancel()
		{
			base.OnCancel();
			
			// Return to this mode
			General.Map.ChangeMode(new VerticesMode());
		}

		// Mode engages
		public override void OnEngage()
		{
			base.OnEngage();
			renderer.SetPresentation(Presentation.Standard);
		}

		// Mode disengages
		public override void OnDisengage()
		{
			base.OnDisengage();

			// Check which mode we are switching to
			if(General.Map.NewMode is LinedefsMode)
			{
				// Convert selection to linedefs

				// Clear selected vertices
				General.Map.Map.ClearSelectedVertices();
			}
			else if(General.Map.NewMode is SectorsMode)
			{
				// Convert selection to sectors
				
				// Clear selected vertices
				General.Map.Map.ClearSelectedVertices();
			}

			// Hide highlight info
			General.Interface.HideInfo();
		}

		// This redraws the display
		public override void OnRedrawDisplay()
		{
			// Render lines and vertices
			if(renderer.StartPlotter(true))
			{
				renderer.PlotLinedefSet(General.Map.Map.Linedefs);
				renderer.PlotVerticesSet(General.Map.Map.Vertices);
				if((highlighted != null) && !highlighted.IsDisposed)
					renderer.PlotVertex(highlighted, ColorCollection.HIGHLIGHT);
				renderer.Finish();
			}

			// Render things
			if(renderer.StartThings(true))
			{
				renderer.RenderThingSet(General.Map.Map.Things);
				renderer.Finish();
			}

			// Selecting?
			if(selecting)
			{
				// Render selection
				if(renderer.StartOverlay(true))
				{
					RenderMultiSelection();
					renderer.Finish();
				}
			}

			renderer.Present();
		}
		
		// This highlights a new item
		protected void Highlight(Vertex v)
		{
			// Update display
			if(renderer.StartPlotter(false))
			{
				// Undraw previous highlight
				if((highlighted != null) && !highlighted.IsDisposed)
					renderer.PlotVertex(highlighted, renderer.DetermineVertexColor(highlighted));

				// Set new highlight
				highlighted = v;

				// Render highlighted item
				if((highlighted != null) && !highlighted.IsDisposed)
					renderer.PlotVertex(highlighted, ColorCollection.HIGHLIGHT);
				
				// Done
				renderer.Finish();
				renderer.Present();
			}

			// Show highlight info
			if((highlighted != null) && !highlighted.IsDisposed)
				General.Interface.ShowVertexInfo(highlighted);
			else
				General.Interface.HideInfo();
		}
		
		// Selection
		protected override void OnSelect()
		{
			// Item highlighted?
			if((highlighted != null) && !highlighted.IsDisposed)
			{
				// Flip selection
				highlighted.Selected = !highlighted.Selected;

				// Redraw highlight to show selection
				if(renderer.StartPlotter(false))
				{
					renderer.PlotVertex(highlighted, renderer.DetermineVertexColor(highlighted));
					renderer.Finish();
					renderer.Present();
				}
			}
			else
			{
				// Start making a selection
				StartMultiSelection();
			}

			base.OnSelect();
		}
		
		// End selection
		protected override void OnEndSelect()
		{
			// Not stopping from multiselection?
			if(!selecting)
			{
				// Item highlighted?
				if((highlighted != null) && !highlighted.IsDisposed)
				{
					// Render highlighted item
					if(renderer.StartPlotter(false))
					{
						renderer.PlotVertex(highlighted, ColorCollection.HIGHLIGHT);
						renderer.Finish();
						renderer.Present();
					}
				}
			}

			base.OnEndSelect();
		}
		
		// Start editing
		protected override void OnEdit()
		{
			// Vertex highlighted?
			if((highlighted != null) && !highlighted.IsDisposed)
			{
				// Edit pressed in this mode
				editpressed = true;
			}
			else
			{
				// Find the nearest linedef within highlight range
				Linedef l = General.Map.Map.NearestLinedefRange(mousemappos, LINEDEF_SPLIT_RANGE / renderer.Scale);

				// Found a line?
				if(l != null)
				{
					// Create undo
					General.Map.UndoRedo.CreateUndo("Split linedef", UndoGroup.None, 0);

					// Create vertex at nearest point on line
					Vector2D nearestpos = l.NearestOnLine(mousemappos);
					Vertex v = General.Map.Map.CreateVertex(nearestpos);

					// Snap to map format accuracy
					v.SnapToAccuracy();

					// Split the line with this vertex
					l.Split(v);

					// Highlight it
					Highlight(v);

					// Redraw display
					General.Interface.RedrawDisplay();
				}
				else
				{
					// Start drawing mode
					DrawGeometryMode drawmode = new DrawGeometryMode();
					drawmode.DrawPointAt(mousemappos, true);
					General.Map.ChangeMode(drawmode);
				}
			}
			
			base.OnEdit();
		}
		
		// Done editing
		protected override void OnEndEdit()
		{
			editpressed = false;
			base.OnEndEdit();
		}
		
		// Mouse moves
		public override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			// Not holding any buttons?
			if(e.Button == MouseButtons.None)
			{
				// Find the nearest vertex within highlight range
				Vertex v = General.Map.Map.NearestVertexSquareRange(mousemappos, VERTEX_HIGHLIGHT_RANGE / renderer.Scale);

				// Highlight if not the same
				if(v != highlighted) Highlight(v);
			}
		}

		// Mouse leaves
		public override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			
			// Highlight nothing
			Highlight(null);
		}

		// Mouse wants to drag
		protected override void OnDragStart(MouseEventArgs e)
		{
			base.OnDragStart(e);

			// Edit button used?
			if(General.Interface.CheckActionActive(null, "classicedit"))
			{
				// Anything highlighted?
				if((highlighted != null) && !highlighted.IsDisposed)
				{
					// Highlighted item not selected?
					if(!highlighted.Selected)
					{
						// Select only this vertex for dragging
						General.Map.Map.ClearSelectedVertices();
						highlighted.Selected = true;
					}

					// Start dragging the selection
					General.Map.ChangeMode(new DragVerticesMode(new VerticesMode(), highlighted, mousedownmappos));
				}
			}
		}

		// This is called wheh selection ends
		protected override void OnEndMultiSelection()
		{
			// Go for all vertices
			foreach(Vertex v in General.Map.Map.Vertices)
			{
				v.Selected = ((v.Position.x >= selectionrect.Left) &&
							  (v.Position.y >= selectionrect.Top) &&
							  (v.Position.x <= selectionrect.Right) &&
							  (v.Position.y <= selectionrect.Bottom));
			}

			base.OnEndMultiSelection();

			// Clear overlay
			if(renderer.StartOverlay(true)) renderer.Finish();

			// Redraw
			General.Interface.RedrawDisplay();
		}

		// This is called when the selection is updated
		protected override void OnUpdateMultiSelection()
		{
			base.OnUpdateMultiSelection();

			// Render selection
			if(renderer.StartOverlay(true))
			{
				RenderMultiSelection();
				renderer.Finish();
				renderer.Present();
			}
		}
		
		#endregion

		#region ================== Actions

		[BeginAction("deleteitem", BaseAction = true)]
		public void DeleteItem()
		{
			// Make list of selected vertices
			ICollection<Vertex> selected = General.Map.Map.GetSelectedVertices(true);
			if((selected.Count == 0) && (highlighted != null) && !highlighted.IsDisposed) selected.Add(highlighted);

			// Anything to do?
			if(selected.Count > 0)
			{
				// Make undo
				if(selected.Count > 1)
					General.Map.UndoRedo.CreateUndo("Delete " + selected.Count + " vertices", UndoGroup.None, 0);
				else
					General.Map.UndoRedo.CreateUndo("Delete vertex", UndoGroup.None, 0);

				// Dispose selected vertices
				foreach(Vertex v in selected) v.Dispose();

				// Update cache values
				General.Map.Map.Update();

				// Invoke a new mousemove so that the highlighted item updates
				MouseEventArgs e = new MouseEventArgs(MouseButtons.None, 0, (int)mousepos.x, (int)mousepos.y, 0);
				OnMouseMove(e);

				// Redraw screen
				General.Interface.RedrawDisplay();
			}
		}

		#endregion
	}
}
