namespace SPQR.Core.Paginacion;

/// <summary>
/// LRU — «la menos usada recientemente». Desaloja la página que lleva más
/// tiempo sin tocarse. Apuesta a la localidad temporal: lo que se usó hace un
/// instante probablemente se vuelva a usar enseguida.
///
/// Es el que más se acerca al Óptimo, porque hace con el pasado lo mismo que
/// el Óptimo hace con el futuro: mira la distancia en el tiempo. La diferencia
/// es que LRU sí es implementable, aunque en hardware real resulta caro —
/// habría que estampar cada acceso. Reloj y segunda oportunidad existen
/// justamente como aproximaciones baratas a LRU.
///
/// Acá se lleva un sello de tiempo por marco, actualizado en cada acierto.
/// </summary>
public sealed class LruPaginacion : IAlgoritmoPaginacion
{
    private int[] _ultimoUso = [];

    public string Nombre => "LRU";
    public string Criterio => "Desaloja la página que lleva más tiempo sin usarse. Es la mejor aproximación al óptimo.";

    public void Reiniciar() => _ultimoUso = [];

    public void AlCargar(int marco, int instante) => Sellar(marco, instante);
    public void AlReferenciar(int marco, int instante) => Sellar(marco, instante);

    public int ElegirVictima(ContextoReemplazo c)
    {
        Asegurar(c.Marcos.Length);
        foreach (var marco in c.Marcos) if (marco.Libre) return marco.Numero;

        var victima = 0;
        var masViejo = int.MaxValue;
        foreach (var marco in c.Marcos)
        {
            if (_ultimoUso[marco.Numero] < masViejo)
            {
                masViejo = _ultimoUso[marco.Numero];
                victima = marco.Numero;
            }
        }
        return victima;
    }

    private void Sellar(int marco, int instante)
    {
        Asegurar(marco + 1);
        _ultimoUso[marco] = instante;
    }

    private void Asegurar(int tamanio)
    {
        if (_ultimoUso.Length >= tamanio) return;
        var nuevo = new int[tamanio];
        Array.Copy(_ultimoUso, nuevo, _ultimoUso.Length);
        _ultimoUso = nuevo;
    }
}
