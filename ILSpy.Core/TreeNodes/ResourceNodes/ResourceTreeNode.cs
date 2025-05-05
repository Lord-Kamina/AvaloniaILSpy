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
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Utils;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.ILSpy.Properties;
using ICSharpCode.ILSpy.TextView;
using Microsoft.Win32;

namespace ICSharpCode.ILSpy.TreeNodes
{
	/// <summary>
	/// This is the default resource entry tree node, which is used if no specific
	/// <see cref="IResourceNodeFactory"/> exists for the given resource type. 
	/// </summary>
	public class ResourceTreeNode : ILSpyTreeNode
	{
		readonly Resource r;
		
		public ResourceTreeNode(Resource r)
		{
            ArgumentNullException.ThrowIfNull(r);
            this.r = r;
		}
		
		public Resource Resource => r;

		public override object Text => r.Name;

		public override object Icon => Images.Resource;

		public override FilterResult Filter(FilterSettings settings)
		{
			if (settings.ShowApiLevel == ApiVisibility.PublicOnly && (r.Attributes & ManifestResourceAttributes.VisibilityMask) == ManifestResourceAttributes.Private)
                return FilterResult.Hidden;
			return settings.SearchTermMatches(r.Name) ? FilterResult.Match : FilterResult.Hidden;
		}
		
		public override void Decompile(Language language, ITextOutput output, DecompilationOptions options)
		{
			language.WriteCommentLine(output, $"{r.Name} ({r.ResourceType}, {r.Attributes})");

			if (!(output is ISmartTextOutput smartOutput)) return;
			smartOutput.AddButton(Images.Save, Resources.Save, delegate { Save(MainWindow.Instance.TextView); });
			output.WriteLine();
		}
		
		public override bool View(DecompilerTextView textView)
		{
			var s = Resource.TryOpenStream();
			if (s == null || s.Length >= DecompilerTextView.DefaultOutputLengthLimit) return false;
			s.Position = 0;
			var type = GuessFileType.DetectFileType(s);
			if (type == FileType.Binary) return false;
			s.Position = 0;
			var output = new AvaloniaEditTextOutput();
			output.Write(new StreamReader(s, Encoding.UTF8).ReadToEnd());
			var ext = type == FileType.Xml ? ".xml" : Path.GetExtension(DecompilerTextView.CleanUpName(Resource.Name));
			textView.ShowNode(output, this, HighlightingManager.Instance.GetDefinitionByExtension(ext));
			return true;
		}
		
	    public override async Task<bool> Save(DecompilerTextView textView)
		{
			var s = Resource.TryOpenStream();
			if (s == null)
				return false;
            var dlg = new SaveFileDialog
            {
	            Title = "Save file",
	            InitialFileName = DecompilerTextView.CleanUpName(Resource.Name)
            };
            var filename = await dlg.ShowAsync(App.Current.GetMainWindow());
            if (string.IsNullOrEmpty(filename)) return true;
            s.Position = 0;
            await using var fs = File.OpenWrite(filename);
            await s.CopyToAsync(fs);
            return true;
		}
		
		public static ILSpyTreeNode Create(Resource resource)
		{
			ILSpyTreeNode result = null;
			foreach (var factory in App.ExportProvider.GetExportedValues<IResourceNodeFactory>()) {
				result = factory.CreateNode(resource);
				if (result != null)
					break;
			}
			return result ?? new ResourceTreeNode(resource);
		}
	}
}
