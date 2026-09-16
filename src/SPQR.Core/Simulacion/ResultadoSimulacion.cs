using SPQR.Core.Dominio;
using SPQR.Core.Estructuras;
using SPQR.Core.Metricas;

namespace SPQR.Core.Simulacion;

/// <summary>
/// Lo que devuelve una corrida completa. La interfaz reproduce esto;
/// no vuelve a llamar al motor.
/// </summary>
public sealed class ResultadoSimulacion
{
    public required ListaEnlazada<EventoSimulacion> Eventos { get; init; }
    public required ListaEnlazada<Proceso> Procesos { get; init; }

    /// <summary>Una foto de la memoria física por cada instante simulado.</summary>
    public ListaEnlazada<FotoMemoria> Memoria { get; init; } = new();

    public required MetricasMmu Mmu { get; init; }
    public required MetricasPlanificador Planificador { get; init; }
    public int InstanteFinal { get; set; }

    public string NombreAlgoritmoPlanificacion { get; set; } = "";
    public string NombreAlgoritmoPaginacion { get; set; } = "";
}
