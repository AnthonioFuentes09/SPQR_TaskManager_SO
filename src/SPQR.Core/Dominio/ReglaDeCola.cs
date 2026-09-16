namespace SPQR.Core.Dominio;

/// <summary>
/// Regla de una de las colas de «múltiples colas». Cada cola tiene su propia
/// prioridad y su propio algoritmo interno: ese es el detalle que convierte a
/// múltiples colas en el único algoritmo compuesto de los siete.
/// </summary>
public sealed class ReglaDeCola
{
    /// <summary>1 es la cola de mayor prioridad.</summary>
    public required int Numero { get; init; }

    public string Etiqueta { get; set; } = "";

    /// <summary>Clave del algoritmo interno: Fifo, Sjf, RoundRobin, Prioridad.</summary>
    public string Algoritmo { get; set; } = "RoundRobin";

    /// <summary>Solo lo usa Round Robin. 0 = hereda el quantum global.</summary>
    public int Quantum { get; set; }
}
