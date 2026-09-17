using SPQR.Core.Estructuras;

namespace SPQR.Core.Dominio;

/// <summary>Parámetros de la máquina simulada (pantalla «Configuración SO»).</summary>
public sealed class ConfiguracionSO : ObjetoObservable
{
    private int _quantum = 3;
    private int _memoriaRamKb = 32;
    private int _paginasVirtuales = 20;
    private int _marcosFisicos = 8;
    private int _tamanioPaginaKb = 4;
    private int _discosDuros = 1;
    private int _areaDeIntercambio = 40;
    private int _intervaloReinicioBitR = 5;
    private int _semillaSorteo = 2026;
    private string _algoritmoPlanificacion = "RoundRobin";
    private string _algoritmoPaginacion = "Lru";

    public int Quantum
    {
        get => _quantum;
        set => Asignar(ref _quantum, Math.Max(1, value));
    }

    public int MemoriaRamKb
    {
        get => _memoriaRamKb;
        set => Asignar(ref _memoriaRamKb, Math.Max(1, value));
    }

    public int PaginasVirtuales
    {
        get => _paginasVirtuales;
        set { if (Asignar(ref _paginasVirtuales, Math.Max(1, value))) NotificarDerivados(); }
    }

    public int MarcosFisicos
    {
        get => _marcosFisicos;
        set { if (Asignar(ref _marcosFisicos, Math.Max(1, value))) NotificarDerivados(); }
    }

    public int TamanioPaginaKb
    {
        get => _tamanioPaginaKb;
        set => Asignar(ref _tamanioPaginaKb, Math.Max(1, value));
    }

    public int DiscosDuros
    {
        get => _discosDuros;
        set => Asignar(ref _discosDuros, Math.Max(1, value));
    }

    public int AreaDeIntercambio
    {
        get => _areaDeIntercambio;
        set => Asignar(ref _areaDeIntercambio, Math.Max(0, value));
    }

    /// <summary>
    /// Cada cuántos ticks el sistema apaga los bits R de todas las páginas.
    /// NRU y los algoritmos que miran el bit R dependen de esta limpieza:
    /// sin ella, a los pocos instantes todas las páginas quedarían con R = 1
    /// y la clasificación dejaría de distinguir nada. 0 la desactiva.
    /// </summary>
    public int IntervaloReinicioBitR
    {
        get => _intervaloReinicioBitR;
        set => Asignar(ref _intervaloReinicioBitR, Math.Max(0, value));
    }

    /// <summary>Semilla del sorteo, para que dos corridas iguales den el mismo resultado.</summary>
    public int SemillaSorteo
    {
        get => _semillaSorteo;
        set => Asignar(ref _semillaSorteo, value);
    }

    public string AlgoritmoPlanificacion
    {
        get => _algoritmoPlanificacion;
        set { if (Asignar(ref _algoritmoPlanificacion, value)) Notificar(nameof(UsaMultiplesColas)); }
    }

    public string AlgoritmoPaginacion
    {
        get => _algoritmoPaginacion;
        set => Asignar(ref _algoritmoPaginacion, value);
    }

    /// <summary>Reglas de las colas cuando el algoritmo elegido es «múltiples colas».</summary>
    public ListaEnlazada<ReglaDeCola> Colas { get; } = new();

    public bool UsaMultiplesColas
        => AlgoritmoPlanificacion.Equals("MultiplesColas", StringComparison.OrdinalIgnoreCase);

    /// <summary>Si los marcos igualan o superan a las páginas nunca habrá reemplazo.</summary>
    public bool HabraReemplazo => PaginasVirtuales > MarcosFisicos;

    /// <summary>Relación virtual : físico, la presión de memoria del escenario.</summary>
    public double RelacionVirtualFisico
        => MarcosFisicos == 0 ? 0 : Math.Round((double)PaginasVirtuales / MarcosFisicos, 2);

    private void NotificarDerivados()
    {
        Notificar(nameof(RelacionVirtualFisico));
        Notificar(nameof(HabraReemplazo));
    }

    /// <summary>
    /// Devuelve la regla de una cola. Si no existe, se inventa una por FIFO —
    /// pero eso es una red de seguridad, no un comportamiento deseado: el
    /// validador avisa antes de simular si algún proceso quedó en una cola
    /// que nadie definió.
    /// </summary>
    public ReglaDeCola ReglaDe(int numeroCola)
    {
        foreach (var r in Colas)
            if (r.Numero == numeroCola) return r;
        return new ReglaDeCola { Numero = numeroCola, Algoritmo = "Fifo", Quantum = Quantum };
    }

    public bool ExisteCola(int numeroCola)
    {
        foreach (var r in Colas)
            if (r.Numero == numeroCola) return true;
        return false;
    }

    public void ColasPorDefecto()
    {
        Colas.Limpiar();
        Colas.AgregarAlFinal(new ReglaDeCola { Numero = 1, Etiqueta = "Alta",  Algoritmo = "RoundRobin", Quantum = 2 });
        Colas.AgregarAlFinal(new ReglaDeCola { Numero = 2, Etiqueta = "Media", Algoritmo = "Sjf" });
        Colas.AgregarAlFinal(new ReglaDeCola { Numero = 3, Etiqueta = "Baja",  Algoritmo = "Fifo" });
    }
}
