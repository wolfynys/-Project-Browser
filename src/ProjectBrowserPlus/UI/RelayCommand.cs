using System;
using System.Windows.Input;

namespace ProjectBrowserPlus.UI
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object> _run;
        private readonly Func<object, bool> _can;
        public RelayCommand(Action<object> run, Func<object, bool> can = null) { _run = run; _can = can; }
        public RelayCommand(Action run, Func<bool> can = null) { _run = _ => run(); if (can != null) _can = _ => can(); }
        public bool CanExecute(object p) => _can == null || _can(p);
        public void Execute(object p) => _run(p);
        public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
    }
}
