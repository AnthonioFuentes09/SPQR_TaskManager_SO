using SPQR.Core.Dominio;
using SPQR.Core.Estructuras;
using SPQR.Core.Paginacion;

namespace SPQR.Core.Simulacion;

/// <summary>Una fila de la tabla comparativa del informe.</summary>
public sealed record FilaComparacion
{
    public required string Algoritmo { get; init; }
    public required string Criterio { get; init; }
    public required int Referencias { get; init; }
    public required int Fallos { get; init; }
    public required int Desalojos { get; init; }
    public required int EscriturasADisco { get; init; }

    public double F => Referencias == 0 ? 0 : Math.Round((double)Fallos / Referencias, 4);
    public double RendimientoPorcentual => Math.Round((1 - F) * 100, 2);
}

/// <summary>
/// Corre la MISMA configuración con los seis algoritmos de paginación y arma
/// la tabla comparativa. Como cada corrida ya deja su lista de eventos y sus
/// contadores, comparar es contar: no hay lógica nueva acá.
///
/// El Óptimo siempre encabeza la tabla — es imposible superarlo — y sirve
/// para medir qué tan cerca quedó el algoritmo elegido.
/// </summary>
public static class ComparadorAlgoritmos
{
    public static List<FilaComparacion> CompararPaginacion(
        ConfiguracionSO config, ListaEnlazada<Proceso> procesos)
    {
        var filas = new List<FilaComparacion>();
        var original = config.AlgoritmoPaginacion;

        foreach (var (clave, titulo) in FabricaPaginacion.Disponibles)
        {
            config.AlgoritmoPaginacion = clave;
            var resultado = Simulador.Ejecutar(config, procesos);

            filas.Add(new FilaComparacion
            {
                Algoritmo = titulo,
                Criterio = FabricaPaginacion.Crear(clave).Criterio,
                Referencias = resultado.Mmu.ReferenciasTotales,
                Fallos = resultado.Mmu.FallosDePagina,
                Desalojos = resultado.Mmu.Desalojos,
                EscriturasADisco = resultado.Mmu.EscriturasADisco,
            });
        }

        config.AlgoritmoPaginacion = original;
        filas.Sort((a, b) => b.RendimientoPorcentual.CompareTo(a.RendimientoPorcentual));
        return filas;
    }

    /// <summary>
    /// Demuestra la anomalía de Belady: corre el mismo escenario variando la
    /// cantidad de marcos. En FIFO puede haber MÁS fallos con más marcos.
    /// </summary>
    public static List<(int Marcos, int Fallos)> CurvaDeFallos(
        ConfiguracionSO config, ListaEnlazada<Proceso> procesos, int desde, int hasta)
    {
        var curva = new List<(int, int)>();
        var original = config.MarcosFisicos;

        for (var m = desde; m <= hasta; m++)
        {
            config.MarcosFisicos = m;
            var r = Simulador.Ejecutar(config, procesos);
            curva.Add((m, r.Mmu.FallosDePagina));
        }

        config.MarcosFisicos = original;
        return curva;
    }
}
