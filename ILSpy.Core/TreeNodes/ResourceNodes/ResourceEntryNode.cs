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
using System.Threading.Tasks;
using Avalonia.Controls;
using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy.TextView;
using Microsoft.Win32;

namespace ICSharpCode.ILSpy.TreeNodes
{
	/// <summary>
	/// Entry in a .resources file
	/// </summary>
	public class ResourceEntryNode : ILSpyTreeNode
	{
		private readonly string key;

		public override object Text => this.key;

		public override object Icon => Images.Resource;

		protected Stream Data { get; }


		public ResourceEntryNode(string key, Stream data)
		{
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(data);
            this.key = key;
			this.Data = data;
		}

		public static ILSpyTreeNode Create(string key, object data)
		{
			ILSpyTreeNode result = null;
			foreach (var factory in App.ExportProvider.GetExportedValues<IResourceNodeFactory>()) {
				result = factory.CreateNode(key, data);
				if (result != null)
					return result;
			}

			if(data is Stream streamData)
				result =  new ResourceEntryNode(key, streamData);

			return result;
		}

		public override void Decompile(Language language, ITextOutput output, DecompilationOptions options)
		{
			language.WriteCommentLine(output, $"{key} = {Data}");
		}

		public override async Task<bool> Save(DecompilerTextView textView)
		{
			var dlg = new SaveFileDialog
			{
				Title = "Save file",
				InitialFileName = Path.GetFileName(DecompilerTextView.CleanUpName(key))
			};
			var filename = await dlg.ShowAsync(App.Current.GetMainWindow());
			if (string.IsNullOrEmpty(filename)) return true;
			Data.Position = 0;
			await using var fs = File.OpenWrite(filename);
			await Data.CopyToAsync(fs);
			return true;
		}
	}
}
