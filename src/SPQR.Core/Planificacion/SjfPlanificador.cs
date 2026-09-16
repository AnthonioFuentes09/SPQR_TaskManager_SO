using SPQR.Core.Dominio;

namespace SPQR.Core.Planificacion;

/// <summary>
/// SJF — «primero el trabajo más corto». Entre los procesos que ya llegaron
/// elige al de ráfaga más corta y, una vez que entra, lo deja terminar.
///
/// La variante apropiativa (<c>expropiativo: true</c>) es SRTF: compara el
/// tiempo RESTANTE en cada instante, así que la llegada de un proceso más
/// corto saca al que está en la CPU. Por eso las dos versiones se parecen
/// tanto en las corridas donde todos llegan en t = 0: sin llegadas nuevas
/// nunca hay motivo para despojar y SRTF degenera en SJF.
/// </summary>
public sealed class SjfPlanificador(bool expropiativo = false) : IAlgoritmoPlanificacion
{
    public string Nombre => expropiativo ? "SRTF (SJF apropiativo)" : "SJF";
    public bool EsApropiativo => expropiativo;
    public void Reiniciar() { }

    public Proceso? Seleccionar(ContextoPlanificacion c)
    {
        Proceso? mejor = null;
        foreach (var p in c.Listos)
            if (mejor is null || Clave(p) < Clave(mejor)) mejor = p;

        if (!expropiativo || c.EnCpu is null) return mejor;

        // SRTF: solo se despoja si el candidato es estrictamente más corto.
        return mejor is not null && Clave(mejor) < Clave(c.EnCpu) ? mejor : c.EnCpu;
    }

    private long Clave(Proceso p)
        => ((long)(expropiativo ? p.TiempoRestante : p.Rafaga) << 20)
         + ((long)p.TiempoLlegada << 10) + p.Id;   // desempate: llegada, luego id
}
