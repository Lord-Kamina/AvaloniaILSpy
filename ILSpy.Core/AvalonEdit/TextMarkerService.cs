// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace ICSharpCode.ILSpy.AvaloniaEdit
{
	using TextView = global::AvaloniaEdit.Rendering.TextView;
	/// <summary>
	/// Handles the text markers for a code editor.
	/// </summary>
	sealed class TextMarkerService : DocumentColorizingTransformer, IBackgroundRenderer, ITextMarkerService
	{
		TextSegmentCollection<TextMarker> markers;
		TextView textView;
		
		public TextMarkerService(TextView textView)
		{
            ArgumentNullException.ThrowIfNull(textView);
            this.textView = textView;
			textView.DocumentChanged += OnDocumentChanged;
			OnDocumentChanged(null, null);
		}

		void OnDocumentChanged(object sender, EventArgs e)
		{
			markers = textView.Document != null ? new TextSegmentCollection<TextMarker>(textView.Document) : null;
		}
		
		#region ITextMarkerService
		public ITextMarker Create(int startOffset, int length)
		{
			if (markers == null)
				throw new InvalidOperationException("Cannot create a marker when not attached to a document");
			
			var textLength = textView.Document.TextLength;
			if (startOffset < 0 || startOffset > textLength)
				throw new ArgumentOutOfRangeException(nameof(startOffset), startOffset, "Value must be between 0 and " + textLength);
			if (length < 0 || startOffset + length > textLength)
				throw new ArgumentOutOfRangeException(nameof(length), length, "length must not be negative and startOffset+length must not be after the end of the document");
			
			var m = new TextMarker(this, startOffset, length);
			markers.Add(m);
			// no need to mark segment for redraw: the text marker is invisible until a property is set
			return m;
		}
		
		public IEnumerable<ITextMarker> GetMarkersAtOffset(int offset)
		{
			return markers == null ? Enumerable.Empty<ITextMarker>() : markers.FindSegmentsContaining(offset);
		}
		
		public IEnumerable<ITextMarker> TextMarkers => markers ?? Enumerable.Empty<ITextMarker>();

		public void RemoveAll(Predicate<ITextMarker> predicate)
		{
            ArgumentNullException.ThrowIfNull(predicate);
            if (markers == null) return;
            foreach (var m in markers.ToArray()) {
				if (predicate(m))
					Remove(m);
			}
		}
		
		public void Remove(ITextMarker marker)
		{
            ArgumentNullException.ThrowIfNull(marker);
            var m = marker as TextMarker;
            if (markers == null || !markers.Remove(m)) return;
            Redraw(m);
			m.OnDeleted();
		}
		
		/// <summary>
		/// Redraws the specified text segment.
		/// </summary>
		internal void Redraw(ISegment segment)
		{
			textView.Redraw(segment, DispatcherPriority.Normal);
			RedrawRequested?.Invoke(this, EventArgs.Empty);
		}
		
		public event EventHandler RedrawRequested;
		#endregion
		
		#region DocumentColorizingTransformer
		protected override void ColorizeLine(DocumentLine line)
		{
			if (markers == null)
				return;
			int lineStart = line.Offset;
			int lineEnd = lineStart + line.Length;
			foreach (TextMarker marker in markers.FindOverlappingSegments(lineStart, line.Length)) {
				Brush foregroundBrush = null;
				if (marker.ForegroundColor != null) {
					foregroundBrush = new SolidColorBrush(marker.ForegroundColor.Value);
					//foregroundBrush.Freeze();
				}
				ChangeLinePart(
					Math.Max(marker.StartOffset, lineStart),
					Math.Min(marker.EndOffset, lineEnd),
					element => {
						if (foregroundBrush != null) {
							element.TextRunProperties.ForegroundBrush = foregroundBrush;
						}
						// TODO: change font style
						//string tf = element.TextRunProperties.Typeface;
						//element.TextRunProperties.SetTypeface(new Typeface(
						//	tf.FontFamily,
						//	marker.FontStyle ?? tf.Style,
						//	marker.FontWeight ?? tf.Weight,
						//	tf.Stretch
						//));
					}
				);
			}
		}
		#endregion
		
		#region IBackgroundRenderer
		public KnownLayer Layer {
			get {
				// draw behind selection
				return KnownLayer.Selection;
			}
		}
		
		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			if (drawingContext == null)
				throw new ArgumentNullException(nameof(drawingContext));
			if (markers == null || !textView.VisualLinesValid)
				return;
			var visualLines = textView.VisualLines;
			if (visualLines.Count == 0)
				return;
			int viewStart = visualLines.First().FirstDocumentLine.Offset;
			int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;
			foreach (TextMarker marker in markers.FindOverlappingSegments(viewStart, viewEnd - viewStart)) {
				if (marker.BackgroundColor != null) {
					BackgroundGeometryBuilder geoBuilder = new BackgroundGeometryBuilder();
					geoBuilder.AlignToWholePixels = true;
					geoBuilder.CornerRadius = 3;
					geoBuilder.AddSegment(textView, marker);
					Geometry geometry = geoBuilder.CreateGeometry();
					if (geometry != null) {
						Color color = marker.BackgroundColor.Value;
						SolidColorBrush brush = new SolidColorBrush(color);
						//brush.Freeze();
						drawingContext.DrawGeometry(brush, null, geometry);
					}
				}
				var underlineMarkerTypes = TextMarkerTypes.SquigglyUnderline | TextMarkerTypes.NormalUnderline | TextMarkerTypes.DottedUnderline;
				if ((marker.MarkerTypes & underlineMarkerTypes) != 0) {
					foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker)) {
						Point startPoint = r.BottomLeft;
						Point endPoint = r.BottomRight;
						
						Brush usedBrush = new SolidColorBrush(marker.MarkerColor);
						//usedBrush.Freeze();
						if ((marker.MarkerTypes & TextMarkerTypes.SquigglyUnderline) != 0) {
							double offset = 2.5;
							
							int count = Math.Max((int)((endPoint.X - startPoint.X) / offset) + 1, 4);
							
							StreamGeometry geometry = new StreamGeometry();
							
							using (StreamGeometryContext ctx = geometry.Open()) {
								ctx.BeginFigure(startPoint, false);
								foreach (var point in CreatePoints(startPoint, endPoint, offset, count)) {
									ctx.LineTo(point);
								}
							}
							
							//geometry.Freeze();
							
							Pen usedPen = new Pen(usedBrush, 1);
							//usedPen.Freeze();
							drawingContext.DrawGeometry(Brushes.Transparent, usedPen, geometry);
						}
						if ((marker.MarkerTypes & TextMarkerTypes.NormalUnderline) != 0) {
							Pen usedPen = new Pen(usedBrush, 1);
							//usedPen.Freeze();
							drawingContext.DrawLine(usedPen, startPoint, endPoint);
						}
						if ((marker.MarkerTypes & TextMarkerTypes.DottedUnderline) != 0) {
							Pen usedPen = new Pen(usedBrush, 1, DashStyle.Dot);
							//usedPen.Freeze();
							drawingContext.DrawLine(usedPen, startPoint, endPoint);
						}
					}
				}
			}
		}
		
		IEnumerable<Point> CreatePoints(Point start, Point end, double offset, int count)
		{
			for (int i = 0; i < count; i++)
				yield return new Point(start.X + i * offset, start.Y - ((i + 1) % 2 == 0 ? offset : 0));
		}
		#endregion
	}
	
	sealed class TextMarker : TextSegment, ITextMarker
	{
		readonly TextMarkerService service;
		
		public TextMarker(TextMarkerService service, int startOffset, int length)
		{
			this.service = service ?? throw new ArgumentNullException(nameof(service));
			this.StartOffset = startOffset;
			this.Length = length;
			this.markerTypes = TextMarkerTypes.None;
		}
		
		public event EventHandler Deleted;
		
		public bool IsDeleted => !IsConnectedToCollection;

		public void Delete()
		{
			service.Remove(this);
		}
		
		internal void OnDeleted()
		{
			Deleted?.Invoke(this, EventArgs.Empty);
		}
		
		void Redraw()
		{
			service.Redraw(this);
		}
		
		Color? backgroundColor;
		
		public Color? BackgroundColor {
			get => backgroundColor;
			set
			{
				if (backgroundColor.GetValueOrDefault().ToUint32() == value.GetValueOrDefault().ToUint32()) return;
				backgroundColor = value;
				Redraw();
			}
		}
		
		Color? foregroundColor;
		
		public Color? ForegroundColor {
			get => foregroundColor;
			set
			{
				if (foregroundColor.GetValueOrDefault().ToUint32() == value.GetValueOrDefault().ToUint32()) return;
				foregroundColor = value;
				Redraw();
			}
		}
		
		FontWeight? fontWeight;
		
		public FontWeight? FontWeight {
			get => fontWeight;
			set
			{
				if (fontWeight == value) return;
				fontWeight = value;
				Redraw();
			}
		}
		
		FontStyle? fontStyle;
		
		public FontStyle? FontStyle {
			get => fontStyle;
			set
			{
				if (fontStyle == value) return;
				fontStyle = value;
				Redraw();
			}
		}
		
		public object Tag { get; set; }
		
		TextMarkerTypes markerTypes;
		
		public TextMarkerTypes MarkerTypes {
			get => markerTypes;
			set
			{
				if (markerTypes == value) return;
				markerTypes = value;
				Redraw();
			}
		}
		
		Color markerColor;
		
		public Color MarkerColor {
			get => markerColor;
			set
			{
				if (markerColor.ToUint32() == value.ToUint32()) return;
				markerColor = value;
				Redraw();
			}
		}
		
		public object ToolTip { get; set; }
	}
}
