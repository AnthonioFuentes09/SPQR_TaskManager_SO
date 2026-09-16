using SPQR.Core.Dominio;

namespace SPQR.Core.Planificacion;

/// <summary>
/// Traduce la clave que eligió el usuario en el menú a la implementación
/// concreta. Es el único lugar del programa que conoce los nombres: el motor
/// de simulación solo ve la interfaz, así que agregar un algoritmo nuevo
/// significa agregar una clase y una línea aquí.
/// </summary>
public static class FabricaPlanificadores
{
    public static readonly (string Clave, string Titulo)[] Disponibles =
    [
        ("Fifo",           "FIFO"),
        ("Sjf",            "SJF"),
        ("Srtf",           "SRTF (SJF apropiativo)"),
        ("RoundRobin",     "Round Robin"),
        ("Prioridad",      "Prioridad"),
        ("MultiplesColas", "Múltiples colas"),
        ("Garantizada",    "Planificación garantizada"),
        ("Sorteo",         "Planificación por sorteo"),
    ];

    public static IAlgoritmoPlanificacion Crear(string clave, ConfiguracionSO config) =>
        clave.ToLowerInvariant() switch
        {
            "fifo" or "fcfs"   => new FifoPlanificador(),
            "sjf"              => new SjfPlanificador(),
            "srtf"             => new SjfPlanificador(expropiativo: true),
            "roundrobin" or "rr" => new RoundRobinPlanificador(config.Quantum),
            "prioridad"        => new PrioridadPlanificador(),
            "prioridadapropiativa" => new PrioridadPlanificador(expropiativo: true),
            "multiplescolas"   => new MultiplesColasPlanificador(),
            "garantizada"      => new GarantizadaPlanificador(),
            "sorteo" or "loteria" => new SorteoPlanificador(config.SemillaSorteo),
            _ => throw new ArgumentException($"Algoritmo de planificación desconocido: {clave}", nameof(clave)),
        };
}
