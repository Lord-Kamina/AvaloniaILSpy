// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using Avalonia.Controls.Primitives;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;

namespace ICSharpCode.TreeView
{
	public class GeneralAdorner : VisualLayerManager
	{
		public GeneralAdorner()
		{
		}

		protected override Size MeasureOverride(Size constraint)
		{
			if (Child == null) return new Size();
			Child.Measure(constraint);
			return Child.DesiredSize;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			if (Child == null) return new Size();
			Child.Arrange(new Rect(finalSize));
			return finalSize;
		}
	}
}
