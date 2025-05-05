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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace ICSharpCode.ILSpy.Options
{
    /// <summary>
    /// Description of DisplaySettings.
    /// </summary>
    public class DisplaySettings : INotifyPropertyChanged
    {
        public DisplaySettings()
        {
        }

        #region INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        FontFamily selectedFont;

        public FontFamily SelectedFont
        {
            get => selectedFont;
            set
            {
                if (selectedFont == value) return;
                selectedFont = value;
                OnPropertyChanged();
            }
        }

        double selectedFontSize;

        public double SelectedFontSize
        {
            get => selectedFontSize;
            set
            {
                if (selectedFontSize != value)
                {
                    selectedFontSize = value;
                    OnPropertyChanged();
                }
            }
        }

        bool showLineNumbers;

        public bool ShowLineNumbers
        {
            get => showLineNumbers;
            set
            {
                if (showLineNumbers == value) return;
                showLineNumbers = value;
                OnPropertyChanged();
            }
        }

        bool showMetadataTokens;

        public bool ShowMetadataTokens
        {
            get => showMetadataTokens;
            set
            {
                if (showMetadataTokens == value) return;
                showMetadataTokens = value;
                OnPropertyChanged();
            }
        }

        bool showMetadataTokensInBase10;

        public bool ShowMetadataTokensInBase10
        {
            get => showMetadataTokensInBase10;
            set
            {
                if (showMetadataTokensInBase10 == value) return;
                showMetadataTokensInBase10 = value;
                OnPropertyChanged();
            }
        }

        bool enableWordWrap;

        public bool EnableWordWrap
        {
            get => enableWordWrap;
            set
            {
                if (enableWordWrap == value) return;
                enableWordWrap = value;
                OnPropertyChanged();
            }
        }

        bool sortResults = true;

        public bool SortResults
        {
            get => sortResults;
            set
            {
                if (sortResults == value) return;
                sortResults = value;
                OnPropertyChanged();
            }
        }

        bool foldBraces = false;

        public bool FoldBraces
        {
            get => foldBraces;
            set
            {
                if (foldBraces == value) return;
                foldBraces = value;
                OnPropertyChanged();
            }
        }

        bool expandMemberDefinitions = false;

        public bool ExpandMemberDefinitions
        {
            get => expandMemberDefinitions;
            set
            {
                if (expandMemberDefinitions == value) return;
                expandMemberDefinitions = value;
                OnPropertyChanged();
            }
        }

        bool expandUsingDeclarations = false;

        public bool ExpandUsingDeclarations
        {
            get => expandUsingDeclarations;
            set
            {
                if (expandUsingDeclarations == value) return;
                expandUsingDeclarations = value;
                OnPropertyChanged();
            }
        }

        bool showDebugInfo;

        public bool ShowDebugInfo
        {
            get => showDebugInfo;
            set
            {
                if (showDebugInfo == value) return;
                showDebugInfo = value;
                OnPropertyChanged();
            }
        }

        bool indentationUseTabs = true;

        public bool IndentationUseTabs
        {
            get => indentationUseTabs;
            set
            {
                if (indentationUseTabs == value) return;
                indentationUseTabs = value;
                OnPropertyChanged();
            }
        }

        int indentationTabSize = 4;

        public int IndentationTabSize
        {
            get => indentationTabSize;
            set
            {
                if (indentationTabSize == value) return;
                indentationTabSize = value;
                OnPropertyChanged();
            }
        }

        int indentationSize = 4;

        public int IndentationSize
        {
            get => indentationSize;
            set
            {
                if (indentationSize == value) return;
                indentationSize = value;
                OnPropertyChanged();
            }
        }

        bool highlightMatchingBraces = true;

        public bool HighlightMatchingBraces
        {
            get => highlightMatchingBraces;
            set
            {
                if (highlightMatchingBraces == value) return;
                highlightMatchingBraces = value;
                OnPropertyChanged();
            }
        }

        public void CopyValues(DisplaySettings s)
        {
            this.SelectedFont = s.selectedFont;
            this.SelectedFontSize = s.selectedFontSize;
            this.ShowLineNumbers = s.showLineNumbers;
            this.ShowMetadataTokens = s.showMetadataTokens;
            this.ShowMetadataTokensInBase10 = s.showMetadataTokensInBase10;
            this.ShowDebugInfo = s.showDebugInfo;
            this.EnableWordWrap = s.enableWordWrap;
            this.SortResults = s.sortResults;
            this.FoldBraces = s.foldBraces;
            this.ExpandMemberDefinitions = s.expandMemberDefinitions;
            this.ExpandUsingDeclarations = s.expandUsingDeclarations;
            this.IndentationUseTabs = s.indentationUseTabs;
            this.IndentationTabSize = s.indentationTabSize;
            this.IndentationSize = s.indentationSize;
            this.HighlightMatchingBraces = s.highlightMatchingBraces;
        }
    }
}
