using System.Collections;

namespace SPQR.Core.Estructuras;

/// <summary>
/// Lista simplemente enlazada con apuntador a cabeza y a cola.
/// Se usa para: el catálogo de programas, la lista de ejecución,
/// las colas de listos del planificador, la lista de marcos libres
/// y el orden de carga del algoritmo FIFO de paginación.
/// </summary>
public sealed class ListaEnlazada<T> : IEnumerable<T>
{
    private Nodo<T>? _cabeza;
    private Nodo<T>? _cola;

    public int Cantidad { get; private set; }
    public bool EstaVacia => _cabeza is null;

    public void AgregarAlFinal(T valor)
    {
        var nodo = new Nodo<T>(valor);
        if (_cola is null) { _cabeza = _cola = nodo; }
        else { _cola.Siguiente = nodo; _cola = nodo; }
        Cantidad++;
    }

    public void AgregarAlInicio(T valor)
    {
        var nodo = new Nodo<T>(valor) { Siguiente = _cabeza };
        _cabeza = nodo;
        _cola ??= nodo;
        Cantidad++;
    }

    public T? QuitarDelInicio()
    {
        if (_cabeza is null) return default;
        var valor = _cabeza.Valor;
        _cabeza = _cabeza.Siguiente;
        if (_cabeza is null) _cola = null;
        Cantidad--;
        return valor;
    }

    public T? VerPrimero() => _cabeza is null ? default : _cabeza.Valor;

    public bool Quitar(Func<T, bool> criterio)
    {
        Nodo<T>? anterior = null;
        var actual = _cabeza;
        while (actual is not null)
        {
            if (criterio(actual.Valor))
            {
                if (anterior is null) _cabeza = actual.Siguiente;
                else anterior.Siguiente = actual.Siguiente;
                if (ReferenceEquals(actual, _cola)) _cola = anterior;
                Cantidad--;
                return true;
            }
            anterior = actual;
            actual = actual.Siguiente;
        }
        return false;
    }

    /// <summary>Quita y devuelve el elemento que minimiza la clave dada.</summary>
    public T? QuitarMenor(Func<T, long> clave)
    {
        if (_cabeza is null) return default;
        var mejor = _cabeza;
        for (var n = _cabeza.Siguiente; n is not null; n = n.Siguiente)
            if (clave(n.Valor) < clave(mejor.Valor)) mejor = n;
        var valor = mejor.Valor;
        Quitar(v => ReferenceEquals(v, valor) || Equals(v, valor));
        return valor;
    }

    public void Limpiar() { _cabeza = _cola = null; Cantidad = 0; }

    public IEnumerator<T> GetEnumerator()
    {
        for (var n = _cabeza; n is not null; n = n.Siguiente) yield return n.Valor;
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
