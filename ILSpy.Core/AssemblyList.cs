﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.Xml.Linq;

namespace ICSharpCode.ILSpy
{
	/// <summary>
	/// A list of assemblies.
	/// </summary>
	public sealed class AssemblyList
	{
		/// <summary>Dirty flag, used to mark modifications so that the list is saved later</summary>
		bool dirty;

		internal readonly ConcurrentDictionary<(string assemblyName, bool isWinRT), LoadedAssembly> assemblyLookupCache = new ConcurrentDictionary<(string assemblyName, bool isWinRT), LoadedAssembly>();
		internal readonly ConcurrentDictionary<string, LoadedAssembly> moduleLookupCache = new ConcurrentDictionary<string, LoadedAssembly>();

		/// <summary>
		/// The assemblies in this list.
		/// Needs locking for multi-threaded access!
		/// Write accesses are allowed on the GUI thread only (but still need locking!)
		/// </summary>
		/// <remarks>
		/// Technically read accesses need locking when done on non-GUI threads... but whenever possible, use the
		/// thread-safe <see cref="GetAssemblies()"/> method.
		/// </remarks>
		internal readonly ObservableCollection<LoadedAssembly> assemblies = new ObservableCollection<LoadedAssembly>();
		
		public AssemblyList(string listName)
		{
			this.ListName = listName;
			assemblies.CollectionChanged += Assemblies_CollectionChanged;
		}
		
		/// <summary>
		/// Loads an assembly list from XML.
		/// </summary>
		public AssemblyList(XElement listElement)
			: this((string)listElement.Attribute("name"))
		{
			foreach (var asm in listElement.Elements("Assembly")) {
				OpenAssembly((string)asm);
			}
			this.dirty = false; // OpenAssembly() sets dirty, so reset it afterwards
		}
		
		/// <summary>
		/// Gets the loaded assemblies. This method is thread-safe.
		/// </summary>
		public LoadedAssembly[] GetAssemblies()
		{
			lock (assemblies) {
				return assemblies.ToArray();
			}
		}
		
		/// <summary>
		/// Saves this assembly list to XML.
		/// </summary>
		internal XElement SaveAsXml()
		{
			lock (assemblies)
			{
				return new XElement(
					"List",
					new XAttribute("name", this.ListName),
					assemblies.Where(asm => !asm.IsAutoLoaded).Select(asm => new XElement("Assembly", asm.FileName))
				);
			}
		}
		
		/// <summary>
		/// Gets the name of this list.
		/// </summary>
		public string ListName { get; }

		void Assemblies_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			ClearCache();
			// Whenever the assembly list is modified, mark it as dirty
			// and enqueue a task that saves it once the UI has finished modifying the assembly list.
			if (dirty) return;
			dirty = true;
			Dispatcher.UIThread.InvokeAsync(
				new Action(
					delegate {
						dirty = false;
						AssemblyListManager.SaveList(this);
						ClearCache();
					}),
				DispatcherPriority.Background
			);
		}

		internal void RefreshSave()
		{
			if (dirty) return;
			dirty = true;
			Dispatcher.UIThread.InvokeAsync(
				delegate {
					dirty = false;
					AssemblyListManager.SaveList(this);
				},
				DispatcherPriority.Background
			);
		}
		
		internal void ClearCache()
		{
			assemblyLookupCache.Clear();
		}

		public LoadedAssembly Open(string assemblyUri, bool isAutoLoaded = false)
		{
			var fileName = assemblyUri["nupkg://".Length..];
			if (!assemblyUri.StartsWith("nupkg://", StringComparison.OrdinalIgnoreCase))
				return OpenAssembly(assemblyUri, isAutoLoaded);
			var separator = fileName.LastIndexOf(';');
			string componentName = null;
			if (separator <= -1) return null;
			componentName = fileName[(separator + 1)..];
			fileName = fileName[..separator];
			var package = new LoadedNugetPackage(fileName);
			var entry = package.Entries.FirstOrDefault(e => e.Name == componentName);
			return entry != null ? OpenAssembly(assemblyUri, entry.Stream, true) : null;

		}

