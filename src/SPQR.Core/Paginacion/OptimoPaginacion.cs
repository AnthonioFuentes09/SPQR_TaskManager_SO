using SPQR.Core.Memoria;

namespace SPQR.Core.Paginacion;

/// <summary>
/// Algoritmo óptimo (OPT / MIN, de Belady). Desaloja la página que tardará
/// MÁS en volver a usarse.
///
/// EMPATE — la regla del docente (clase del 1 de septiembre): cuando varias
/// páginas ya no vuelven a aparecer en el futuro, sale «el primer proceso que
/// tuvo la primera referencia a un marco», es decir, la que ENTRÓ PRIMERO a
/// memoria. En su ejemplo (1,2,3,3,5,1,2,2,6,2,1,5,7,6,3 · 4 marcos) eso
/// decide los dos últimos reemplazos: con el 7 sale el 1, y con el 3 sale el
/// 2 —«del 7, 2, 6, 5, la primera que se dio el 2»—, no el marco de número
/// más bajo, que era lo que hacía esta clase antes.
///
/// En un sistema operativo real es imposible, porque nadie conoce el futuro.
/// Acá sí se puede porque el emulador genera la cadena de referencias
/// completa en una primera pasada y la MMU corre en una segunda: por eso el
/// contexto trae el campo <c>Futuro</c>. Sirve como COTA SUPERIOR, la vara
/// contra la que se miden los otros cinco.
/// </summary>
public sealed class OptimoPaginacion : IAlgoritmoPaginacion
{
    public string Nombre => "Óptimo";
    public string Criterio =>
        "Desaloja la página que tardará más en volver a usarse. Si varias no vuelven nunca, sale la que entró primero.";

    public void Reiniciar() { }
    public void AlCargar(int marco, int instante) { }
    public void AlReferenciar(int marco, int instante) { }

    public int ElegirVictima(ContextoReemplazo c)
    {
        foreach (var marco in c.Marcos) if (marco.Libre) return marco.Numero;

        // 1 · ¿hay páginas que no vuelven a usarse nunca? Sale la más antigua.
        var sinFuturo = -1;
        var cargaMasVieja = int.MaxValue;
        foreach (var marco in c.Marcos)
        {
            if (ProximoUso(c, marco) != int.MaxValue) continue;
            var carga = CargadaEn(marco);
            if (carga < cargaMasVieja)
            {
                cargaMasVieja = carga;
                sinFuturo = marco.Numero;
            }
        }
        if (sinFuturo >= 0) return sinFuturo;

        // 2 · todas vuelven: sale la que vuelve más tarde.
        var victima = 0;
        var distanciaMayor = -1;
        foreach (var marco in c.Marcos)
        {
            var distancia = ProximoUso(c, marco);
            if (distancia > distanciaMayor)
            {
                distanciaMayor = distancia;
                victima = marco.Numero;
            }
        }
        return victima;
    }

    private static int CargadaEn(MarcoFisico marco)
        => marco.Duenio is null ? int.MaxValue : marco.Duenio.TablaDePaginas[marco.Pagina].InstanteDeCarga;

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
