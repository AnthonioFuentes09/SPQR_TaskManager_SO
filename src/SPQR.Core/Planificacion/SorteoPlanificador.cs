using SPQR.Core.Dominio;

namespace SPQR.Core.Planificacion;

/// <summary>
/// Planificación por sorteo (lottery scheduling). Cada proceso tiene una
/// cantidad de boletos; en cada turno se rifa un número entre 1 y el total de
/// boletos en juego y gana el proceso en cuyo tramo cayó el número.
///
/// La gracia del algoritmo es que reparte la CPU en proporción a los boletos
/// sin tener que llevar contabilidad: un proceso con el doble de boletos gana
/// aproximadamente el doble de sorteos. No hay inanición mientras el proceso
/// tenga al menos un boleto.
///
/// El generador va con semilla fija para que dos corridas de la misma
/// configuración den exactamente el mismo resultado; si no, el informe
/// comparativo entre algoritmos de paginación no sería reproducible.
/// </summary>
public sealed class SorteoPlanificador(int semilla = 2026) : IAlgoritmoPlanificacion
{
    private Random _rnd = new(semilla);

    public string Nombre => "Planificación por sorteo";
    public bool EsApropiativo => true;

    public void Reiniciar() => _rnd = new Random(semilla);

    public Proceso? Seleccionar(ContextoPlanificacion c)
    {
        var total = 0;
        foreach (var p in c.Listos) total += Math.Max(1, p.Boletos);
        if (c.EnCpu is not null) total += Math.Max(1, c.EnCpu.Boletos);
        if (total == 0) return c.Listos.VerPrimero();

        var ganador = _rnd.Next(1, total + 1);

        var acumulado = 0;
        foreach (var p in c.Listos)
        {
            acumulado += Math.Max(1, p.Boletos);
            if (ganador <= acumulado) return p;
        }
        return c.EnCpu ?? c.Listos.VerPrimero();
    }

    public int QuantumPara(Proceso proceso, ConfiguracionSO config)
        => Math.Max(1, config.Quantum);
}
