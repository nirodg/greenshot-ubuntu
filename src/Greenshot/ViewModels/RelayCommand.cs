using System.Windows.Input;

namespace Greenshot.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Func<Task>? _asyncAction;
    private readonly Action? _action;

    public RelayCommand(Action action) => _action = action;
    public RelayCommand(Func<Task> asyncAction) => _asyncAction = asyncAction;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        if (_asyncAction != null)
            _ = _asyncAction();
        else
            _action?.Invoke();
    }
}
