using SPQR.Core.Estructuras;

namespace SPQR.Core.Dominio;

/// <summary>Parámetros de la máquina simulada (pantalla «Configuración SO»).</summary>
public sealed class ConfiguracionSO
{
    public int Quantum { get; set; } = 3;

    public int MemoriaRamKb { get; set; } = 32;
    public int PaginasVirtuales { get; set; } = 20;
    public int MarcosFisicos { get; set; } = 8;
    public int TamanioPaginaKb { get; set; } = 4;

    public int DiscosDuros { get; set; } = 1;
    public int AreaDeIntercambio { get; set; } = 40;

    public string AlgoritmoPlanificacion { get; set; } = "RoundRobin";
    public string AlgoritmoPaginacion { get; set; } = "Lru";

    /// <summary>
    /// Cada cuántos ticks el sistema apaga los bits R de todas las páginas.
    /// NRU y los algoritmos que miran el bit R dependen de esta limpieza:
    /// sin ella, a los pocos instantes todas las páginas quedarían con R = 1
    /// y la clasificación dejaría de distinguir nada.
    /// </summary>
    public int IntervaloReinicioBitR { get; set; } = 5;

    /// <summary>Reglas de las colas cuando el algoritmo elegido es «múltiples colas».</summary>
    public ListaEnlazada<ReglaDeCola> Colas { get; } = new();

    /// <summary>Semilla del sorteo, para que dos corridas iguales den el mismo resultado.</summary>
    public int SemillaSorteo { get; set; } = 2026;

    /// <summary>Si los marcos igualan o superan a las páginas nunca habrá reemplazo.</summary>
    public bool HabraReemplazo => PaginasVirtuales > MarcosFisicos;

    /// <summary>Relación virtual : físico, la presión de memoria del escenario.</summary>
    public double RelacionVirtualFisico
        => MarcosFisicos == 0 ? 0 : Math.Round((double)PaginasVirtuales / MarcosFisicos, 2);

    public ReglaDeCola ReglaDe(int numeroCola)
    {
        foreach (var r in Colas)
            if (r.Numero == numeroCola) return r;
        return new ReglaDeCola { Numero = numeroCola, Algoritmo = "Fifo", Quantum = Quantum };
    }

    public void ColasPorDefecto()
    {
        Colas.Limpiar();
        Colas.AgregarAlFinal(new ReglaDeCola { Numero = 1, Etiqueta = "Alta",  Algoritmo = "RoundRobin", Quantum = 2 });
        Colas.AgregarAlFinal(new ReglaDeCola { Numero = 2, Etiqueta = "Media", Algoritmo = "Sjf" });
        Colas.AgregarAlFinal(new ReglaDeCola { Numero = 3, Etiqueta = "Baja",  Algoritmo = "Fifo" });
    }
}
