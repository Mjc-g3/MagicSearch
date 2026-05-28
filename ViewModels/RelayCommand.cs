using System;
using System.Windows.Input;

namespace MagicSearch.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action execute)
        : this(_ => execute(), null)
    {
    }

    public RelayCommand(Action<object?> execute)
        : this(execute, null)
    {
    }

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}