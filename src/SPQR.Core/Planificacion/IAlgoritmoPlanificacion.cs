using SPQR.Core.Dominio;
using SPQR.Core.Estructuras;

namespace SPQR.Core.Planificacion;

/// <summary>Lo que el planificador ve para decidir quién entra a la CPU.</summary>
public sealed class ContextoPlanificacion
{
    public required ListaEnlazada<Proceso> Listos { get; init; }
    public required Proceso? EnCpu { get; init; }
    public required int Instante { get; init; }
    public required ConfiguracionSO Config { get; init; }

    /// <summary>Tiempo de CPU repartido hasta ahora; lo necesita «garantizada».</summary>
    public int CpuTotalRepartida { get; init; }
}

/// <summary>
/// Contrato de los algoritmos de planificación. Se implementan los siete:
/// FIFO, SJF, Round Robin, Prioridad, Múltiples colas, Garantizada y Sorteo.
///
/// Reglas del contrato:
///  · <see cref="Seleccionar"/> DEVUELVE al candidato pero NO lo saca de la
///    lista de listos: de eso se encarga el simulador, para que el algoritmo
///    quede libre de efectos secundarios y sea fácil de probar.
///  · Si el algoritmo es apropiativo, el simulador lo consulta en cada tick;
///    si no lo es, solo cuando la CPU queda libre.
/// </summary>
public interface IAlgoritmoPlanificacion
{
    string Nombre { get; }
    bool EsApropiativo { get; }
    void Reiniciar();
    Proceso? Seleccionar(ContextoPlanificacion contexto);

    /// <summary>
    /// Tajada de tiempo que le toca al proceso. 0 significa «sin quantum»:
    /// el proceso corre hasta terminar o hasta que alguien lo despoje.
    /// </summary>
    int QuantumPara(Proceso proceso, ConfiguracionSO config) => 0;
}
