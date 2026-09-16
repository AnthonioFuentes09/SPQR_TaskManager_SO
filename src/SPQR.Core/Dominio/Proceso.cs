namespace SPQR.Core.Dominio;

/// <summary>
/// Una instancia de un programa dentro de la lista de ejecución.
/// Lleva todos los registros que consultan los algoritmos.
/// </summary>
public sealed class Proceso
{
    public required int Id { get; init; }
    public required Programa Programa { get; init; }

    public string Nombre => $"{Programa.Nombre} ({Id})";

    public int TiempoLlegada { get; set; }
    public int Rafaga { get; set; }
    public int Cola { get; set; } = 1;

    public int Prioridad => Programa.Prioridad;
    public int Boletos => Programa.Boletos;
    public int PorcentajeAsignacion => Programa.PorcentajeAsignacion;

    // --- estado durante la simulación ---
    public EstadoProceso Estado { get; set; } = EstadoProceso.Nuevo;
    public int TiempoRestante { get; set; }
    public int QuantumConsumido { get; set; }
    public int CpuRecibida { get; set; }
    public int? InstantePrimeraEjecucion { get; set; }
    public int? InstanteFinalizacion { get; set; }

    /// <summary>Remanente entre la ráfaga y el quantum, que el docente pidió guardar.</summary>
    public int RemanenteDeQuantum { get; set; }

    public TablaDePaginas TablaDePaginas { get; } = new();

    public int? TiempoRetorno => InstanteFinalizacion is null ? null : InstanteFinalizacion - TiempoLlegada;
    public int? TiempoEspera   => TiempoRetorno is null ? null : TiempoRetorno - Rafaga;
    public int? TiempoRespuesta => InstantePrimeraEjecucion is null ? null : InstantePrimeraEjecucion - TiempoLlegada;

    public void Reiniciar()
    {
        Estado = EstadoProceso.Nuevo;
        TiempoRestante = Rafaga;
        QuantumConsumido = 0;
        CpuRecibida = 0;
        InstantePrimeraEjecucion = null;
        InstanteFinalizacion = null;
        RemanenteDeQuantum = 0;
        TablaDePaginas.Reiniciar(Programa.TamanioEnPaginas);
    }
}
