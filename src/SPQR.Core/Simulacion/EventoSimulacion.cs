using SPQR.Core.Dominio;

namespace SPQR.Core.Simulacion;

/// <summary>
/// Una entrada del log: todo lo que ocurrió en un instante.
/// El docente pidió expresamente poder ver «el cambio de los estados del
/// proceso» y aceptó que se deje «un log o un historial».
/// </summary>
public sealed record EventoSimulacion
{
    public required int Instante { get; init; }
    public required int ProcesoId { get; init; }
    public required string ProcesoNombre { get; init; }
    public required EstadoProceso Estado { get; init; }

    public int? PaginaReferenciada { get; init; }
    public int? MarcoAsignado { get; init; }
    public bool HuboFalloDePagina { get; init; }
    public int? PaginaDesalojada { get; init; }
    public int? DuenioDeLaVictima { get; init; }
    public bool SeEscribioADisco { get; init; }
    public string? Observacion { get; init; }

    public char Letra => Estado switch
    {
        EstadoProceso.Ejecutando => 'E',
        EstadoProceso.Bloqueado  => 'B',
        EstadoProceso.Finalizado => 'F',
        _ => ' '
    };
}
