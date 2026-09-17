namespace SPQR.Core.Dominio;

/// <summary>
/// Una instancia de un programa dentro de la lista de ejecución.
/// Lleva todos los registros que consultan los algoritmos.
/// </summary>
public sealed class Proceso : ObjetoObservable
{
    private Programa _programa = null!;
    private int _tiempoLlegada;
    private int _rafaga = 1;
    private int _cola = 1;

    public required int Id { get; init; }

    /// <summary>
    /// El programa del que esta instancia es una ejecución. Se puede cambiar
    /// después de creada: la fila de la lista de ejecución ofrece el catálogo
    /// completo en un desplegable.
    /// </summary>
    public required Programa Programa
    {
        get => _programa;
        set
        {
            var anterior = _programa;
            if (!Asignar(ref _programa, value)) return;

            // El nombre del proceso se arma con el del programa, así que hay
            // que seguirle el rastro al programa que tenga en cada momento.
            if (anterior is not null) anterior.PropertyChanged -= AlCambiarElPrograma;
            if (_programa is not null) _programa.PropertyChanged += AlCambiarElPrograma;

            Notificar(nameof(Nombre));
            Notificar(nameof(Prioridad));
            Notificar(nameof(Boletos));
            Notificar(nameof(PorcentajeAsignacion));
        }
    }

    private void AlCambiarElPrograma(object? _, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Dominio.Programa.Nombre)) Notificar(nameof(Nombre));
        if (e.PropertyName == nameof(Dominio.Programa.Prioridad)) Notificar(nameof(Prioridad));
        if (e.PropertyName == nameof(Dominio.Programa.Boletos)) Notificar(nameof(Boletos));
        if (e.PropertyName == nameof(Dominio.Programa.PorcentajeAsignacion)) Notificar(nameof(PorcentajeAsignacion));
    }

    public string Nombre => $"{Programa.Nombre} ({Id})";

    public int TiempoLlegada
    {
        get => _tiempoLlegada;
        set => Asignar(ref _tiempoLlegada, Math.Max(0, value));
    }

    /// <summary>Tiempo de CPU que necesita. Una ráfaga de cero no es un proceso.</summary>
    public int Rafaga
    {
        get => _rafaga;
        set => Asignar(ref _rafaga, Math.Max(1, value));
    }

    /// <summary>Cola a la que pertenece cuando el algoritmo es «múltiples colas».</summary>
    public int Cola
    {
        get => _cola;
        set => Asignar(ref _cola, Math.Max(1, value));
    }

    public int Prioridad => Programa.Prioridad;
    public int Boletos => Programa.Boletos;
    public int PorcentajeAsignacion => Programa.PorcentajeAsignacion;

    // --- estado durante la simulación (no se edita desde la pantalla) ---
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
