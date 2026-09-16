using SPQR.Core.Dominio;

namespace SPQR.Core.Planificacion;

/// <summary>
/// Planificación garantizada. A cada proceso se le prometió un porcentaje del
/// procesador (el campo «% asignación» de la pantalla 1). El algoritmo mira
/// cuánta CPU recibió realmente cada uno contra cuánta le tocaba y le da el
/// turno al que va más atrasado respecto de su promesa.
///
/// Se implementa con la razón habitual:
///     razón = CPU recibida ÷ CPU prometida
/// y gana la razón más baja. Una razón de 0.5 significa «recibió la mitad de
/// lo que le corresponde»; una de 1.0, que está exactamente al día.
/// </summary>
public sealed class GarantizadaPlanificador : IAlgoritmoPlanificacion
{
    public string Nombre => "Planificación garantizada";
    public bool EsApropiativo => true;
    public void Reiniciar() { }

    public Proceso? Seleccionar(ContextoPlanificacion c)
    {
        Proceso? mejor = null;
        var mejorRazon = double.MaxValue;

        foreach (var p in Candidatos(c))
        {
            var razon = Razon(p, c.CpuTotalRepartida);
            if (razon < mejorRazon - 1e-9 ||
               (Math.Abs(razon - mejorRazon) < 1e-9 && mejor is not null && p.Id < mejor.Id))
            {
                mejor = p;
                mejorRazon = razon;
            }
        }
        return mejor;
    }

    private static IEnumerable<Proceso> Candidatos(ContextoPlanificacion c)
    {
        foreach (var p in c.Listos) yield return p;
        if (c.EnCpu is not null) yield return c.EnCpu;
    }

    /// <summary>CPU recibida ÷ CPU prometida. Menor razón = más atrasado.</summary>
    private static double Razon(Proceso p, int cpuTotal)
    {
        var prometida = cpuTotal * (p.PorcentajeAsignacion / 100.0);
        return prometida <= 0 ? 0 : p.CpuRecibida / prometida;
    }

    public int QuantumPara(Proceso proceso, ConfiguracionSO config) => 1;
}
