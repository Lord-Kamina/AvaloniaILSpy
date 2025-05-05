// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
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
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.ILSpy.Properties;
using ICSharpCode.ILSpy.TextView;

namespace ICSharpCode.ILSpy.TreeNodes
{
	[Export(typeof(IResourceNodeFactory))]
	sealed class CursorResourceNodeFactory : IResourceNodeFactory
	{
		private static readonly string[] imageFileExtensions = { ".cur" };

		public ILSpyTreeNode CreateNode(Resource resource)
		{
			var stream = resource.TryOpenStream();
			return stream == null ? null : CreateNode(resource.Name, stream);
		}

		public ILSpyTreeNode CreateNode(string key, object data)
		{
			if (!(data is Stream stream))
			    return null;
			return imageFileExtensions.Any(fileExt => key.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase)) ? new CursorResourceEntryNode(key, stream) : null;
		}
	}

	sealed class CursorResourceEntryNode : ResourceEntryNode
	{
		public CursorResourceEntryNode(string key, Stream data)
			: base(key, data)
		{
		}

		public override object Icon => Images.ResourceImage;

		public override bool View(DecompilerTextView textView)
		{
			try {
				var output = new AvaloniaEditTextOutput();
				Data.Position = 0;
                Bitmap image;

                //HACK: windows imaging does not understand that .cur files have the same layout as .ico
                // so load to data, and modify the ResourceType in the header to make look like an icon...
                if (!(Data is MemoryStream s))
				{
					// data was stored in another stream type (e.g. PinnedBufferedMemoryStream)
					s = new MemoryStream();
					Data.CopyTo(s);
				}
				var curData = s.ToArray();
				curData[2] = 1;
				using (Stream stream = new MemoryStream(curData)) {
                    image = new Bitmap(stream);
                }

				output.AddUIElement(() => new Image { Source = image });
				output.WriteLine();
                output.AddButton(Images.Save, Resources.Save, async delegate {
                    await Save(null);
                });
                textView.ShowNode(output, this);
				return true;
			}
			catch (Exception) {
				return false;
			}
		}
	}
}
