using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace SPQR.UI.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Notificar([CallerMemberName] string? propiedad = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));

    protected bool Asignar<T>(ref T campo, T valor, [CallerMemberName] string? propiedad = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor)) return false;
        campo = valor;
        Notificar(propiedad);
        return true;
    }
}

/// <summary>Comando mínimo, para no depender de paquetes externos.</summary>
public sealed class Comando(Action ejecutar, Func<bool>? puede = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parametro) => puede?.Invoke() ?? true;
    public void Execute(object? parametro) => ejecutar();
    public void Revisar() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
