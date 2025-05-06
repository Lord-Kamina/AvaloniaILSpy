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
using System.Threading.Tasks;

using AvaloniaEdit.Highlighting;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.TreeNodes;

namespace ICSharpCode.ILSpy.Xaml
{
	[Export(typeof(IResourceNodeFactory))]
	sealed class XmlResourceNodeFactory : IResourceNodeFactory
	{
		private static readonly string[] xmlFileExtensions = { ".xml", ".xsd", ".xslt" };

		public ILSpyTreeNode CreateNode(Resource resource)
		{
			var stream = resource.TryOpenStream();
			return stream == null ? null : CreateNode(resource.Name, stream);
		}
		
		public ILSpyTreeNode CreateNode(string key, object data)
		{
			if (!(data is Stream stream))
			    return null;
			return xmlFileExtensions.Any(fileExt => key.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase)) ? new XmlResourceEntryNode(key, stream) : null;
		}
	}
	
	sealed class XmlResourceEntryNode : ResourceEntryNode
	{
		string xml;
		
		public XmlResourceEntryNode(string key, Stream data)
			: base(key, data)
		{
		}
		
		public override object Icon
		{
			get
			{
				var text = (string)Text;
				if (text.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
					return Images.ResourceXml;
				if (text.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
					return Images.ResourceXsd;
				return text.EndsWith(".xslt", StringComparison.OrdinalIgnoreCase) ? Images.ResourceXslt : Images.Resource;
			}
		}

		public override bool View(DecompilerTextView textView)
		{
			var output = new AvaloniaEditTextOutput();
			IHighlightingDefinition highlighting = null;
			
			textView.RunWithCancellation(
				token => Task.Factory.StartNew(
					() => {
						try {
							// cache read XAML because stream will be closed after first read
							if (xml == null)
							{
								using var reader = new StreamReader(Data);
								xml = reader.ReadToEnd();
							}
							output.Write(xml);
							highlighting = HighlightingManager.Instance.GetDefinitionByExtension(".xml");
						}
						catch (Exception ex) {
							output.Write(ex.ToString());
						}
						return output;
					}, token)
			).Then(t => textView.ShowNode(t, this, highlighting)).HandleExceptions();
			return true;
		}
	}
}
