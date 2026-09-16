namespace SPQR.Core.Paginacion;

/// <summary>
/// Reloj. Los marcos se ven como una lista CIRCULAR fija y hay una manecilla
/// que apunta a uno de ellos. Al necesitar una víctima:
///   · si el marco apuntado tiene R = 0 → es la víctima, y la manecilla avanza
///     una posición;
///   · si tiene R = 1 → se le apaga el bit y la manecilla avanza, sin mover
///     el marco de lugar.
///
/// Diferencia con segunda oportunidad: el criterio de decisión es idéntico,
/// pero acá NADA se reordena. No hay que sacar un nodo de la cabeza y
/// reinsertarlo al final; solo avanza un índice. Es la misma idea implementada
/// con un costo menor, y por eso se cuentan como dos algoritmos y no como uno.
/// </summary>
public sealed class RelojPaginacion : IAlgoritmoPaginacion
{
    private int _manecilla;

    public string Nombre => "Reloj";
    public string Criterio => "Lista circular de marcos con una manecilla: R = 0 es víctima, R = 1 se apaga y se avanza.";

    public void Reiniciar() => _manecilla = 0;
    public void AlCargar(int marco, int instante) { }
    public void AlReferenciar(int marco, int instante) { }

    public int ElegirVictima(ContextoReemplazo c)
    {
        var n = c.Marcos.Length;
        if (n == 0) return 0;

        foreach (var marco in c.Marcos) if (marco.Libre) return marco.Numero;

        // A lo sumo dos vueltas: la primera apaga bits, la segunda encuentra víctima.
        for (var pasos = 0; pasos < n * 2; pasos++)
        {
            var actual = c.Marcos[_manecilla % n];
            var indice = _manecilla % n;
            _manecilla = (_manecilla + 1) % n;

            if (actual.Duenio is null) return indice;

            ref var entrada = ref actual.Duenio.TablaDePaginas[actual.Pagina];
            if (!entrada.Referenciada) return indice;

            entrada.Referenciada = false;   // segunda oportunidad, sin reordenar
        }
        return _manecilla % n;
    }
}
