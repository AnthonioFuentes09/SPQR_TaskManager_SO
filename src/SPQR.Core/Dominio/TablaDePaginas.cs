namespace SPQR.Core.Dominio;

/// <summary>Una entrada de la tabla de páginas: la referencia entre página y marco.</summary>
public struct EntradaPagina
{
    public bool Presente;
    public int  Marco;
    public bool Referenciada;   // bit R
    public bool Modificada;     // bit M
    public int  InstanteDeCarga;
    public int  InstanteUltimoUso;

    /// <summary>Clase de NRU: 0 = ni R ni M, 1 = solo M, 2 = solo R, 3 = R y M.</summary>
    public readonly int Clase => (Referenciada ? 2 : 0) + (Modificada ? 1 : 0);
}

/// <summary>Tabla de páginas de un proceso. Una entrada por página virtual.</summary>
public sealed class TablaDePaginas
{
    private EntradaPagina[] _entradas = [];

    public int Cantidad => _entradas.Length;
    public ref EntradaPagina this[int pagina] => ref _entradas[pagina];

    public void Reiniciar(int cantidadPaginas)
    {
        _entradas = new EntradaPagina[cantidadPaginas];
        for (var i = 0; i < cantidadPaginas; i++)
            _entradas[i] = new EntradaPagina { Presente = false, Marco = -1 };
    }

    public IEnumerable<(int Pagina, EntradaPagina Entrada)> Recorrer()
    {
        for (var i = 0; i < _entradas.Length; i++) yield return (i, _entradas[i]);
    }
}
