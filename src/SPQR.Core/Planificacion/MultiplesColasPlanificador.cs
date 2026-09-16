using SPQR.Core.Dominio;
using SPQR.Core.Estructuras;

namespace SPQR.Core.Planificacion;

/// <summary>
/// Múltiples colas: el único algoritmo compuesto de los siete, porque usa a
/// los otros por dentro.
///
/// Toma DOS decisiones en cada instante:
///   1. Elige la cola de mayor prioridad (número más bajo) que no esté vacía.
///   2. Dentro de esa cola aplica el algoritmo que esa cola tenga configurado
///      — Round Robin, SJF, FIFO o prioridad.
///
/// La primera decisión es la que produce el comportamiento característico:
/// mientras haya alguien en la cola 1, las colas 2 y 3 no corren. Por eso un
/// proceso de cola baja puede sufrir inanición, y por eso los sistemas reales
/// agregan realimentación (mover procesos entre colas), que aquí se deja
/// documentada pero fuera del alcance.
/// </summary>
public sealed class MultiplesColasPlanificador : IAlgoritmoPlanificacion
{
    private readonly Dictionary<string, IAlgoritmoPlanificacion> _internos = new(StringComparer.OrdinalIgnoreCase);

    public string Nombre => "Múltiples colas";

    /// <summary>
    /// Es apropiativo porque la llegada de un proceso a una cola más
    /// prioritaria debe poder sacar de la CPU al que corre en una cola baja.
    /// </summary>
    public bool EsApropiativo => true;

    public void Reiniciar()
    {
        foreach (var a in _internos.Values) a.Reiniciar();
    }

    public Proceso? Seleccionar(ContextoPlanificacion c)
    {
        // --- decisión 1: la cola de mayor prioridad que tenga alguien ---
        var cola = ColaMasPrioritariaConGente(c.Listos, c.EnCpu);
        if (cola is null) return c.EnCpu;

        // Si quien está en la CPU pertenece a una cola más prioritaria, se queda.
        if (c.EnCpu is not null && c.EnCpu.Cola < cola.Value) return c.EnCpu;

        // --- decisión 2: dentro de esa cola, su propio algoritmo ---
        var regla = c.Config.ReglaDe(cola.Value);
        var interno = ObtenerInterno(regla);

        var subConjunto = new ListaEnlazada<Proceso>();
        foreach (var p in c.Listos)
            if (p.Cola == cola.Value) subConjunto.AgregarAlFinal(p);

        var enCpuDeEstaCola = c.EnCpu is not null && c.EnCpu.Cola == cola.Value ? c.EnCpu : null;

        return interno.Seleccionar(new ContextoPlanificacion
        {
            Listos = subConjunto,
            EnCpu = enCpuDeEstaCola,
            Instante = c.Instante,
            Config = c.Config,
            CpuTotalRepartida = c.CpuTotalRepartida,
        }) ?? c.EnCpu;
    }

    private static int? ColaMasPrioritariaConGente(ListaEnlazada<Proceso> listos, Proceso? enCpu)
    {
        int? mejor = null;
        foreach (var p in listos)
            if (mejor is null || p.Cola < mejor) mejor = p.Cola;
        if (enCpu is not null && (mejor is null || enCpu.Cola < mejor)) mejor = enCpu.Cola;
        return mejor;
    }

    private IAlgoritmoPlanificacion ObtenerInterno(ReglaDeCola regla)
    {
        var clave = $"{regla.Numero}:{regla.Algoritmo}:{regla.Quantum}";
        if (_internos.TryGetValue(clave, out var existente)) return existente;

        IAlgoritmoPlanificacion nuevo = regla.Algoritmo.ToLowerInvariant() switch
        {
            "roundrobin" or "rr" => new RoundRobinPlanificador(regla.Quantum),
            "sjf"                => new SjfPlanificador(),
            "srtf"               => new SjfPlanificador(expropiativo: true),
            "prioridad"          => new PrioridadPlanificador(),
            _                    => new FifoPlanificador(),
        };
        _internos[clave] = nuevo;
        return nuevo;
    }

    /// <summary>El quantum sale de la regla de la cola a la que pertenece el proceso.</summary>
    public int QuantumPara(Proceso proceso, ConfiguracionSO config)
    {
        var regla = config.ReglaDe(proceso.Cola);
        return regla.Algoritmo.Equals("RoundRobin", StringComparison.OrdinalIgnoreCase)
            ? (regla.Quantum > 0 ? regla.Quantum : Math.Max(1, config.Quantum))
            : 0;
    }
}
