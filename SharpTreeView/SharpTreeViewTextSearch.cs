// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Threading;

namespace ICSharpCode.TreeView
{
	/// <summary>
	/// Custom TextSearch-implementation.
	/// Fixes #67 - Moving to class member in tree view by typing in first character of member name selects parent assembly
	/// </summary>
	public class SharpTreeViewTextSearch : AvaloniaObject
	{
		const double doubleClickTime = 0.1;

		//static readonly DependencyPropertyKey TextSearchInstancePropertyKey = AvaloniaProperty.RegisterAttachedReadOnly("TextSearchInstance",
		//	typeof(SharpTreeViewTextSearch), typeof(SharpTreeViewTextSearch), new FrameworkPropertyMetadata(null));
		static readonly AttachedProperty<SharpTreeViewTextSearch> TextSearchInstanceProperty = AvaloniaProperty.RegisterAttached<SharpTreeView, SharpTreeViewTextSearch, SharpTreeViewTextSearch>("TextSearchInstance");
		//static readonly StyledProperty<SharpTreeViewTextSearch> TextSearchInstanceProperty = TextSearchInstancePropertyKey.DependencyProperty;

		DispatcherTimer timer;

		bool isActive;
		int lastMatchIndex;
		string matchPrefix;

		readonly Stack<string> inputStack;
		readonly SharpTreeView treeView;

		private SharpTreeViewTextSearch(SharpTreeView treeView)
		{
			this.treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
			inputStack = new Stack<string>(8);
			ClearState();
		}

		public static SharpTreeViewTextSearch GetInstance(SharpTreeView sharpTreeView)
		{
			var textSearch = (SharpTreeViewTextSearch)sharpTreeView.GetValue(TextSearchInstanceProperty);
			if (textSearch != null) return textSearch;
			textSearch = new SharpTreeViewTextSearch(sharpTreeView);
			sharpTreeView.SetValue(TextSearchInstanceProperty, textSearch);
			return textSearch;
		}

		public bool RevertLastCharacter()
		{
			if (!isActive || inputStack.Count == 0)
				return false;
			matchPrefix = matchPrefix.Substring(0, matchPrefix.Length - inputStack.Pop().Length);
			ResetTimeout();
			return true;
		}

		public bool Search(string nextChar)
		{
			var items = (IList)treeView.Items;
			var startIndex = isActive ? lastMatchIndex : Math.Max(0, treeView.SelectedIndex);
			var lookBackwards = inputStack.Count > 0 && string.Compare(inputStack.Peek(), nextChar, StringComparison.OrdinalIgnoreCase) == 0;
			var nextMatchIndex = IndexOfMatch(matchPrefix + nextChar, startIndex, lookBackwards, out var wasNewCharUsed);
			if (nextMatchIndex != -1) {
				if (!isActive || nextMatchIndex != startIndex) {
					treeView.SelectedItem = items[nextMatchIndex];
					treeView.FocusNode((SharpTreeNode)treeView.SelectedItem);
					lastMatchIndex = nextMatchIndex;
				}
				if (wasNewCharUsed) {
					matchPrefix += nextChar;
					inputStack.Push(nextChar);
				}
				isActive = true;
			}
			if (isActive) {
				ResetTimeout();
			}
			return nextMatchIndex != -1;
		}

		int IndexOfMatch(string needle, int startIndex, bool tryBackward, out bool charWasUsed)
		{
			var items = (IList)treeView.Items;
			charWasUsed = false;
			if (items.Count == 0 || string.IsNullOrEmpty(needle))
				return -1;
			var index = -1;
			var fallbackIndex = -1;
			var fallbackMatch = false;
			var i = startIndex;
			var comparisonType = treeView.IsTextSearchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			do {
				var item = (SharpTreeNode)items[i];
				if (item?.Text != null) {
					var text = item.Text.ToString();
					if (text.StartsWith(needle, comparisonType)) {
						charWasUsed = true;
						index = i;
						break;
					}
					if (tryBackward) {
						if (fallbackMatch && matchPrefix != string.Empty) {
							if (fallbackIndex == -1 && text.StartsWith(matchPrefix, comparisonType)) {
								fallbackIndex = i;
							}
						} else {
							fallbackMatch = true;
						}
					}
				}
				i++;
				if (i >= items.Count)
					i = 0;
			} while (i != startIndex);
			return index == -1 ? fallbackIndex : index;
		}

		void ClearState()
		{
			isActive = false;
			matchPrefix = string.Empty;
			lastMatchIndex = -1;
			inputStack.Clear();
			timer?.Stop();
			timer = null;
		}

		void ResetTimeout()
		{
			if (timer == null) {
				timer = new DispatcherTimer(DispatcherPriority.Normal);
				timer.Tick += (sender, e) => ClearState();
			} else {
				timer.Stop();
			}
			timer.Interval = TimeSpan.FromMilliseconds(doubleClickTime * 2);
			timer.Start();
		}
	}
}