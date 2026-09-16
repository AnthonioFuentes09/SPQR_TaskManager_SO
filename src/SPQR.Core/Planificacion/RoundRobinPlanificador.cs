using SPQR.Core.Dominio;

namespace SPQR.Core.Planificacion;

/// <summary>
/// Round Robin. Toma al primero de la cola igual que FIFO; la diferencia está
/// en el quantum: cuando el proceso agota su tajada el simulador lo devuelve
/// al final de la lista de listos y vuelve a preguntar.
/// El remanente entre la ráfaga y el quantum queda guardado en el proceso,
/// tal como lo pidió el docente.
/// </summary>
public sealed class RoundRobinPlanificador(int quantum = 0) : IAlgoritmoPlanificacion
{
    public string Nombre => "Round Robin";
    public bool EsApropiativo => false;
    public void Reiniciar() { }

    public Proceso? Seleccionar(ContextoPlanificacion c) => c.Listos.VerPrimero();

    public int QuantumPara(Proceso proceso, ConfiguracionSO config)
        => quantum > 0 ? quantum : Math.Max(1, config.Quantum);
}
