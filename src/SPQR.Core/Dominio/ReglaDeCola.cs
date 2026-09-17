namespace SPQR.Core.Dominio;

/// <summary>
/// Regla de una de las colas de «múltiples colas». Cada cola tiene su propia
/// prioridad y su propio algoritmo interno: ese es el detalle que convierte a
/// múltiples colas en el único algoritmo compuesto de los siete.
/// </summary>
public sealed class ReglaDeCola : ObjetoObservable
{
    private int _numero = 1;
    private string _etiqueta = "";
    private string _algoritmo = "RoundRobin";
    private int _quantum;

    /// <summary>1 es la cola de mayor prioridad.</summary>
    public required int Numero
    {
        get => _numero;
        set { if (Asignar(ref _numero, Math.Max(1, value))) Notificar(nameof(Descripcion)); }
    }

    public string Etiqueta
    {
        get => _etiqueta;
        set { if (Asignar(ref _etiqueta, value)) Notificar(nameof(Descripcion)); }
    }

    /// <summary>Clave del algoritmo interno: Fifo, Sjf, RoundRobin, Prioridad.</summary>
    public string Algoritmo
    {
        get => _algoritmo;
        set => Asignar(ref _algoritmo, value);
    }

    /// <summary>Solo lo usa Round Robin. 0 = hereda el quantum global.</summary>
    public int Quantum
    {
        get => _quantum;
        set => Asignar(ref _quantum, Math.Max(0, value));
    }

    /// <summary>Lo que se ve en el desplegable de la columna COLA.</summary>
    public string Descripcion => string.IsNullOrWhiteSpace(Etiqueta) ? $"{Numero}" : $"{Numero} · {Etiqueta}";
}
