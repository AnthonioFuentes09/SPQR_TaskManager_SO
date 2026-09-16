using SPQR.Core.Memoria;

namespace SPQR.Core.Paginacion;

/// <summary>
/// Algoritmo óptimo (OPT / MIN, de Belady). Desaloja la página que tardará
/// MÁS en volver a usarse; si alguna no vuelve a usarse nunca, esa es la
/// víctima inmediata.
///
/// En un sistema operativo real es imposible, porque nadie conoce el futuro.
/// Acá sí se puede porque el emulador genera la cadena de referencias
/// completa en una primera pasada y la MMU corre en una segunda: por eso el
/// contexto trae el campo <c>Futuro</c>. No sirve para usarlo en producción;
/// sirve como COTA SUPERIOR, la vara contra la que se miden los otros cinco.
/// </summary>
public sealed class OptimoPaginacion : IAlgoritmoPaginacion
{
    public string Nombre => "Óptimo";
    public string Criterio => "Desaloja la página que tardará más en volver a usarse. Cota superior teórica.";

    public void Reiniciar() { }
    public void AlCargar(int marco, int instante) { }
    public void AlReferenciar(int marco, int instante) { }

    public int ElegirVictima(ContextoReemplazo c)
    {
        var victima = -1;
        var distanciaMayor = -1;

        foreach (var marco in c.Marcos)
        {
            if (marco.Libre) return marco.Numero;

            var distancia = ProximoUso(c, marco);
            if (distancia == int.MaxValue) return marco.Numero;   // no vuelve a usarse nunca

            if (distancia > distanciaMayor)
            {
                distanciaMayor = distancia;
                victima = marco.Numero;
            }
        }
        return victima < 0 ? 0 : victima;
    }

    /// <summary>Cuántas referencias faltan para que esta página vuelva a tocarse.</summary>
    private static int ProximoUso(ContextoReemplazo c, MarcoFisico marco)
    {
        var d = 0;
        foreach (var (procesoId, pagina) in c.Futuro)
        {
            if (marco.Duenio is not null && marco.Duenio.Id == procesoId && marco.Pagina == pagina)
                return d;
            d++;
        }
        return int.MaxValue;
    }
}
