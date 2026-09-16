namespace SPQR.Core.Estructuras;

/// <summary>
/// Nodo de una lista enlazada. La rúbrica del proyecto exige el uso de
/// listas enlazadas con apuntadores, así que el núcleo no usa List&lt;T&gt;.
/// </summary>
public sealed class Nodo<T>
{
    public T Valor { get; set; }
    public Nodo<T>? Siguiente { get; set; }

    public Nodo(T valor) => Valor = valor;
}
