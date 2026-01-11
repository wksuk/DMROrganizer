using System;
using System.Windows.Input;

namespace DMROrganizer.Helpers;

/// <summary>
/// Simple ICommand implementation that delegates execute and can-execute behavior.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action execute)
        : this(_ => execute(), _ => true)
    {
    }

    public RelayCommand(Action execute, Func<bool> canExecute)
        : this(_ => execute(), _ => canExecute())
    {
    }

    public RelayCommand(Action<object?> execute)
        : this(execute, _ => true)
    {
    }

    public RelayCommand(Action<object?> execute, Predicate<object?> canExecute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Forces WPF to re-query CanExecute for bound commands.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
