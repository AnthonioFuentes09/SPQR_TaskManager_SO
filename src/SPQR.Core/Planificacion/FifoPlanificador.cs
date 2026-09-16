using SPQR.Core.Dominio;

namespace SPQR.Core.Planificacion;

/// <summary>
/// FIFO (o FCFS). El más simple de los siete: se atiende en el orden en que
/// los procesos llegaron a la cola de listos y nadie interrumpe a nadie.
/// Como la lista de listos ya se mantiene en orden de llegada, basta con
/// mirar el primer nodo.
/// </summary>
public sealed class FifoPlanificador : IAlgoritmoPlanificacion
{
    public string Nombre => "FIFO";
    public bool EsApropiativo => false;
    public void Reiniciar() { }

    public Proceso? Seleccionar(ContextoPlanificacion c) => c.Listos.VerPrimero();
}
