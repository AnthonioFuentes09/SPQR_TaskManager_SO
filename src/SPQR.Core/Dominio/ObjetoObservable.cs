using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SPQR.Core.Dominio;

/// <summary>
/// Base mínima para que las clases del dominio avisen cuando cambian.
///
/// Sin esto, todo valor DERIVADO que se muestre en pantalla —la suma de
/// ráfagas, la relación entre páginas y marcos— se calcula una vez al abrir
/// la ventana y queda congelado para siempre, aunque el usuario edite los
/// campos de los que depende. Es la causa de que un número en la interfaz
/// mienta sin que nadie se dé cuenta.
///
/// La interfaz no se mete en el núcleo; es el núcleo el que avisa.
/// </summary>
public abstract class ObjetoObservable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Notificar([CallerMemberName] string? propiedad = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));

    /// <summary>Asigna y avisa. Devuelve true si el valor cambió de verdad.</summary>
    protected bool Asignar<T>(ref T campo, T valor, [CallerMemberName] string? propiedad = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor)) return false;
        campo = valor;
        Notificar(propiedad);
        return true;
    }
}
