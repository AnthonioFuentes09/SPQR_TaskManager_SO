using SPQR.Core.Dominio;

namespace SPQR.Core.Planificacion;

/// <summary>
/// Planificación por prioridad. Número menor = más urgente.
/// En la variante apropiativa, la llegada de un proceso más prioritario
/// saca de la CPU al que está corriendo.
///
/// Riesgo conocido: la inanición. Un proceso de prioridad baja puede no
/// entrar nunca si arriba siguen llegando procesos urgentes. Por eso se
/// incluye el envejecimiento opcional, que le sube la prioridad efectiva
/// a quien lleva mucho esperando.
/// </summary>
public sealed class PrioridadPlanificador(bool expropiativo = false, int envejecimiento = 0)
    : IAlgoritmoPlanificacion
{
    public string Nombre => expropiativo ? "Prioridad (apropiativa)" : "Prioridad";
    public bool EsApropiativo => expropiativo;
    public void Reiniciar() { }

    public Proceso? Seleccionar(ContextoPlanificacion c)
    {
        Proceso? mejor = null;
        foreach (var p in c.Listos)
            if (mejor is null || Clave(p, c.Instante) < Clave(mejor, c.Instante)) mejor = p;

        if (!expropiativo || c.EnCpu is null) return mejor;

        return mejor is not null && Clave(mejor, c.Instante) < Clave(c.EnCpu, c.Instante)
            ? mejor : c.EnCpu;
    }

    private long Clave(Proceso p, int instante)
    {
        var prioridad = p.Prioridad;
        if (envejecimiento > 0)
        {
            var esperando = instante - p.TiempoLlegada - p.CpuRecibida;
            prioridad -= esperando / envejecimiento;      // envejecer = bajar el número
        }
        return ((long)prioridad << 20) + ((long)p.TiempoLlegada << 10) + p.Id;
    }
}
