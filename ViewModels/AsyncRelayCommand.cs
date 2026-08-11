using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MarkdownEditor.ViewModels;

/// <summary>
/// Async-capable ICommand implementation that prevents reentrancy
/// while the command is executing.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (_isExecuting)
        {
            return false;
        }

        return _canExecute == null || _canExecute();
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync().ConfigureAwait(true);
    }

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();

            await _executeAsync().ConfigureAwait(true);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Generic async ICommand that accepts a parameter.
/// </summary>
public sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _executeAsync;
    private readonly Predicate<T?>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> executeAsync, Predicate<T?>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (_isExecuting)
        {
            return false;
        }

        return _canExecute == null || _canExecute(CastParameter(parameter));
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync(CastParameter(parameter)).ConfigureAwait(true);
    }

    public async Task ExecuteAsync(T? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();

            await _executeAsync(parameter).ConfigureAwait(true);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static T? CastParameter(object? parameter)
    {
        if (parameter is T typed)
        {
            return typed;
        }

        return default;
    }
}