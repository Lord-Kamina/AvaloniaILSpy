using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using AvaloniaEdit;

namespace ICSharpCode.ILSpy.TextView
{
    public class CopyCommand<T> : ICommand
    {
        public bool CanExecute(object parameter)=> true;
        private readonly  T _textEditor;
        public CopyCommand(T editor)
        {
            _textEditor = editor;
        }
        public void Execute(object parameter)
        {
            if (_textEditor is TextEditor editor)
            {
                Dispatcher.UIThread.InvokeAsync(() => {
                    editor.Copy();
                });
            }
        }

        public event EventHandler CanExecuteChanged;
    }
}