		/// <summary>
		/// Opens an assembly from disk.
		/// Returns the existing assembly node if it is already loaded.
		/// </summary>
		public LoadedAssembly OpenAssembly(string file, bool isAutoLoaded = false)
		{
			Dispatcher.UIThread.VerifyAccess();
			
			file = Path.GetFullPath(file);
			
			lock (assemblies)
			{
				foreach (var asm in this.assemblies) {
					if (file.Equals(asm.FileName, StringComparison.OrdinalIgnoreCase))
						return asm;
				}
			}
			
			var newAsm = new LoadedAssembly(this, file)
			{
				IsAutoLoaded = isAutoLoaded
			};
			lock (assemblies) {
				this.assemblies.Add(newAsm);
			}
			return newAsm;
		}

		/// <summary>
		/// Opens an assembly from a stream.
		/// </summary>
		public LoadedAssembly OpenAssembly(string file, Stream stream, bool isAutoLoaded = false)
		{
			Dispatcher.UIThread.VerifyAccess();

			lock (assemblies)
			{
				foreach (var asm in this.assemblies) {
					if (file.Equals(asm.FileName, StringComparison.OrdinalIgnoreCase))
						return asm;
				}
			}

			var newAsm = new LoadedAssembly(this, file, stream)
			{
				IsAutoLoaded = isAutoLoaded
			};
			lock (assemblies) {
				this.assemblies.Add(newAsm);
			}
			return newAsm;
		}

		/// <summary>
		/// Replace the assembly object model from a crafted stream, without disk I/O
		/// Returns null if it is not already loaded.
		/// </summary>
		public LoadedAssembly HotReplaceAssembly(string file, Stream stream)
		{
			Dispatcher.UIThread.VerifyAccess();
			file = Path.GetFullPath(file);

			var target = this.assemblies.FirstOrDefault(asm => file.Equals(asm.FileName, StringComparison.OrdinalIgnoreCase));
			if (target == null)
				return null;

			var index = this.assemblies.IndexOf(target);
			var newAsm = new LoadedAssembly(this, file, stream)
			{
				IsAutoLoaded = target.IsAutoLoaded
			};
			lock (assemblies) {
				this.assemblies.Remove(target);
				this.assemblies.Insert(index, newAsm);
			}
			return newAsm;
		}

		public LoadedAssembly ReloadAssembly(string file)
		{
			Dispatcher.UIThread.VerifyAccess();
			file = Path.GetFullPath(file);

			var target = this.assemblies.FirstOrDefault(asm => file.Equals(asm.FileName, StringComparison.OrdinalIgnoreCase));
			if (target == null)
				return null;

			var index = this.assemblies.IndexOf(target);
			var newAsm = new LoadedAssembly(this, file)
			{
				IsAutoLoaded = target.IsAutoLoaded
			};
			lock (assemblies) {
				this.assemblies.Remove(target);
				this.assemblies.Insert(index, newAsm);
			}
			return newAsm;
		}
		
		public void Unload(LoadedAssembly assembly)
		{
			Dispatcher.UIThread.VerifyAccess();
			lock (assemblies) {
				assemblies.Remove(assembly);
			}
			RequestGC();
		}
		
		static bool gcRequested;
		
		void RequestGC()
		{
			if (gcRequested) return;
			gcRequested = true;
			Dispatcher.UIThread.InvokeAsync(new Action(
				delegate {
					gcRequested = false;
					GC.Collect();
				}), DispatcherPriority.ContextIdle);
		}
		
		public void Sort(IComparer<LoadedAssembly> comparer)
		{
			Sort(0, int.MaxValue, comparer);
		}
		
		public void Sort(int index, int count, IComparer<LoadedAssembly> comparer)
		{
			Dispatcher.UIThread.VerifyAccess();
			var list = new List<LoadedAssembly>(assemblies);
			lock (assemblies) {
				list.Sort(index, Math.Min(count, list.Count - index), comparer);
				assemblies.Clear();
				assemblies.AddRange(list);
			}
		}
	}
}
