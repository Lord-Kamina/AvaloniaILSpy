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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Util;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.TreeView;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.Controls;
using Microsoft.Win32;
using System.Threading.Tasks;
using Avalonia.Controls;
using ICSharpCode.ILSpy.Properties;

namespace ICSharpCode.ILSpy.TreeNodes
{
	[Export(typeof(IResourceNodeFactory))]
	sealed class ResourcesFileTreeNodeFactory : IResourceNodeFactory
	{
		public ILSpyTreeNode CreateNode(Resource resource)
		{
			return resource.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) ? new ResourcesFileTreeNode(resource) : null;
		}

		public ILSpyTreeNode CreateNode(string key, object data)
		{
			return null;
		}
	}

	sealed class ResourcesFileTreeNode : ResourceTreeNode
	{
		readonly ICollection<KeyValuePair<string, string>> stringTableEntries = new ObservableCollection<KeyValuePair<string, string>>();
		readonly ICollection<SerializedObjectRepresentation> otherEntries = new ObservableCollection<SerializedObjectRepresentation>();

		public ResourcesFileTreeNode(Resource er)
			: base(er)
		{
			this.LazyLoading = true;
		}

		public override object Icon => Images.ResourceResourcesFile;

		protected override void LoadChildren()
		{
			var s = Resource.TryOpenStream();
			if (s == null) return;
			s.Position = 0;
			try {
                foreach (var entry in new ResourcesFile(s).OrderBy(e => e.Key, NaturalStringComparer.Instance)) {
					ProcessResourceEntry(entry);
				}
			} catch (BadImageFormatException) {
                // ignore errors
            }
            catch (EndOfStreamException)
            {
                // ignore errors
            }
        }

		private void ProcessResourceEntry(KeyValuePair<string, object> entry)
		{
			switch (entry.Value)
			{
				case string value:
					stringTableEntries.Add(new KeyValuePair<string, string>(entry.Key, value));
					return;
				case byte[] bytes:
					Children.Add(ResourceEntryNode.Create(entry.Key, new MemoryStream(bytes)));
					return;
			}

			var node = ResourceEntryNode.Create(entry.Key, entry.Value);
			if (node != null) {
				Children.Add(node);
				return;
			}

			switch (entry.Value)
			{
				case null:
					otherEntries.Add(new SerializedObjectRepresentation(entry.Key, "null", ""));
					break;
				case ResourceSerializedObject so:
					otherEntries.Add(new SerializedObjectRepresentation(entry.Key, so.TypeName, "<serialized>"));
					break;
				default:
					otherEntries.Add(new SerializedObjectRepresentation(entry.Key, entry.Value.GetType().FullName, entry.Value.ToString()));
					break;
			}
		}

		public override async Task<bool> Save(DecompilerTextView textView)
		{
			var s = Resource.TryOpenStream();
			if (s == null) return false;
            var dlg = new SaveFileDialog
            {
	            Title = "Save file",
	            InitialFileName = DecompilerTextView.CleanUpName(Resource.Name),
	            Filters = new List<FileDialogFilter>()
	            {
		            new FileDialogFilter(){ Name="Resources file(*.resources)", Extensions = { "resources" } },
		            new FileDialogFilter(){ Name="Resource XML file(*.resx)", Extensions = { "resx" } }
	            }
            };
            var filename = await dlg.ShowAsync(App.Current.GetMainWindow());
            if (string.IsNullOrEmpty(filename)) return true;
            s.Position = 0;
            if (filename.Contains("resources"))
            {
	            await using var fs = File.OpenWrite(filename);
	            await s.CopyToAsync(fs);
            } else {
	            try
	            {
		            using var writer = new ResXResourceWriter(File.OpenWrite(filename));
		            foreach (var entry in new ResourcesFile(s))
		            {
			            writer.AddResource(entry.Key, entry.Value);
		            }
	            }
	            catch (BadImageFormatException)
	            {
		            // ignore errors
	            }
	            catch (EndOfStreamException)
	            {
		            // ignore errors
	            }
            }
            return true;
        }


        public override void Decompile(Language language, ITextOutput output, DecompilationOptions options)
		{
			EnsureLazyChildren();
			base.Decompile(language, output, options);
			if (stringTableEntries.Count != 0) {
				var smartOutput = output as ISmartTextOutput;
				smartOutput?.AddUIElement(() =>
					new ResourceStringTable(stringTableEntries, MainWindow.Instance.mainPane));
				output.WriteLine();
				output.WriteLine();
			}

			if (otherEntries.Count == 0) return;
			var smartOutput1 = output as ISmartTextOutput;
			{
				smartOutput1?.AddUIElement(() => new ResourceObjectTable(otherEntries, MainWindow.Instance.mainPane));
				output.WriteLine();
			}
		}

		internal class SerializedObjectRepresentation
		{
			public SerializedObjectRepresentation(string key, string type, string value)
			{
				this.Key = key;
				this.Type = type;
				this.Value = value;
			}

			public string Key { get; private set; }
			public string Type { get; private set; }
			public string Value { get; private set; }
		}
	}
}
